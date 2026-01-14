using FluentAssertions;
using ProjGraph.Cli.Commands;
using Spectre.Console.Cli;
using System.Text;

namespace ProjGraph.Tests.Integration.Cli;

public class CliIntegrationTests
{
    [Fact]
    public void VisualizeCommand_WithSlnx_ShouldProduceOutput()
    {
        // Arrange
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddCommand<VisualizeCommand>("visualize");
        });

        var slnxPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "..",
            "..",
            "..",
            "..",
            "ProjGraph.slnx");
        slnxPath = Path.GetFullPath(slnxPath);

        var output = new StringBuilder();
        var originalOut = Console.Out;

        try
        {
            // Capture console output
            using var writer = new StringWriter(output);
            Console.SetOut(writer);

            // Act
            var result = app.Run(["visualize", slnxPath, "--format", "mermaid"]);

            // Assert
            result.Should().Be(0);
            var capturedOutput = output.ToString();
            capturedOutput.Should().Contain("graph TD");
            capturedOutput.Should().Contain("ProjGraph_Cli");
            capturedOutput.Should().Contain("ProjGraph_Core");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void VisualizeCommand_WithSln_ShouldProduceOutput()
    {
        // Arrange
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddCommand<VisualizeCommand>("visualize");
        });

        var slnPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "..",
            "..",
            "..",
            "..",
            "ProjGraph.sln");
        slnPath = Path.GetFullPath(slnPath);

        var output = new StringBuilder();
        var originalOut = Console.Out;

        try
        {
            // Capture console output
            using var writer = new StringWriter(output);
            Console.SetOut(writer);

            // Act
            var result = app.Run(["visualize", slnPath, "--format", "mermaid"]);

            // Assert
            result.Should().Be(0);
            var capturedOutput = output.ToString();
            capturedOutput.Should().Contain("graph TD");
            capturedOutput.Should().Contain("ProjGraph_Cli");
            capturedOutput.Should().Contain("ProjGraph_Core");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
