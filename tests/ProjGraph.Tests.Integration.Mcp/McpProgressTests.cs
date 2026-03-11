using ModelContextProtocol;
using ProjGraph.Tests.Integration.Mcp.Helpers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Integration.Mcp;

public sealed class McpProgressTests : IDisposable
{
    private readonly TestDirectory _temp = new();

    [Fact]
    public async Task GetClassDiagram_WithProgress_ShouldReport3Stages()
    {
        var tools = McpTestHelper.CreateTools();
        var tempFile = Path.Combine(_temp.DirectoryPath, "Test.cs");
        await File.WriteAllTextAsync(tempFile, "namespace Test; public class Foo { public int Id { get; set; } }");

        var progress = new ProgressCollector();
        await tools.GetClassDiagramAsync(tempFile, progress: progress);

        progress.Reports.Should().HaveCount(3);
        progress.Reports[0].Message.Should().Be("Discovering C# files");
        progress.Reports[1].Message.Should().Be("Analyzing types and members");
        progress.Reports[2].Message.Should().Be("Rendering class diagram");
    }

    [Fact]
    public async Task GetClassDiagram_WithoutProgress_ShouldSucceed()
    {
        var tools = McpTestHelper.CreateTools();
        var tempFile = Path.Combine(_temp.DirectoryPath, "Test.cs");
        await File.WriteAllTextAsync(tempFile, "namespace Test; public class Foo { public int Id { get; set; } }");

        var result = await tools.GetClassDiagramAsync(tempFile);
        result.Should().Contain("classDiagram");
    }

    [Fact]
    public async Task GetProjectGraph_WithProgress_ShouldReport3Stages()
    {
        var tools = McpTestHelper.CreateTools();
        var slnxPath = TestPathHelper.GetRootPath("ProjGraph.slnx");

        var progress = new ProgressCollector();
        await tools.GetProjectGraphAsync(slnxPath, progress: progress);

        progress.Reports.Should().HaveCount(3);
        progress.Reports[0].Message.Should().Be("Parsing solution file");
        progress.Reports[1].Message.Should().Be("Building dependency graph");
        progress.Reports[2].Message.Should().Be("Rendering diagram");
    }

    [Fact]
    public async Task GetProjectStats_WithProgress_ShouldReport3Stages()
    {
        var tools = McpTestHelper.CreateTools();
        var slnxPath = TestPathHelper.GetRootPath("ProjGraph.slnx");

        var progress = new ProgressCollector();
        await tools.GetProjectStatsAsync(slnxPath, progress: progress);

        progress.Reports.Should().HaveCount(3);
        progress.Reports[0].Message.Should().Be("Parsing solution");
        progress.Reports[1].Message.Should().Be("Computing dependency metrics");
        progress.Reports[2].Message.Should().Be("Summarizing results");
    }

    [Fact]
    public async Task GetErd_WithProgress_ShouldReport3Stages()
    {
        var tools = McpTestHelper.CreateTools();
        var tempFile = Path.Combine(_temp.DirectoryPath, "Ctx.cs");
        await File.WriteAllTextAsync(tempFile,
            """
            using Microsoft.EntityFrameworkCore;
            namespace Test;
            public class Blog { public int Id { get; set; } public string Title { get; set; } }
            public class TestDbContext : DbContext { public DbSet<Blog> Blogs { get; set; } }
            """);

        var progress = new ProgressCollector();
        await tools.GetErdAsync(tempFile, progress: progress);

        progress.Reports.Should().HaveCount(3);
        progress.Reports[0].Message.Should().Be("Parsing EF Core context");
        progress.Reports[1].Message.Should().Be("Analyzing entities and relationships");
        progress.Reports[2].Message.Should().Be("Rendering entity diagram");
    }

    [Fact]
    public async Task AllProgress_ShouldHaveCorrectTotalAndSequence()
    {
        var tools = McpTestHelper.CreateTools();
        var slnxPath = TestPathHelper.GetRootPath("ProjGraph.slnx");

        var progress = new ProgressCollector();
        await tools.GetProjectGraphAsync(slnxPath, progress: progress);

        for (var i = 0; i < progress.Reports.Count; i++)
        {
            progress.Reports[i].Total.Should().Be(3);
            progress.Reports[i].Progress.Should().Be(i + 1);
        }
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    private sealed class ProgressCollector : IProgress<ProgressNotificationValue>
    {
        public List<ProgressNotificationValue> Reports { get; } = [];

        public void Report(ProgressNotificationValue value)
        {
            Reports.Add(value);
        }
    }
}
