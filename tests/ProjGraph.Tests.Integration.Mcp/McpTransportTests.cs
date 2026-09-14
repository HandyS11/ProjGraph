using ModelContextProtocol.Protocol;
using ProjGraph.Tests.Integration.Mcp.Helpers;

namespace ProjGraph.Tests.Integration.Mcp;

/// <summary>
/// True end-to-end tests that launch the real MCP server executable and talk to it over the
/// stdio transport with a real <see cref="McpClient"/>. Unlike the hand-wired tests (which
/// construct <see cref="ProjGraph.Mcp.ProjGraphTools"/> directly), these cross the SDK's
/// tool-invocation boundary — the layer that replaces the message of any non-McpException
/// with a generic "An error occurred invoking …", which no in-process test can observe.
/// </summary>
public sealed class McpTransportTests
{
    private static string JoinText(CallToolResult result)
    {
        return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
    }

    [Fact]
    public async Task ListTools_OverRealStdioTransport_ExposesAllFourTools()
    {
        await using var client = await McpServerProcess.ConnectAsync();

        var tools = await client.ListToolsAsync();

        tools.Select(tool => tool.Name).Should().BeEquivalentTo(
            "get_class_diagram", "get_project_graph", "get_project_stats", "get_erd");
    }

    [Fact]
    public async Task GetErd_RelativePathWithoutRootsCapability_SurfacesAbsolutePathGuidance()
    {
        await using var client = await McpServerProcess.ConnectAsync();

        var result = await client.CallToolAsync(
            "get_erd",
            new Dictionary<string, object?> { ["path"] = "SomeDbContext.cs" });

        result.IsError.Should().BeTrue("a relative path cannot be resolved without workspace roots");

        // The audit's High scenario: WorkspaceRootService's guidance must survive the SDK
        // boundary instead of being stripped to "An error occurred invoking 'get_erd'".
        JoinText(result).Should().Contain("absolute path");
    }
}
