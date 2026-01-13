using System.CommandLine;
using System.CommandLine.IO;
using FluentAssertions;
using ProjGraph.Cli;

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
}
