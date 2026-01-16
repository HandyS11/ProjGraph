using FluentAssertions;
using ProjGraph.Lib.Services;
using ProjGraph.Mcp;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpIntegrationTests
{
    [Fact]
    public void GetProjectGraph_ShouldReturnValidMermaidDiagram()
    {
        // Arrange
        var graphService = new GraphService();
        var efService = new EfAnalysisService();
        var tools = new ProjGraphTools(graphService, efService);
        var slnxPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "ProjGraph.slnx");
        slnxPath = Path.GetFullPath(slnxPath);

        // Act
        var result = tools.GetProjectGraph(slnxPath);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().StartWith("```mermaid");
        result.Should().Contain("graph TD");
        result.Trim().Should().EndWith("```");
    }
}
