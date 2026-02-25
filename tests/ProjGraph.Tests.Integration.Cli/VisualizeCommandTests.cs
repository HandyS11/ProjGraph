using ProjGraph.Tests.Integration.Cli.Helpers;
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
    public void VisualizeCommand_SimpleDependencies_FlatFormat_ShouldShowList()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", slnxPath, "--format", "flat"]);
            result.Should().Be(0);
        });

        // Assert
        capturedOutput.Should().Contain("Projects");
        capturedOutput.Should().Contain("A");
        capturedOutput.Should().Contain("B");
        capturedOutput.Should().Contain("→ B");
    }

    [Fact]
    public void VisualizeCommand_WithIncludePackages_ShouldShowPackages()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var projPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\A\A.csproj");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", projPath, "--format", "mermaid", "--include-packages"]);
            result.Should().Be(0);
        });

        // Assert
        capturedOutput.Should().Contain("Spectre_Console");
        capturedOutput.Should().Contain("Microsoft_Extensions_DependencyInjection");
        capturedOutput.Should().Contain("Microsoft_Extensions_Logging_Abstractions");
        capturedOutput.Should().Contain("A -.-> Spectre_Console");
        capturedOutput.Should().Contain("A -.-> Microsoft_Extensions_DependencyInjection");
        capturedOutput.Should().Contain("A -.-> Microsoft_Extensions_Logging_Abstractions");
    }

    [Fact]
    public async Task VisualizeCommand_FileOutput_ShouldSaveToDiskAndWrapInFence()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");
        var outputPath = Path.Combine(Path.GetTempPath(), "visualize_" + Guid.NewGuid() + ".md");

        try
        {
            // Act
            var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
            {
                var result = app.Run(["visualize", slnxPath, "--output", outputPath]);
                result.Should().Be(0);
            });

            // Assert
            capturedOutput.Should().Contain($"Saved to {outputPath}");
            capturedOutput.Should().NotContain("graph TD");

            File.Exists(outputPath).Should().BeTrue();
            var fileContent = await File.ReadAllTextAsync(outputPath);
            fileContent.Should().Contain("```mermaid");
            fileContent.Should().Contain("graph TD");
            fileContent.Should().Contain("A --> B");
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
    public async Task VisualizeCommand_FileOutput_MmdExtension_ShouldNotWrapInFence()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");
        var outputPath = Path.Combine(Path.GetTempPath(), "visualize_" + Guid.NewGuid() + ".mmd");

        try
        {
            // Act
            CliTestHelpers.CaptureConsoleOutput(() =>
            {
                var result = app.Run(["visualize", slnxPath, "--output", outputPath]);
                result.Should().Be(0);
            });

            // Assert
            File.Exists(outputPath).Should().BeTrue();
            var fileContent = await File.ReadAllTextAsync(outputPath);
            fileContent.Should().NotContain("```mermaid");
            fileContent.Should().Contain("graph TD");
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
        capturedOutput.Should().Contain("A");
        capturedOutput.Should().Contain("B");
        capturedOutput.Should().Contain("C");
        capturedOutput.Should().Contain("D");
    }

    [Fact]
    public void VisualizeCommand_DefaultFormat_ShouldUseMermaid()
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

        // Assert - mermaid format is default
        capturedOutput.Should().Contain("graph TD");
    }

    [Fact]
    public void VisualizeCommand_NonExistentFile_ShouldFail()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "this", "path", "does", "not", "exist.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", nonExistentPath]);
            result.Should().Be(1);
        });

        // Assert
        capturedOutput.Should().Contain("File not found");
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

    [Fact]
    public void VisualizeCommand_SimpleDependencies_Mermaid_WithShowTitleFalse_ShouldOmitTitle()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", slnxPath, "--format", "mermaid", "--show-title", "false"]);
            result.Should().Be(0);
        });

        // Assert — mermaid content without title block
        capturedOutput.Should().Contain("graph TD");
        capturedOutput.Should().NotContain("title:");
    }

    [Fact]
    public void VisualizeCommand_SimpleDependencies_TreeFormat_ShouldContainAllProjects()
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

        // Assert — all four projects should appear with dependencies shown
        capturedOutput.Should().Contain("A");
        capturedOutput.Should().Contain("B");
        capturedOutput.Should().Contain("C");
        capturedOutput.Should().Contain("D");
        // Tree format should NOT contain mermaid markers
        capturedOutput.Should().NotContain("graph TD");
    }

    [Fact]
    public void VisualizeCommand_SingleCsproj_FlatFormat_ShouldShowProject()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var projPath = CliTestHelpers.GetSamplePath(@"visualize\simple-dependencies\A\A.csproj");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", projPath, "--format", "flat"]);
            result.Should().Be(0);
        });

        // Assert
        capturedOutput.Should().Contain("A");
    }

    [Fact]
    public void VisualizeCommand_ProjGraphSolution_FlatFormat_ShouldShowAllProjects()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetRootPath("ProjGraph.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", slnxPath, "--format", "flat"]);
            result.Should().Be(0);
        });

        // Assert
        capturedOutput.Should().Contain("Projects");
        capturedOutput.Should().Contain("ProjGraph");
    }

    [Fact]
    public void VisualizeCommand_ProjGraphSolution_TreeFormat_ShouldSucceed()
    {
        // Arrange
        var app = CliTestHelpers.CreateApp();
        var slnxPath = CliTestHelpers.GetRootPath("ProjGraph.slnx");

        // Act
        var capturedOutput = CliTestHelpers.CaptureConsoleOutput(() =>
        {
            var result = app.Run(["visualize", slnxPath, "--format", "tree"]);
            result.Should().Be(0);
        });

        // Assert
        capturedOutput.Should().Contain("ProjGraph");
    }
}
