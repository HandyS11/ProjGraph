using System.Text.Json;
using FluentAssertions;
using ProjGraph.Lib;
using ProjGraph.Mcp;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpIntegrationTests
{
    [Fact]
    public void GetProjectGraph_ShouldReturnValidJson()
    {
        // Arrange
        var graphService = new GraphService();
        var tools = new ProjGraphTools(graphService);
        var slnxPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "ProjGraph.slnx");
        slnxPath = Path.GetFullPath(slnxPath);

        // Act
        var resultJson = tools.GetProjectGraph(slnxPath);

        // Assert
        resultJson.Should().NotStartWith("Error");
        var doc = JsonDocument.Parse(resultJson);
        doc.RootElement.TryGetProperty("nodes", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("edges", out _).Should().BeTrue();

        var nodes = doc.RootElement.GetProperty("nodes");
        nodes.GetArrayLength().Should().BeGreaterThan(0);
    }
}
