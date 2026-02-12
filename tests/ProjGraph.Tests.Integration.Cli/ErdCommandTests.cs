using ProjGraph.Tests.Integration.Cli.Helpers;
using Spectre.Console.Cli;
using System.Text.RegularExpressions;

namespace ProjGraph.Tests.Integration.Cli;

[Collection("CLI Tests")]
public partial class ErdCommandTests
{
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
            var args = new[] { "erd", contextPath };
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
