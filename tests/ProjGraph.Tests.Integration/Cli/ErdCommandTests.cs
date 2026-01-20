using FluentAssertions;
using Spectre.Console.Cli;

namespace ProjGraph.Tests.Integration.Cli;

[Collection("CLI Tests")]
public class ErdCommandTests
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
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["erd", contextPath, "--context", "MyDbContext"]);
            result.Should().Be(0);
        });

        // Assert
        capturedOutput.Should().Contain("erDiagram");
        capturedOutput.Should().Contain("Author");
        capturedOutput.Should().Contain("Book");
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
}