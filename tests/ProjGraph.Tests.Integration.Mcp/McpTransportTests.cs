using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ProjGraph.Tests.Shared.Helpers;

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
    /// <summary>
    /// Connects a client to the server apphost in ProjGraph.Mcp's own build output (guaranteed
    /// up to date by the ProjectReference). ProjGraph.Mcp is a self-contained exe, so its build
    /// lands in a RID subdirectory and must be launched via its apphost — the DLL that the
    /// ProjectReference copies into the test output has no runtime next to it and cannot start.
    /// The client deliberately advertises no capabilities — in particular no workspace roots.
    /// </summary>
    private static async Task<McpClient> ConnectAsync()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ProjGraph e2e",
            Command = LocateServerExecutable()
        });

        return await McpClient.CreateAsync(transport);
    }

    private static string LocateServerExecutable()
    {
        // .../tests/ProjGraph.Tests.Integration.Mcp/bin/{Configuration}/{tfm}/
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var binRoot = TestPathHelper.GetRootPath(Path.Combine(
            "src", "ProjGraph.Mcp", "bin", testOutput.Parent!.Name, testOutput.Name));
        Directory.Exists(binRoot).Should().BeTrue(
            $"the MCP server build output must exist at {binRoot}");
        var exeName = OperatingSystem.IsWindows() ? "ProjGraph.Mcp.exe" : "ProjGraph.Mcp";

        // The build RID matches the machine that built it, so probing the RID subdirectories
        // is exact enough without reconstructing the RID by hand. Preferring the most recently
        // written apphost keeps a dev machine with stale cross-RID leftovers deterministic.
        var serverExe = Directory.GetDirectories(binRoot)
            .Select(ridDir => new FileInfo(Path.Combine(ridDir, exeName)))
            .Where(apphost => apphost.Exists)
            .OrderByDescending(apphost => apphost.LastWriteTimeUtc)
            .FirstOrDefault();

        serverExe.Should().NotBeNull($"the MCP server apphost must be present under {binRoot}");
        return serverExe.FullName;
    }

    private static string JoinText(CallToolResult result)
    {
        return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
    }

    [Fact]
    public async Task ListTools_OverRealStdioTransport_ExposesAllFourTools()
    {
        await using var client = await ConnectAsync();

        var tools = await client.ListToolsAsync();

        tools.Select(tool => tool.Name).Should().BeEquivalentTo(
            "get_class_diagram", "get_project_graph", "get_project_stats", "get_erd");
    }

    [Fact]
    public async Task GetErd_RelativePathWithoutRootsCapability_SurfacesAbsolutePathGuidance()
    {
        await using var client = await ConnectAsync();

        var result = await client.CallToolAsync(
            "get_erd",
            new Dictionary<string, object?> { ["path"] = "SomeDbContext.cs" });

        result.IsError.Should().BeTrue("a relative path cannot be resolved without workspace roots");

        // The audit's High scenario: WorkspaceRootService's guidance must survive the SDK
        // boundary instead of being stripped to "An error occurred invoking 'get_erd'".
        JoinText(result).Should().Contain("absolute path");
    }
}
