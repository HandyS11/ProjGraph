using ProjGraph.Tests.Integration.Cli.Helpers;
using System.Text.RegularExpressions;

namespace ProjGraph.Tests.Integration.Cli;

[Collection("CLI Tests")]
public partial class MarkdownErdTests
{
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
        {
            result = app.Run(["erd", contextPath]);
        });

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
