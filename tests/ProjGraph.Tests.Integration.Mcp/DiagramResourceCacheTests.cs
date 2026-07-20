using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ProjGraph.Mcp;
using ProjGraph.Tests.Integration.Mcp.Helpers;
using System.Reflection;

namespace ProjGraph.Tests.Integration.Mcp;

/// <summary>
/// Covers the half of <see cref="DiagramResourceCache"/> that only runs when a live
/// <see cref="ModelContextProtocol.Server.McpServer"/> is attached: publishing each cached diagram
/// as a real MCP resource, un-publishing it on eviction, and notifying the client when the content
/// behind an unchanged URI is regenerated. <c>McpResourcesTests</c> covers the server-less paths.
/// </summary>
public sealed class DiagramResourceCacheTests
{
    private const string SolutionPath = "/src/App.slnx";

    private static string UriFor(string type, string sourcePath)
    {
        return $"projgraph://diagrams/{type}/{Uri.EscapeDataString(sourcePath)}";
    }

    [Fact]
    public async Task StoreAsync_WithServer_ShouldPublishTheDiagramAsAnMcpResource()
    {
        await using var session = await InProcessMcpSession.StartAsync();
        var cache = new DiagramResourceCache();

        await cache.StoreAsync("graph", SolutionPath, "text/plain", "graph TD\n  A --> B",
            "Dependency graph for App.slnx", session.Server, CancellationToken.None);

        var resources = await session.Client.ListResourcesAsync();

        // Without a server the entry is cache-only; with one it must show up in resources/list so
        // the client can discover the diagram without re-running the tool.
        var published = resources.Should().ContainSingle().Subject;
        published.Uri.Should().Be(UriFor("graph", SolutionPath));
        published.Name.Should().Be("graph — App.slnx");
        published.Description.Should().Be("Dependency graph for App.slnx");
        published.MimeType.Should().Be("text/plain");
    }

    [Fact]
    public async Task StoreAsync_WithServer_ShouldServeTheContentThroughResourcesRead()
    {
        await using var session = await InProcessMcpSession.StartAsync();
        var cache = new DiagramResourceCache();

        await cache.StoreAsync("class", "/src/Models/Order.cs", "text/plain",
            "classDiagram\n  class Order", "Class diagram", session.Server, CancellationToken.None);

        var published = (await session.Client.ListResourcesAsync())
            .Single(resource => resource.Uri == UriFor("class", "/src/Models/Order.cs"));
        var result = await published.ReadAsync();

        var contents = result.Contents.Should().ContainSingle().Subject
            .Should().BeOfType<TextResourceContents>().Subject;
        contents.Text.Should().Be("classDiagram\n  class Order");
        contents.MimeType.Should().Be("text/plain");
    }

    [Fact]
    public async Task StoreAsync_WithServer_UpdatingAnExistingEntry_ShouldServeTheNewContent()
    {
        await using var session = await InProcessMcpSession.StartAsync();
        var cache = new DiagramResourceCache();

        await cache.StoreAsync("graph", SolutionPath, "text/plain", "v1", "desc",
            session.Server, CancellationToken.None);
        await cache.StoreAsync("graph", SolutionPath, "text/plain", "v2", "desc",
            session.Server, CancellationToken.None);

        // The URI is stable across regenerations, so the client must not end up with two entries.
        var published = (await session.Client.ListResourcesAsync()).Should().ContainSingle().Subject;

        var result = await published.ReadAsync();
        result.Contents.Should().ContainSingle().Subject
            .Should().BeOfType<TextResourceContents>().Subject
            .Text.Should().Be("v2");
    }

    [Fact]
    public async Task StoreAsync_WithServer_UpdatingAnExistingEntry_ShouldNotifyTheClient()
    {
        await using var session = await InProcessMcpSession.StartAsync();
        var cache = new DiagramResourceCache();

        var notified = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = session.Client.RegisterNotificationHandler(
            NotificationMethods.ResourceUpdatedNotification,
            (notification, _) =>
            {
                notified.TrySetResult(notification.Params?.ToString() ?? string.Empty);
                return default;
            });

        await cache.StoreAsync("graph", SolutionPath, "text/plain", "v1", "desc",
            session.Server, CancellationToken.None);
        await cache.StoreAsync("graph", SolutionPath, "text/plain", "v2", "desc",
            session.Server, CancellationToken.None);

        // Adding a resource raises resources/list_changed on its own, but a regeneration keeps the
        // same URI — only an explicit resources/updated tells the client the content is stale.
        var payload = await notified.Task.WaitAsync(TimeSpan.FromSeconds(30));
        payload.Should().Contain(UriFor("graph", SolutionPath));
    }

    [Fact]
    public async Task StoreAsync_WithServer_AtCapacity_ShouldUnpublishTheEvictedResource()
    {
        await using var session = await InProcessMcpSession.StartAsync();
        var cache = new DiagramResourceCache();

        for (var i = 0; i < DiagramResourceCache.MaxCachedResources; i++)
        {
            await cache.StoreAsync("graph", $"/src/file{i}.slnx", "text/plain", $"content{i}",
                $"desc{i}", session.Server, CancellationToken.None);
        }

        await cache.StoreAsync("graph", "/src/newest.slnx", "text/plain", "newest", "desc",
            session.Server, CancellationToken.None);

        var uris = (await session.Client.ListResourcesAsync()).Select(resource => resource.Uri).ToList();

        // Eviction must reach the server's resource collection too, otherwise resources/list would
        // keep advertising diagrams the cache can no longer serve.
        uris.Should().HaveCount(DiagramResourceCache.MaxCachedResources);
        uris.Should().NotContain(UriFor("graph", "/src/file0.slnx"));
        uris.Should().Contain(UriFor("graph", "/src/newest.slnx"));
    }

    [Fact]
    public async Task StoreAsync_WithServer_DifferentTypesForOnePath_ShouldPublishBoth()
    {
        await using var session = await InProcessMcpSession.StartAsync();
        var cache = new DiagramResourceCache();

        await cache.StoreAsync("graph", SolutionPath, "text/plain", "graph content", "Graph",
            session.Server, CancellationToken.None);
        await cache.StoreAsync("class", SolutionPath, "text/plain", "class content", "Class",
            session.Server, CancellationToken.None);

        var uris = (await session.Client.ListResourcesAsync()).Select(resource => resource.Uri).ToList();

        uris.Should().HaveCount(2);
        uris.Should().Contain(UriFor("graph", SolutionPath));
        uris.Should().Contain(UriFor("class", SolutionPath));
    }

    [Fact]
    public async Task ReadForResource_WhenTheCacheEntryIsGone_ShouldThrowMcpException()
    {
        // A published McpServerResource is removed from the server collection outside the cache
        // lock, so a read can still arrive after its entry was evicted. The reader must fail with a
        // clear not-found error rather than serving another entry's content.
        var cache = new DiagramResourceCache();
        await cache.StoreAsync("graph", SolutionPath, "text/plain", "content", "desc",
            null, CancellationToken.None);

        var readForResource = typeof(DiagramResourceCache)
            .GetMethod("ReadForResource", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var act = () => _ = readForResource.Invoke(cache, [UriFor("graph", "/src/evicted.slnx")]);

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<McpException>()
            .WithMessage("*Resource not found*");
    }

    [Fact]
    public async Task ReadForResource_ForALiveEntry_ShouldPromoteItInTheLruOrder()
    {
        // resources/read goes through ReadForResource rather than TryRead, so it must apply the
        // same LRU promotion — otherwise reading a diagram would not protect it from eviction.
        await using var session = await InProcessMcpSession.StartAsync();
        var cache = new DiagramResourceCache();

        for (var i = 0; i < DiagramResourceCache.MaxCachedResources; i++)
        {
            await cache.StoreAsync("graph", $"/src/file{i}.slnx", "text/plain", $"content{i}",
                $"desc{i}", session.Server, CancellationToken.None);
        }

        var file0 = (await session.Client.ListResourcesAsync())
            .Single(resource => resource.Uri == UriFor("graph", "/src/file0.slnx"));
        await file0.ReadAsync();

        await cache.StoreAsync("graph", "/src/newest.slnx", "text/plain", "newest", "desc",
            session.Server, CancellationToken.None);

        cache.TryRead(UriFor("graph", "/src/file0.slnx")).Should()
            .Be("content0", "file0 was just read and must not be the eviction victim");
        cache.TryRead(UriFor("graph", "/src/file1.slnx")).Should()
            .BeNull("file1 became the least recently used entry once file0 was promoted");
    }
}
