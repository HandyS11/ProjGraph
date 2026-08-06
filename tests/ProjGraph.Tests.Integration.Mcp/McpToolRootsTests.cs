using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Mcp;
using ProjGraph.Tests.Integration.Mcp.Helpers;
using ProjGraph.Tests.Shared.Helpers;
using System.Reflection;

namespace ProjGraph.Tests.Integration.Mcp;

/// <summary>
/// Drives the tools the way a real client does — an actual <c>tools/call</c> over the wire — so the
/// request-scoped <see cref="McpServer"/> is the one the SDK binds, not a root instance a test
/// handed in. This is what distinguishes protocol revision 2026-07-28 (client capabilities declared
/// per request in <c>_meta</c>, visible only on the request-scoped server) from the earlier
/// <c>initialize</c> handshake that <c>WorkspaceRootServiceTests</c> pins itself to.
/// </summary>
public sealed class McpToolRootsTests : IDisposable
{
    private readonly TestDirectory _temp = new();

    /// <summary>
    /// Builds server options exposing the real ProjGraph tools, so a <c>tools/call</c> reaches the
    /// production code path including workspace-root resolution.
    /// </summary>
    /// <param name="rootService">
    /// The roots service to wire in, when a test needs to inspect it afterwards.
    /// </param>
    /// <returns>Server options whose tool collection is backed by the real tools.</returns>
    private static McpServerOptions CreateServerOptionsWithTools(WorkspaceRootService? rootService = null)
    {
        var tools = McpTestHelper.CreateTools(new CollectingOutputConsole(), rootService: rootService);
        var options = InProcessMcpSession.CreateServerOptions();

        options.ToolCollection =
        [
            McpServerTool.Create(tools.GetProjectGraphAsync),
            McpServerTool.Create(tools.GetProjectStatsAsync)
        ];

        return options;
    }

    [Fact]
    public async Task CallTool_RelativePath_OnCurrentProtocol_ShouldResolveAgainstTheClientRoots()
    {
        // A minimal but real solution file, so resolution is the only thing under test.
        _temp.CreateFile("App.slnx", "<Solution></Solution>");

        await using var session = await InProcessMcpSession.StartAsync(
            CreateServerOptionsWithTools(),
            // Not pinned: the client negotiates the latest revision (2026-07-28), which drops the
            // initialize handshake. This is the case that silently regressed on the v2 upgrade.
            InProcessMcpSession.CreateClientOptionsWithRoots(() => [_temp.DirectoryPath], pinDownLevel: false));

        var result = await session.Client.CallToolAsync(
            "get_project_graph",
            new Dictionary<string, object?> { ["path"] = "App.slnx" });

        result.IsError.Should().NotBeTrue(
            "a relative path must resolve against the client's workspace roots on the current protocol revision");
    }

    [Fact]
    public async Task CallTool_RelativePath_WithoutRootsCapability_ShouldAskForAnAbsolutePath()
    {
        await using var session = await InProcessMcpSession.StartAsync(
            CreateServerOptionsWithTools(),
            InProcessMcpSession.CreateClientOptionsWithoutRoots());

        var result = await session.Client.CallToolAsync(
            "get_project_graph",
            new Dictionary<string, object?> { ["path"] = "App.slnx" });

        result.IsError.Should().BeTrue();
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        text.Should().Contain("absolute path",
            "the guidance must survive the SDK's tool boundary instead of being replaced by a generic error");
    }

    [Fact]
    public async Task CallTool_OnCurrentProtocol_ShouldNotServeStaleRootsAcrossRequests()
    {
        using var secondRoot = new TestDirectory();
        secondRoot.CreateFile("Moved.slnx", "<Solution></Solution>");
        _temp.CreateFile("App.slnx", "<Solution></Solution>");

        var currentRoots = new List<string> { _temp.DirectoryPath };
        await using var session = await InProcessMcpSession.StartAsync(
            CreateServerOptionsWithTools(),
            InProcessMcpSession.CreateClientOptionsWithRoots(
                () => [.. Volatile.Read(ref currentRoots)], pinDownLevel: false));

        var first = await session.Client.CallToolAsync(
            "get_project_graph",
            new Dictionary<string, object?> { ["path"] = "App.slnx" });
        first.IsError.Should().NotBeTrue();

        // The workspace moves. On 2026-07-28 there is no session for roots/list_changed to
        // invalidate, so the roots must be re-fetched per request rather than cached.
        Volatile.Write(ref currentRoots, [secondRoot.DirectoryPath]);

        var second = await session.Client.CallToolAsync(
            "get_project_graph",
            new Dictionary<string, object?> { ["path"] = "Moved.slnx" });

        second.IsError.Should().NotBeTrue(
            "the roots of the current request must be used, not those cached from an earlier one");
    }

    [Fact]
    public async Task CallTool_OnCurrentProtocol_ShouldNotPublishTheRootsToTheSharedCache()
    {
        _temp.CreateFile("App.slnx", "<Solution></Solution>");

        await using var rootService = new WorkspaceRootService(new PhysicalFileSystem());
        await using var session = await InProcessMcpSession.StartAsync(
            CreateServerOptionsWithTools(rootService),
            InProcessMcpSession.CreateClientOptionsWithRoots(() => [_temp.DirectoryPath], pinDownLevel: false));

        var result = await session.Client.CallToolAsync(
            "get_project_graph",
            new Dictionary<string, object?> { ["path"] = "App.slnx" });
        result.IsError.Should().NotBeTrue();

        // The roots of a per-request revision belong to the request that fetched them. Leaving them
        // in the singleton's fields is what would let an overlapping request resolve its own path
        // against them, so the shared cache must still be untouched.
        var type = typeof(WorkspaceRootService);
        var rootPaths = type.GetField("_rootPaths", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(rootService);
        var status = type.GetField("_status", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(rootService);

        rootPaths.Should().BeAssignableTo<IReadOnlyList<string>>()
            .Which.Should().BeEmpty("the request's roots must not be published to the shared cache");
        // RootsStatusKind.Unknown = 0 (private enum inside WorkspaceRootService)
        ((int)status!).Should().Be(0, "the shared status must stay untouched on the per-request revision");
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
