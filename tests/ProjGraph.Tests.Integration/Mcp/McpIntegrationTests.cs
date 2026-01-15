using FluentAssertions;
using ProjGraph.Lib;
using ProjGraph.Lib.Services;
using ProjGraph.Mcp;
using System.Text.Json;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpIntegrationTests
{
    [Fact]
    public void GetProjectGraph_ShouldReturnValidJson()
    {
        // Arrange
        var graphService = new GraphService();
        var efService = new EfAnalysisService();
        var tools = new ProjGraphTools(graphService, efService);
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
