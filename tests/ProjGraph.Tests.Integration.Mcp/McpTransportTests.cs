using ModelContextProtocol.Protocol;
using ProjGraph.Tests.Integration.Mcp.Helpers;
using ProjGraph.Tests.Shared.Helpers;
using System.Text.Json.Nodes;

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

    [Fact]
    public async Task GetProjectStats_SolutionWithMalformedProject_ReturnsStatsWithWarnings()
    {
        using var temp = new TestDirectory();
        temp.CreateFile(Path.Combine("Good", "Good.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        temp.CreateFile(Path.Combine("Bad", "Bad.csproj"), "<Project><PropertyGroup></Project>");
        var slnxPath = temp.CreateFile("sol.slnx",
            "<Solution><Project Path=\"Good/Good.csproj\" /><Project Path=\"Bad/Bad.csproj\" /></Solution>");
        await using var client = await McpServerProcess.ConnectAsync();

        var result = await client.CallToolAsync(
            "get_project_stats",
            new Dictionary<string, object?> { ["path"] = slnxPath });

        // The real server runs with reflection-based JSON disabled, which the in-process
        // McpWarningsTests cannot observe: attaching the warnings must not depend on it.
        result.IsError.Should().NotBeTrue(JoinText(result));
        var warnings = JsonNode.Parse(JoinText(result))?["warnings"]?.AsArray();
        warnings.Should().NotBeNullOrEmpty();
    }
}
