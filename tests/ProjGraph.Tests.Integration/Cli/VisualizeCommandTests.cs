using FluentAssertions;
using ProjGraph.Tests.Integration.Helpers;
using Spectre.Console.Cli;

namespace ProjGraph.Tests.Integration.Cli;

[Collection("CLI Tests")]
public class VisualizeCommandTests
{
    [Fact]
    public void VisualizeCommand_SimpleDependencies_Slnx_Mermaid_ShouldGenerateValidGraph()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", slnxPath, "--format", "mermaid"]);
            result.Should().Be(0);
        });

        // Assert
        capturedOutput.Should().Contain("graph TD");
        capturedOutput.Should().Contain("A[\"A\"]");
        capturedOutput.Should().Contain("B[\"B\"]");
        capturedOutput.Should().Contain("C[\"C\"]");
        capturedOutput.Should().Contain("D[\"D\"]");
        capturedOutput.Should().Contain("A --> B");
        capturedOutput.Should().Contain("B --> C");
        capturedOutput.Should().Contain("B --> D");
    }

    [Fact]
    public void VisualizeCommand_SimpleDependencies_SingleProject_Mermaid_ShouldDiscoverAllDependencies()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var projPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\A\A.csproj");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", projPath, "--format", "mermaid"]);
            result.Should().Be(0);
        });

        // Assert
        capturedOutput.Should().Contain("graph TD");
        capturedOutput.Should().Contain("A[\"A\"]");
        capturedOutput.Should().Contain("B[\"B\"]");
        capturedOutput.Should().Contain("C[\"C\"]");
        capturedOutput.Should().Contain("D[\"D\"]");
        capturedOutput.Should().Contain("A --> B");
    }

    [Fact]
    public void VisualizeCommand_ProjGraphSolution_Slnx_Mermaid_ShouldShowAllProjects()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetRootPath("ProjGraph.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", slnxPath, "--format", "mermaid"]);
            result.Should().Be(0);
        });

        // Assert
        capturedOutput.Should().Contain("graph TD");
        capturedOutput.Should().Contain("ProjGraph_Cli");
        capturedOutput.Should().Contain("ProjGraph_Core");
        capturedOutput.Should().Contain("ProjGraph_Lib");
        capturedOutput.Should().Contain("ProjGraph_Mcp");
    }

    [Fact]
    public void VisualizeCommand_SimpleDependencies_TreeFormat_ShouldShowHierarchy()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", slnxPath, "--format", "tree"]);
            result.Should().Be(0);
        });

        // Assert
        capturedOutput.Should().Contain("Projects");
        capturedOutput.Should().Contain("📦 A");
        capturedOutput.Should().Contain("→ B");
        capturedOutput.Should().Contain("📦 B");
        capturedOutput.Should().Contain("→ C");
        capturedOutput.Should().Contain("→ D");
        capturedOutput.Should().Contain("📦 C");
        capturedOutput.Should().Contain("📦 D");
    }

    [Fact]
    public void VisualizeCommand_DefaultFormat_ShouldUseTree()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", slnxPath]);
            result.Should().Be(0);
        });

        // Assert - tree format is default
        capturedOutput.Should().Contain("Projects");
        capturedOutput.Should().Contain("📦");
    }

    [Fact]
    public void VisualizeCommand_NonExistentFile_ShouldFail()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "this", "path", "does", "not", "exist.slnx");

        // Act & Assert
        var exception = Assert.Throws<CommandRuntimeException>(() =>
            app.Run(["visualize", nonExistentPath]));

        exception.Message.Should().Contain("File not found");
    }

    [Fact]
    public void VisualizeCommand_InvalidFormat_ShouldFail()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath("visualize/simple-dependencies/simple-dependencies.slnx");

        // Act & Assert
        var exception = Assert.Throws<CommandRuntimeException>(() =>
            app.Run(["visualize", slnxPath, "--format", "invalid"]));

        exception.Message.Should().Contain("Format must be");
    }
}