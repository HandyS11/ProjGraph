using FluentAssertions;
using ProjGraph.Cli;
using System.CommandLine;
using System.CommandLine.IO;

namespace ProjGraph.Tests.Integration.Cli;

public class CliIntegrationTests
{
    [Fact]
    public async Task VisualizeCommand_WithSlnx_ShouldProduceOutput()
    {
        // Arrange
        var console = new TestConsole();
        var rootCommand = Program.CreateRootCommand();
        var slnxPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "ProjGraph.slnx");
        slnxPath = Path.GetFullPath(slnxPath);

        // Act
        var result = await rootCommand.InvokeAsync($"visualize {slnxPath} --format mermaid", console);

        // Assert
        result.Should().Be(0);
        var output = console.Out.ToString();
        output.Should().Contain("graph TD");
        output.Should().Contain("ProjGraph_Cli");
        output.Should().Contain("ProjGraph_Core");
    }

    [Fact]
    public async Task VisualizeCommand_WithSln_ShouldProduceOutput()
    {
        // Arrange
        var console = new TestConsole();
        var rootCommand = Program.CreateRootCommand();
        var slnPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "ProjGraph.sln");
        slnPath = Path.GetFullPath(slnPath);

        // Act
        var result = await rootCommand.InvokeAsync($"visualize {slnPath} --format mermaid", console);

        // Assert
        result.Should().Be(0);
        var output = console.Out.ToString();
        output.Should().Contain("graph TD");
        output.Should().Contain("ProjGraph_Cli");
        output.Should().Contain("ProjGraph_Core");
    }
}
