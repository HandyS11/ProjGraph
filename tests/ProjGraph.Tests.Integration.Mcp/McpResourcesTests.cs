using ProjGraph.Mcp;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpResourcesTests
{
    [Fact]
    public void Welcome_ShouldReturn_NonEmptyContent()
    {
        var content = ProjGraphResources.GetWelcome();
        content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Welcome_ShouldContain_AllToolDescriptions()
    {
        var content = ProjGraphResources.GetWelcome();
        content.Should().Contain("get_project_graph");
        content.Should().Contain("get_class_diagram");
        content.Should().Contain("get_erd");
        content.Should().Contain("get_project_stats");
    }

    [Fact]
    public void Welcome_ShouldContain_AllPromptDescriptions()
    {
        var content = ProjGraphResources.GetWelcome();
        content.Should().Contain("architecture_review");
        content.Should().Contain("dependency_analysis");
        content.Should().Contain("database_schema_review");
        content.Should().Contain("class_structure_review");
    }

    [Fact]
    public void Welcome_ShouldContain_DiagramCacheExplanation()
    {
        var content = ProjGraphResources.GetWelcome();
        content.Should().Contain("projgraph://diagrams/{type}/{encodedPath}");
    }

    [Fact]
    public async Task Cache_Store_ShouldMakeResourceReadable()
    {
        var cache = new DiagramResourceCache();

        await cache.StoreAsync("graph", @"D:\Projects\MySolution.slnx", "text/plain",
            "graph TD\n  A --> B", "Project graph", null, CancellationToken.None);

        var uri = $"projgraph://diagrams/graph/{Uri.EscapeDataString(@"D:\Projects\MySolution.slnx")}";
        var content = cache.TryRead(uri);

        content.Should().Be("graph TD\n  A --> B");
    }

    [Fact]
    public async Task Cache_Store_ShouldAppearInListResources()
    {
        var cache = new DiagramResourceCache();

        await cache.StoreAsync("class", @"D:\Src\MyClass.cs", "text/plain",
            "classDiagram", "Class diagram", null, CancellationToken.None);

        var resources = cache.ListResources();
        resources.Should().ContainSingle();

        var resource = resources[0];
        resource.AnalysisType.Should().Be("class");
        resource.MimeType.Should().Be("text/plain");
    }

    [Fact]
    public async Task Cache_StoreMultiple_ShouldDeduplicate()
    {
        var cache = new DiagramResourceCache();

        await cache.StoreAsync("graph", @"D:\test.slnx", "text/plain",
            "v1", "Graph v1", null, CancellationToken.None);
        await cache.StoreAsync("graph", @"D:\test.slnx", "text/plain",
            "v2", "Graph v2", null, CancellationToken.None);

        var resources = cache.ListResources();
        resources.Should().ContainSingle("Same path should not create duplicate resources");

        var uri = $"projgraph://diagrams/graph/{Uri.EscapeDataString(@"D:\test.slnx")}";
        var content = cache.TryRead(uri);
        content.Should().Be("v2", "Content should be updated to latest version");
    }

    [Fact]
    public void Cache_TryRead_NonExistentUri_ShouldReturnNull()
    {
        var cache = new DiagramResourceCache();
        var content = cache.TryRead("projgraph://diagrams/graph/nonexistent");
        content.Should().BeNull();
    }

    [Fact]
    public void Cache_MaxCachedResources_ShouldBe50()
    {
        DiagramResourceCache.MaxCachedResources.Should().Be(50);
    }

    [Fact]
    public async Task Cache_ShouldEvictLRU_WhenAtCapacity()
    {
        var cache = new DiagramResourceCache();

        // Fill cache to capacity
        for (var i = 0; i < DiagramResourceCache.MaxCachedResources; i++)
        {
            await cache.StoreAsync("graph", $@"D:\file{i}.slnx", "text/plain",
                $"content{i}", $"desc{i}", null, CancellationToken.None);
        }

        cache.ListResources().Should().HaveCount(DiagramResourceCache.MaxCachedResources);

        // Add one more — should evict the LRU (file0)
        await cache.StoreAsync("graph", @"D:\file_new.slnx", "text/plain",
            "new content", "new desc", null, CancellationToken.None);

        cache.ListResources().Should().HaveCount(DiagramResourceCache.MaxCachedResources);

        // file0 should be evicted
        var evictedUri = $"projgraph://diagrams/graph/{Uri.EscapeDataString(@"D:\file0.slnx")}";
        cache.TryRead(evictedUri).Should().BeNull("LRU entry should be evicted");

        // New entry should still be readable
        var newUri = $"projgraph://diagrams/graph/{Uri.EscapeDataString(@"D:\file_new.slnx")}";
        cache.TryRead(newUri).Should().Be("new content");
    }

    [Fact]
    public async Task ReadDiagram_ExistingResource_ShouldReturnContent()
    {
        var cache = new DiagramResourceCache();
        await cache.StoreAsync("graph", @"D:\Projects\Test.slnx", "text/plain",
            "graph TD\n  A --> B", "Test graph", null, CancellationToken.None);

        var resources = new ProjGraphResources(cache);
        var encodedPath = Uri.EscapeDataString(@"D:\Projects\Test.slnx");
        var result = resources.ReadDiagram("graph", encodedPath);

        result.Should().NotBeNull();
        result.Contents.Should().ContainSingle();
    }

    [Fact]
    public void ReadDiagram_NonExistentResource_ShouldThrow()
    {
        var cache = new DiagramResourceCache();
        var resources = new ProjGraphResources(cache);

        var act = () => resources.ReadDiagram("graph", "nonexistent");
        act.Should().Throw<Exception>();
    }
}
