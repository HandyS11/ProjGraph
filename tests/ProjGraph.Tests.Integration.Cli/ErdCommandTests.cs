using ProjGraph.Cli.Commands;
using ProjGraph.Tests.Integration.Cli.Helpers;
using Spectre.Console.Cli;
using System.Text.RegularExpressions;

namespace ProjGraph.Tests.Integration.Cli;

[Collection("CLI Tests")]
public partial class ErdCommandTests
{
    [Fact]
    public void BuildFileChoices_SameFileNameInDifferentDirectories_MapsEachToItsOwnPath()
    {
        // Two files sharing the same file name must remain distinguishable, and each display
        // label must resolve back to its own full path — not collapse onto the first one.
        var root = Path.Combine("repo", "root");
        var first = Path.Combine(root, "ProjectA", "AppDbContext.cs");
        var second = Path.Combine(root, "ProjectB", "AppDbContext.cs");
        string[] files = [first, second];

        var choices = ErdCommand.BuildFileChoices(files, root);

        choices.Should().HaveCount(2);
        choices.Should().ContainValue(first);
        choices.Should().ContainValue(second);
        choices.Values.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void BuildFileChoices_NameWithMarkupBrackets_EscapesDisplayLabel()
    {
        // The selection prompt renders labels as Spectre markup, so a bracketed path must be
        // escaped ('[' -> '[[') to avoid being parsed as a style tag.
        var root = Path.Combine("repo", "root");
        var file = Path.Combine(root, "[archive]", "AppDbContext.cs");
        string[] files = [file];

        var choices = ErdCommand.BuildFileChoices(files, root);

        choices.Should().ContainValue(file);
        choices.Keys.Should().OnlyContain(k => k.Contains("[[", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildFileChoices_CollidingLabels_KeepsEveryFileSelectable()
    {
        // If two entries would produce the same label, each must still get a distinct key so no
        // file is silently dropped from the prompt.
        var root = Path.Combine("repo", "root");
        var file = Path.Combine(root, "App", "AppDbContext.cs");
        string[] files = [file, file];

        var choices = ErdCommand.BuildFileChoices(files, root);

        choices.Should().HaveCount(2);
        choices.Values.Should().AllBe(file);
    }

    [Fact]
    public void ErdCommand_SimpleContext_ShouldGenerateCompleteErDiagram()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var contextPath = CliTestHelpers.GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Verify file exists
        File.Exists(contextPath).Should().BeTrue($"Test file should exist at: {contextPath}");

        // Act
        var resultCode = 0;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            // Debug: verify the command and path
            var args = new[]
            {
                "erd", contextPath
            };
            resultCode = app.Run(args);
        });

        // Assert
        resultCode.Should().Be(0, $"Command should succeed. Path: {contextPath}, Output: {capturedOutput}");
        capturedOutput.Should().Contain("erDiagram", $"Path was: {contextPath}. Full output was: {capturedOutput}");
        capturedOutput.Should().Contain("Author {");
        capturedOutput.Should().Contain("Book {");
        capturedOutput.Should().Contain("Category {");
        capturedOutput.Should().Contain("Publisher {");
        capturedOutput.Should().Contain("Review {");
        capturedOutput.Should().Contain("int Id PK");
    }

    [Fact]
    public void ErdCommand_SimpleContext_WithContextName_ShouldSucceed()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var contextPath = CliTestHelpers.GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act
        var result = -1;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            result = app.Run(["erd", contextPath, "--context", "MyDbContext"]));

        result.Should().Be(0);

        // Assert
        capturedOutput.Should().Contain("erDiagram");
        capturedOutput.Should().Contain("Author");
        capturedOutput.Should().Contain("Book");
    }

    [Fact]
    public void ErdCommand_SimpleContext_ShouldNotContainIrrelevantFields()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var contextPath = CliTestHelpers.GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act
        var result = -1;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            result = app.Run(["erd", contextPath]));

        result.Should().Be(0);

        // Assert
        // Irrelevant fields should not be present in Book entity
        capturedOutput.Should().NotContain("Guid AuthorId FK");
        capturedOutput.Should().NotContain("Guid BookId FK");
        capturedOutput.Should().NotContain("Guid CategoryId FK");

        // Ensure standard fields are still there
        capturedOutput.Should().Contain("int PublisherId FK");

        // Ensure no self-referencing Book which was caused by misparsing UsingEntity
        capturedOutput.Should().NotContain("Book ||--o{ Book : \"\"");

        // Ensure junction tables and their relationships are present
        capturedOutput.Should().Contain("AuthorBook {");
        capturedOutput.Should().Contain("BookCategory {");
        capturedOutput.Should().Contain("Author ||--o{ AuthorBook : \"\"");
        capturedOutput.Should().Contain("Book ||--o{ AuthorBook : \"\"");
        capturedOutput.Should().Contain("Book ||--o{ BookCategory : \"\"");
        capturedOutput.Should().Contain("Category ||--o{ BookCategory : \"\"");
    }

    [Fact]
    public void ErdCommand_NonCsFile_ShouldFail()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath("visualize/simple-dependencies/simple-dependencies.slnx");

        // Act & Assert
        var exception = Assert.Throws<CommandRuntimeException>(() =>
            app.Run(["erd", slnxPath]));

        exception.Message.Should().Contain("Only .cs files are supported");
    }

    [Fact]
    public void ErdCommand_FileNotFound_ShouldFail()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        const string nonExistentPath = "NonExistentFile.cs";

        // Act & Assert
        var exception = Assert.Throws<CommandRuntimeException>(() =>
            app.Run(["erd", nonExistentPath]));

        exception.Message.Should().Contain("File not found");
    }

    [Fact]
    public void ErdCommand_FileWithNoContext_ShouldReturnError()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        // Create a temporary file with no DbContext
        var tempFile = Path.GetTempFileName();
        var csFile = Path.ChangeExtension(tempFile, ".cs");
        File.WriteAllText(csFile, "public class NotAContext {}");

        try
        {
            // Act
            var resultCode = 0;
            var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
                resultCode = app.Run(["erd", csFile]));

            // Assert
            resultCode.Should().Be(1);
            capturedOutput.Should().Contain("No DbContext found");
        }
        finally
        {
            if (File.Exists(csFile))
            {
                File.Delete(csFile);
            }

            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ErdCommand_SimpleContext_ReadmeOutput_ShouldMatchActualOutput()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var contextPath = CliTestHelpers.GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");
        var readmePath = CliTestHelpers.GetSamplePath(@"erd\simple-context\README.md");

        // Act
        var result = -1;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            result = app.Run(["erd", contextPath]));

        result.Should().Be(0);

        // Parse README for expected mermaid block
        var readmeContent = File.ReadAllText(readmePath);
        var expectedMermaid = ExtractMermaidBlock(readmeContent);

        // Normalize line endings for comparison
        var normalizedActual = Normalize(capturedOutput);
        var normalizedExpected = Normalize(expectedMermaid);

        // Assert
        normalizedActual.Should().Contain(normalizedExpected,
            "The generated ERD should match the example documentation in README.md");
    }

    [Fact]
    public void ErdCommand_SimpleContext_WithShowTitleFalse_ShouldOmitTitle()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var contextPath = CliTestHelpers.GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act
        var result = -1;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            result = app.Run(["erd", contextPath, "--show-title", "false"]));

        result.Should().Be(0);

        // Assert — erDiagram should still be present but no ---\ntitle: line
        capturedOutput.Should().Contain("erDiagram");
        capturedOutput.Should().NotContain("title:");
    }

    [Fact]
    public async Task ErdCommand_FileOutput_ShouldSaveToDisk()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var contextPath = CliTestHelpers.GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");
        var outputPath = Path.Combine(Path.GetTempPath(), "erd_" + Guid.NewGuid() + ".md");

        try
        {
            // Act
            var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            {
                var result = app.Run(["erd", contextPath, "--output", outputPath]);
                result.Should().Be(0);
            });

            // Assert
            capturedOutput.Should().Contain($"Saved to {outputPath}");
            capturedOutput.Should().NotContain("erDiagram");

            File.Exists(outputPath).Should().BeTrue();
            var fileContent = await File.ReadAllTextAsync(outputPath);
            fileContent.Should().Contain("```mermaid");
            fileContent.Should().Contain("erDiagram");
            fileContent.Should().Contain("Author {");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void ErdCommand_WithOwnedModeClassic_ChainedOwnedContext_RendersOwnedTypeAsSeparateEntity()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var contextPath = CliTestHelpers.GetRootPath(
            Path.Combine("tests", "ProjGraph.Tests.Unit.EntityFramework", "Golden", "fixtures", "ChainedOwnedContext.cs"));

        // Act
        var result = -1;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            result = app.Run(["erd", contextPath, "--owned-mode", "classic", "--show-title", "false"]));

        // Assert
        result.Should().Be(0);
        capturedOutput.Should().Contain("PostalAddress {");
        capturedOutput.Should().Contain("Shopper ||--|| PostalAddress");
    }

    [Fact]
    public void ErdCommand_DefaultOwnedMode_OwnedModesContext_InlinesTableSplitOwnedTypeOntoOwner()
    {
        // Arrange — no --owned-mode flag; must default to mirror (MirrorEf), which inlines the
        // table-split ShipTo owned type onto OwnedModesInvoice using EF's Nav_Property naming
        // rather than drawing it as its own entity box.
        var app = CliTestHelpers.CreateApp();
        var contextPath = CliTestHelpers.GetRootPath(
            Path.Combine("tests", "ProjGraph.Tests.Unit.EntityFramework", "Golden", "fixtures", "OwnedModesContext.cs"));

        // Act
        var result = -1;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            result = app.Run(["erd", contextPath, "--show-title", "false"]));

        // Assert
        result.Should().Be(0);
        capturedOutput.Should().Contain("ShipTo_Street");
        capturedOutput.Should().NotContain("OwnedModesInvoice_ShipTo {");
    }

    [Fact]
    public void ErdCommand_WithOwnedModeClassic_OwnedModesContext_GivesTableSplitOwnedTypeItsOwnEntity()
    {
        // Arrange — same fixture as the mirror-mode test above, but with --owned-mode classic.
        // Proves the flag actually changes output (rather than merely being accepted): the
        // table-split ShipTo owned type moves from an inlined column prefix to its own entity box
        // with an identifying relationship, and the prefixed column disappears.
        var app = CliTestHelpers.CreateApp();
        var contextPath = CliTestHelpers.GetRootPath(
            Path.Combine("tests", "ProjGraph.Tests.Unit.EntityFramework", "Golden", "fixtures", "OwnedModesContext.cs"));

        // Act
        var result = -1;
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            result = app.Run(["erd", contextPath, "--owned-mode", "classic", "--show-title", "false"]));

        // Assert
        result.Should().Be(0);
        capturedOutput.Should().Contain("OwnedModesInvoice_ShipTo {");
        capturedOutput.Should().Contain("OwnedModesInvoice ||--|| OwnedModesInvoice_ShipTo : \"ShipTo\"");
        capturedOutput.Should().NotContain("ShipTo_Street");
    }

    [Fact]
    public void ErdCommand_WithInvalidOwnedMode_ShouldFail()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var contextPath = CliTestHelpers.GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act & Assert
        var exception = Assert.Throws<CommandRuntimeException>(() =>
            app.Run(["erd", contextPath, "--owned-mode", "bogus"]));

        exception.Message.Should().Contain("Invalid --owned-mode");
    }

    private static string ExtractMermaidBlock(string content)
    {
        var match = ExtractMermaidRegex().Match(content);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string Normalize(string input)
    {
        return NormalizeRegex().Replace(input, "\n").Trim();
    }

    [GeneratedRegex(@"```mermaid\s+([\s\S]*?)\s+```")]
    private static partial Regex ExtractMermaidRegex();

    [GeneratedRegex(@"\r\n|\n|\r")]
    private static partial Regex NormalizeRegex();
}
