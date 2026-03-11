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

    [Fact]
    public async Task Cache_TryRead_PromotesEntry_SoRecentlyReadEntryIsNotEvictedFirst()
    {
        var cache = new DiagramResourceCache();

        // Fill cache to capacity
        for (var i = 0; i < DiagramResourceCache.MaxCachedResources; i++)
        {
            await cache.StoreAsync("graph", $@"D:\file{i}.slnx", "text/plain",
                $"content{i}", $"desc{i}", null, CancellationToken.None);
        }

        // Read file0 — promotes it to most-recently-used, file1 becomes the new LRU
        var file0Uri = $"projgraph://diagrams/graph/{Uri.EscapeDataString(@"D:\file0.slnx")}";
        cache.TryRead(file0Uri).Should().Be("content0");

        // Add one more — should evict the current LRU (file1, not file0)
        await cache.StoreAsync("graph", @"D:\file_new.slnx", "text/plain",
            "new content", "new desc", null, CancellationToken.None);

        // file0 was recently read so it should still be present
        cache.TryRead(file0Uri).Should().Be("content0", "file0 was recently read and should not be evicted");

        // file1 is now the LRU and should have been evicted
        var file1Uri = $"projgraph://diagrams/graph/{Uri.EscapeDataString(@"D:\file1.slnx")}";
        cache.TryRead(file1Uri).Should().BeNull("file1 is the LRU after file0 was promoted");
    }

    [Fact]
    public async Task Cache_ListResources_ShouldReturnCorrectAllProperties()
    {
        var cache = new DiagramResourceCache();
        var before = DateTimeOffset.UtcNow;

        // Use a forward-slash path so Path.GetFileName behaves consistently on Linux and Windows
        const string path = "/data/MyContext.cs";
        await cache.StoreAsync("erd", path, "text/plain",
            "erDiagram\n  A ||--o{ B : has", "ERD for MyContext", null, CancellationToken.None);

        var after = DateTimeOffset.UtcNow;
        var resources = cache.ListResources();

        resources.Should().ContainSingle();
        var r = resources[0];

        r.Uri.Should().Be($"projgraph://diagrams/erd/{Uri.EscapeDataString(path)}");
        r.AnalysisType.Should().Be("erd");
        r.SourcePath.Should().Be(path);
        r.MimeType.Should().Be("text/plain");
        r.Description.Should().Be("ERD for MyContext");
        r.Name.Should().Be("erd \u2014 MyContext.cs");
        r.GeneratedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        r.LastUpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Cache_Store_DifferentTypeSamePath_ShouldCreateDistinctEntries()
    {
        var cache = new DiagramResourceCache();
        const string path = @"D:\Projects\Solution.slnx";

        await cache.StoreAsync("graph", path, "text/plain", "graph content", "Graph", null, CancellationToken.None);
        await cache.StoreAsync("class", path, "text/plain", "class content", "Class", null, CancellationToken.None);

        cache.ListResources().Should().HaveCount(2, "different types with the same path are distinct resources");

        var graphUri = $"projgraph://diagrams/graph/{Uri.EscapeDataString(path)}";
        var classUri = $"projgraph://diagrams/class/{Uri.EscapeDataString(path)}";

        cache.TryRead(graphUri).Should().Be("graph content");
        cache.TryRead(classUri).Should().Be("class content");
    }

    [Fact]
    public async Task Cache_Update_ShouldUpdateLastUpdatedAt_ButNotGeneratedAt()
    {
        var cache = new DiagramResourceCache();

        await cache.StoreAsync("graph", @"D:\test.slnx", "text/plain",
            "v1", "desc", null, CancellationToken.None);

        var generatedAt = cache.ListResources()[0].GeneratedAt;

        // Small delay to ensure timestamps differ
        await Task.Delay(10);

        await cache.StoreAsync("graph", @"D:\test.slnx", "text/plain",
            "v2", "updated desc", null, CancellationToken.None);

        var updated = cache.ListResources()[0];
        updated.GeneratedAt.Should().Be(generatedAt, "GeneratedAt should not change on update");
        updated.LastUpdatedAt.Should().BeAfter(generatedAt, "LastUpdatedAt should advance on update");
        updated.Description.Should().Be("updated desc", "description should be updated");
    }

    [Fact]
    public async Task Cache_Store_ShouldUseEscapedPathInUri()
    {
        var cache = new DiagramResourceCache();
        const string path = @"D:\My Projects\Solution File.slnx";

        await cache.StoreAsync("graph", path, "text/plain", "content", "desc", null, CancellationToken.None);

        var expectedUri = $"projgraph://diagrams/graph/{Uri.EscapeDataString(path)}";
        var resources = cache.ListResources();

        resources.Should().ContainSingle();
        resources[0].Uri.Should().Be(expectedUri);
        cache.TryRead(expectedUri).Should().Be("content");
    }
}
