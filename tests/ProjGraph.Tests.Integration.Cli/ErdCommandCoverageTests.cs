using ProjGraph.Tests.Integration.Cli.Helpers;
using ProjGraph.Tests.Shared.Helpers;
using Spectre.Console;
using Spectre.Console.Testing;
using System.Text;

namespace ProjGraph.Tests.Integration.Cli;

/// <summary>
/// Integration tests for the <c>erd</c> command paths that the main suite never reaches: automatic
/// discovery of a DbContext/ModelSnapshot when no path argument is supplied, the interactive
/// selection prompts shown when the discovery is ambiguous, and the ModelSnapshot analysis branch.
/// </summary>
[Collection("CLI Tests")]
public sealed class ErdCommandCoverageTests
{
    [Fact]
    public void ErdCommand_NoPathAndNoCandidateFiles_ShouldReportNoFileFoundAndExitOne()
    {
        // Arrange — an empty working directory, so auto-discovery finds nothing.
        using var temp = new TestDirectory();
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = 0;
        var output = RunInDirectory(temp.DirectoryPath,
            () => CliTestHelpers.CaptureConsoleOutput(() => exitCode = app.Run(["erd"])));

        // Assert — an omitted path is valid (discovery mode), but finding nothing must fail loudly
        // with usage guidance rather than rendering an empty diagram.
        exitCode.Should().Be(1);
        output.Should().Contain("No DbContext or ModelSnapshot .cs file found.");
        output.Should().Contain("projgraph erd path/to/YourDbContext.cs",
            "the error should tell the user how to pass an explicit path");
    }

    [Fact]
    public void ErdCommand_NoPathAndSingleCandidate_ShouldDiscoverItAndRenderDiagram()
    {
        // Arrange — exactly one *DbContext.cs below the working directory.
        using var temp = new TestDirectory();
        temp.CreateFile(Path.Combine("Data", "WidgetDbContext.cs"), WidgetContextSource("WidgetDbContext"));
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = RunInDirectory(temp.DirectoryPath,
            () => CliTestHelpers.CaptureConsoleOutput(() => exitCode = app.Run(["erd", "--show-title", "false"])));

        // Assert — the single candidate is used without prompting, and the user is told which file
        // was picked so an implicit choice is never silent.
        exitCode.Should().Be(0);
        output.Should().Contain("Using ", "the auto-selected file name should be announced");
        output.Should().Contain("WidgetDbContext.cs");
        output.Should().Contain("erDiagram");
        output.Should().Contain("Widget {");
    }

    [Fact]
    public void ErdCommand_NoPath_ShouldIgnoreCandidatesUnderExcludedDirectories()
    {
        // Arrange — a build-output copy under bin/ plus one real source file. If the excluded
        // directory were not filtered out, discovery would see two files and prompt instead.
        using var temp = new TestDirectory();
        temp.CreateFile(Path.Combine("src", "WidgetDbContext.cs"), WidgetContextSource("WidgetDbContext"));
        temp.CreateFile(Path.Combine("bin", "Debug", "WidgetDbContext.cs"), WidgetContextSource("WidgetDbContext"));
        temp.CreateFile(Path.Combine("obj", "StaleDbContext.cs"), WidgetContextSource("StaleDbContext"));
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = RunInDirectory(temp.DirectoryPath,
            () => CliTestHelpers.CaptureConsoleOutput(() => exitCode = app.Run(["erd", "--show-title", "false"])));

        // Assert — the single non-excluded candidate is selected outright.
        exitCode.Should().Be(0);
        output.Should().Contain("Using ");
        output.Should().Contain("WidgetDbContext.cs");
        output.Should().NotContain("Multiple files found",
            "generated copies under bin/ and obj/ must not make the discovery ambiguous");
        output.Should().NotContain("StaleDbContext",
            "a DbContext under obj/ must never be offered");
        output.Should().Contain("Widget {");
    }

    [Fact]
    public void ErdCommand_NoPathAndMultipleCandidates_ShouldPromptAndUseTheSelectedFile()
    {
        // Arrange — two discoverable contexts in different directories. Both describe the same
        // Widget entity, so the rendered diagram is deterministic regardless of enumeration order,
        // while the prompt itself must list both distinct paths.
        using var temp = new TestDirectory();
        temp.CreateFile(Path.Combine("Billing", "BillingDbContext.cs"), WidgetContextSource("BillingDbContext"));
        temp.CreateFile(Path.Combine("Shipping", "ShippingDbContext.cs"), WidgetContextSource("ShippingDbContext"));
        var app = CliTestHelpers.CreateApp();

        // Act — accept the highlighted (first) choice.
        var exitCode = -1;
        var output = RunInDirectory(temp.DirectoryPath,
            () => CaptureInteractiveOutput(
                input => input.PushKey(ConsoleKey.Enter),
                () => exitCode = app.Run(["erd", "--show-title", "false"])));

        // Assert — the prompt is shown with both candidates, and the selection resolves back to a
        // real file that is then analysed.
        exitCode.Should().Be(0);
        output.Should().Contain("Multiple files found. Please select one:");
        output.Should().Contain("BillingDbContext.cs");
        output.Should().Contain("ShippingDbContext.cs");
        output.Should().Contain("erDiagram");
        output.Should().Contain("Widget {");
    }

    [Fact]
    public void ErdCommand_NoPathAndMultipleCandidates_NonInteractiveTerminal_ShouldExitOne()
    {
        // Arrange — the same ambiguous discovery, but on a terminal that cannot prompt (CI, pipes).
        using var temp = new TestDirectory();
        temp.CreateFile(Path.Combine("Billing", "BillingDbContext.cs"), WidgetContextSource("BillingDbContext"));
        temp.CreateFile(Path.Combine("Shipping", "ShippingDbContext.cs"), WidgetContextSource("ShippingDbContext"));
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = RunInDirectory(temp.DirectoryPath,
            () => CliTestHelpers.CaptureConsoleOutput(() => exitCode = app.Run(["erd"])));

        // Assert — it must fail with a readable message rather than hanging or throwing raw.
        exitCode.Should().Be(1);
        output.Should().Contain("interactive",
            "the failure should explain that a selection prompt cannot be shown");
    }

    [Fact]
    public void ErdCommand_ModelSnapshotFile_ShouldRenderDiagramFromTheSnapshot()
    {
        // Arrange — a file whose name ends in ModelSnapshot.cs must take the snapshot analysis
        // branch (EF-generated fluent builder) rather than the DbContext branch.
        using var temp = new TestDirectory();
        var fixturePath = CliTestHelpers.GetRootPath(Path.Combine(
            "tests", "ProjGraph.Tests.Unit.EntityFramework", "Golden", "fixtures", "JournalSnapshot.cs"));
        var snapshotPath = temp.CreateFile("JournalContextModelSnapshot.cs", File.ReadAllText(fixturePath));
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = CliTestHelpers.CaptureConsoleOutput(() =>
            exitCode = app.Run(["erd", snapshotPath]));

        // Assert — entities and the relationship declared in the snapshot are rendered, and the
        // title comes from the snapshot's context rather than the file name.
        exitCode.Should().Be(0);
        output.Should().Contain("title: JournalContext");
        output.Should().Contain("erDiagram");
        output.Should().Contain("Entry {");
        output.Should().Contain("Journal {");
        output.Should().Contain("Journal ||--o{ Entry");
    }

    [Fact]
    public void ErdCommand_MultipleContextsInOneFile_ShouldPromptForTheContextToRender()
    {
        // Arrange — one file declaring two DbContext types and no --context option, which is the
        // only way the per-context selection prompt is reached.
        using var temp = new TestDirectory();
        var contextPath = temp.CreateFile("TwoContexts.cs", TwoContextSource);
        var app = CliTestHelpers.CreateApp();

        // Act — accept the highlighted (first) context.
        var exitCode = -1;
        var output = CaptureInteractiveOutput(
            input => input.PushKey(ConsoleKey.Enter),
            () => exitCode = app.Run(["erd", contextPath, "--show-title", "false"]));

        // Assert
        exitCode.Should().Be(0);
        output.Should().Contain("Multiple DbContexts found. Please select one:");
        output.Should().Contain("BillingDbContext");
        output.Should().Contain("ShippingDbContext");
        output.Should().Contain("erDiagram");
        output.Should().Contain("Widget {");
    }

    [Fact]
    public void ErdCommand_MultipleContextsInOneFile_WithContextOption_ShouldSkipThePrompt()
    {
        // Arrange — naming the context explicitly must bypass the prompt entirely, so the command
        // stays usable on a non-interactive terminal.
        using var temp = new TestDirectory();
        var contextPath = temp.CreateFile("TwoContexts.cs", TwoContextSource);
        var app = CliTestHelpers.CreateApp();

        // Act
        var exitCode = -1;
        var output = CliTestHelpers.CaptureConsoleOutput(() =>
            exitCode = app.Run(["erd", contextPath, "--context", "ShippingDbContext"]));

        // Assert
        exitCode.Should().Be(0);
        output.Should().NotContain("Multiple DbContexts found");
        output.Should().Contain("title: ShippingDbContext");
        output.Should().Contain("Widget {");
    }

    private static string WidgetContextSource(string contextName)
    {
        return $$"""
                 using Microsoft.EntityFrameworkCore;

                 namespace CoverageFixtures;

                 public class {{contextName}} : DbContext
                 {
                     public DbSet<Widget> Widgets { get; set; }
                 }

                 public class Widget
                 {
                     public int Id { get; set; }
                     public string Name { get; set; }
                 }
                 """;
    }

    private const string TwoContextSource = """
                                            using Microsoft.EntityFrameworkCore;

                                            namespace CoverageFixtures;

                                            public class BillingDbContext : DbContext
                                            {
                                                public DbSet<Widget> Widgets { get; set; }
                                            }

                                            public class ShippingDbContext : DbContext
                                            {
                                                public DbSet<Widget> Widgets { get; set; }
                                            }

                                            public class Widget
                                            {
                                                public int Id { get; set; }
                                                public string Name { get; set; }
                                            }
                                            """;

    /// <summary>
    /// Runs <paramref name="action"/> with the process working directory temporarily switched to
    /// <paramref name="directory"/>, which is what the <c>erd</c> command searches when no path
    /// argument is given. Safe because every class in this assembly shares one xUnit collection and
    /// therefore never runs concurrently.
    /// </summary>
    /// <param name="directory">The directory to make current for the duration of the call.</param>
    /// <param name="action">The action to run; its return value is passed through.</param>
    /// <returns>Whatever <paramref name="action"/> returned.</returns>
    private static string RunInDirectory(string directory, Func<string> action)
    {
        var original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(directory);
        try
        {
            return action();
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    /// <summary>
    /// Captures command output while presenting an interactive terminal, so selection prompts can
    /// be driven by pushed key presses instead of failing as "not interactive".
    /// </summary>
    /// <param name="pushInput">Queues the key presses the prompt should consume.</param>
    /// <param name="action">The command invocation to capture.</param>
    /// <returns>The combined standard output and standard error produced by <paramref name="action"/>.</returns>
    private static string CaptureInteractiveOutput(Action<TestConsoleInput> pushInput, Action action)
    {
        var stderr = new StringBuilder();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var errorWriter = new StringWriter(stderr);
        using var testConsole = new TestConsole();
        testConsole.Interactive();
        testConsole.Profile.Capabilities.Unicode = true;
        testConsole.Profile.Width = 200;
        pushInput(testConsole.Input);

        try
        {
            Console.SetOut(errorWriter);
            Console.SetError(errorWriter);
            AnsiConsole.Console = testConsole;

            action();

            errorWriter.Flush();
            return testConsole.Output + stderr;
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings());
        }
    }
}
