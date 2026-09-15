using ProjGraph.Tests.Shared.Helpers;
using ProjGraph.Tests.Smoke.Aot.Helpers;
using System.Text.RegularExpressions;

namespace ProjGraph.Tests.Smoke.Aot;

/// <summary>
/// Runs CLI commands through the native executable and through the JIT build of the same commit,
/// and requires the same exit code, standard output, standard error, and written diagram file.
/// </summary>
public sealed partial class CliParityTests : IDisposable
{
    private const string ModularSolution = "samples/visualize/modular-architecture/ModularArchitecture.slnx";

    private readonly TestDirectory _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [AotSmokeFact]
    public async Task Visualize_Mermaid_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("visualize", ModularSolution, "--format", "mermaid");
    }

    [AotSmokeFact]
    public async Task Visualize_Tree_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("visualize", ModularSolution, "--format", "tree");
    }

    [AotSmokeFact]
    public async Task Visualize_Flat_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("visualize", ModularSolution, "--format", "flat");
    }

    [AotSmokeFact]
    public async Task Visualize_LegacySln_ShouldMatchReference()
    {
        await AssertDiagramParityAsync(
            "visualize", "tests/ProjGraph.Tests.Smoke.Aot/Fixtures/legacy.sln", "--format", "mermaid");
    }

    [AotSmokeFact]
    public async Task Erd_ComplexEcommerce_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("erd", "samples/erd/complex-ecommerce/Data/MyDbContext.cs");
    }

    [AotSmokeFact]
    public async Task Erd_SimpleContext_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("erd", "samples/erd/simple-context/EntityFramework/MyDbContext.cs");
    }

    [AotSmokeFact]
    public async Task ClassDiagram_DesignPatterns_ShouldMatchReference()
    {
        await AssertDiagramParityAsync(
            "classdiagram", "samples/classdiagram/design-patterns/Domain/Order.cs",
            "--inheritance", "--dependencies", "--depth", "2", "--properties", "true", "--functions", "true");
    }

    [AotSmokeFact]
    public async Task ClassDiagram_ComplexHierarchy_ShouldMatchReference()
    {
        await AssertDiagramParityAsync(
            "classdiagram", "samples/classdiagram/complex-hierarchy/Domain/Models/CEO.cs",
            "--inheritance", "--dependencies", "--depth", "5", "--properties", "true", "--functions", "true");
    }

    [AotSmokeFact]
    public async Task ClassDiagram_SimpleHierarchy_ShouldMatchReference()
    {
        await AssertDiagramParityAsync(
            "classdiagram", "samples/classdiagram/simple-hierarchy/Models/Admin.cs",
            "--inheritance", "--dependencies", "--depth", "2", "--properties", "true", "--functions", "true");
    }

    [AotSmokeFact]
    public async Task ClassDiagram_SourceDirectory_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("classdiagram", "src", "--inheritance", "--dependencies");
    }

    [AotSmokeFact]
    public async Task ClassDiagram_ConcurrencyAndAnnotationTypes_ShouldMatchReference()
    {
        // BCL types that bind only through the embedded reference assemblies (spec §1.5).
        _temp.CreateFile("Types/Types.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var file = _temp.CreateFile("Types/Inventory.cs",
            """
            using System.Collections.Concurrent;
            using System.ComponentModel.DataAnnotations;

            namespace Types;

            public class Inventory : Dictionary<string, int>, IDisposable
            {
                [Required]
                public string Name { get; set; } = "";

                public ConcurrentDictionary<string, StockItem> Items { get; } = new();

                public Barrier? Gate { get; set; }

                public IEnumerable<StockItem> Available() => Items.Values;

                public void Dispose() => Gate?.Dispose();
            }

            public record StockItem(string Sku, decimal Price);
            """);

        await AssertDiagramParityAsync("classdiagram", file, "--inheritance", "--dependencies");
    }

    [AotSmokeFact]
    public async Task Visualize_SolutionWithMalformedProject_ShouldMatchReference()
    {
        var solution = CreateSolutionWithMalformedProject();

        await AssertDiagramParityAsync("visualize", solution, "--format", "mermaid");
    }

    [AotSmokeFact]
    public async Task Visualize_SlnListingAProjectTwice_ShouldFailLikeReference()
    {
        _temp.CreateFile("Good/Good.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var solution = _temp.CreateFile("Duplicate.sln",
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Good", "Good\Good.csproj", "{0A5E0000-0000-4000-8000-000000000001}"
            EndProject
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "GoodAgain", "Good\Good.csproj", "{0A5E0000-0000-4000-8000-000000000002}"
            EndProject
            """);

        await AssertParityAsync(expectSuccess: false, "visualize", solution, "--format", "mermaid");
    }

    [AotSmokeFact]
    public async Task Stats_Repository_ShouldMatchReferenceApartFromTiming()
    {
        await AssertStatsParityAsync("stats", "ProjGraph.slnx");
    }

    [AotSmokeFact]
    public async Task Stats_SolutionWithMalformedProject_ShouldMatchReferenceApartFromTiming()
    {
        var solution = CreateSolutionWithMalformedProject();

        await AssertStatsParityAsync("stats", solution);
    }

    [AotSmokeFact]
    public async Task Stats_UnknownOption_ShouldFailLikeReference()
    {
        // Strict parsing rejects the typo before the command runs (Spectre's error rendering path).
        await AssertParityAsync(expectSuccess: false, "stats", "ProjGraph.slnx", "--tpo", "3");
    }

    [AotSmokeFact]
    public async Task Help_Root_ShouldMatchReference()
    {
        // Help rendering is the most reflection-heavy Spectre path (descriptions and defaults on
        // settings types).
        await AssertParityAsync(expectSuccess: true, "--help");
    }

    [AotSmokeFact]
    public async Task Help_Visualize_ShouldMatchReference()
    {
        await AssertParityAsync(expectSuccess: true, "visualize", "--help");
    }

    [AotSmokeFact]
    public async Task Version_ShouldMatchReference()
    {
        // The version is read from an assembly attribute, which the native build must keep.
        var reference = await AssertParityAsync(expectSuccess: true, "--version");
        reference.StandardOutput.Should().NotBeNullOrWhiteSpace("the version must be printed");
    }

    private string CreateSolutionWithMalformedProject()
    {
        _temp.CreateFile("Good/Good.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        _temp.CreateFile("Bad/Bad.csproj", "<Project><PropertyGroup></Project>");
        return _temp.CreateFile("Malformed.slnx",
            "<Solution><Project Path=\"Good/Good.csproj\" /><Project Path=\"Bad/Bad.csproj\" /></Solution>");
    }

    /// <summary>
    /// Runs both builds and requires they agree, pinning the reference outcome first.
    /// </summary>
    /// <param name="expectSuccess">Whether the reference build must exit with 0.</param>
    /// <param name="arguments">The CLI arguments.</param>
    /// <returns>The reference build's result, for callers that need to inspect it further.</returns>
    private static async Task<ProcessResult> AssertParityAsync(bool expectSuccess, params string[] arguments)
    {
        var (native, reference) = await RunBothAsync(arguments);

        AssertReferenceOutcome(reference, expectSuccess);
        AssertSameResult(native, reference);
        return reference;
    }

    /// <summary>
    /// Compares a diagram command twice: printed to the console, then written with <c>--output</c>.
    /// Both builds write to the same path one after the other, because the "Saved to" line on
    /// standard error names the path and Spectre wraps it at the console width, so two different
    /// paths could not be compared exactly. Both runs must produce non-empty output, so a case where
    /// both builds silently print or write nothing cannot pass as parity.
    /// </summary>
    /// <param name="arguments">The CLI arguments, without <c>--output</c>.</param>
    private async Task AssertDiagramParityAsync(params string[] arguments)
    {
        var console = await AssertParityAsync(expectSuccess: true, arguments);
        console.StandardOutput.Should().NotBeNullOrWhiteSpace("the console run must print a diagram");

        var outputPath = Path.Combine(_temp.DirectoryPath, "output", "diagram.mmd");
        string[] withOutput = [.. arguments, "--output", outputPath];

        var native = await ProcessRunner.RunAsync(SmokeEnvironment.CliNative, withOutput);
        File.Exists(outputPath).Should().BeTrue($"the native build must write the diagram; stderr:\n{native.StandardError}");
        var nativeFile = await File.ReadAllBytesAsync(outputPath);
        File.Delete(outputPath);

        var reference = await ProcessRunner.RunAsync(SmokeEnvironment.CliReference, withOutput);
        AssertReferenceOutcome(reference, expectSuccess: true);
        var referenceFile = await File.ReadAllBytesAsync(outputPath);
        referenceFile.Should().NotBeEmpty("the --output run must write a non-empty diagram file");

        AssertSameResult(native, reference);
        nativeFile.Should().Equal(referenceFile, "the native build must write the same diagram bytes");
    }

    private static async Task AssertStatsParityAsync(params string[] arguments)
    {
        var (native, reference) = await RunBothAsync(arguments);

        AssertReferenceOutcome(reference, expectSuccess: true);
        native.ExitCode.Should().Be(reference.ExitCode);
        NormalizeStats(native.StandardOutput).Should().Be(NormalizeStats(reference.StandardOutput));
        native.StandardError.Should().Be(reference.StandardError);
    }

    private static async Task<(ProcessResult Native, ProcessResult Reference)> RunBothAsync(string[] arguments)
    {
        var native = await ProcessRunner.RunAsync(SmokeEnvironment.CliNative, arguments);
        var reference = await ProcessRunner.RunAsync(SmokeEnvironment.CliReference, arguments);
        return (native, reference);
    }

    /// <summary>
    /// Pins the reference outcome, so a case where both builds fail the same way (a wrong path, a
    /// missing sample) cannot pass as parity.
    /// </summary>
    /// <param name="reference">The reference build's result.</param>
    /// <param name="expectSuccess">Whether the reference build must exit with 0.</param>
    private static void AssertReferenceOutcome(ProcessResult reference, bool expectSuccess)
    {
        if (expectSuccess)
        {
            reference.ExitCode.Should().Be(0, $"the reference build must succeed; stderr:\n{reference.StandardError}");
        }
        else
        {
            reference.ExitCode.Should().NotBe(0, "the reference build must reject this input");
        }
    }

    private static void AssertSameResult(ProcessResult native, ProcessResult reference)
    {
        native.ExitCode.Should().Be(reference.ExitCode, $"native stderr:\n{native.StandardError}");
        native.StandardOutput.Should().Be(reference.StandardOutput);
        native.StandardError.Should().Be(reference.StandardError);
    }

    /// <summary>
    /// Drops the wall-clock "Analysis time" row and collapses runs of spaces, because the timing
    /// value's width can shift the table's padding. ANSI colour escapes (Spectre's GitHub Actions
    /// enricher sets <c>Capabilities.Ansi = true</c> when <c>GITHUB_ACTIONS=true</c>, and child
    /// processes inherit that variable) are stripped only to decide which line to drop, so colour
    /// output on the other rows is still compared exactly.
    /// </summary>
    /// <param name="output">The captured standard output of <c>stats</c>.</param>
    /// <returns>The output without timing, for comparison.</returns>
    private static string NormalizeStats(string output)
    {
        return string.Join('\n', output.ReplaceLineEndings("\n")
            .Split('\n')
            .Where(line => !AnsiEscape().Replace(line, string.Empty).TrimStart()
                .StartsWith("Analysis time", StringComparison.Ordinal))
            .Select(line => RunOfSpaces().Replace(line, " ")));
    }

    [GeneratedRegex(" {2,}")]
    private static partial Regex RunOfSpaces();

    [GeneratedRegex(@"\x1B\[[0-9;?]*[A-Za-z]")]
    private static partial Regex AnsiEscape();
}
