using NSubstitute;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Dependencies.Application;

namespace ProjGraph.Tests.Unit.Dependencies;

/// <summary>
/// Tests for <see cref="StatsService"/>.
/// </summary>
[Trait("Category", "Stats")]
public sealed class StatsServiceTests
{
    private readonly IGraphService _graphService = Substitute.For<IGraphService>();
    private readonly StatsService _sut;

    public StatsServiceTests()
    {
        _sut = new StatsService(_graphService);
    }

    [Fact]
    public async Task ComputeStatsAsync_CallsBuildGraphAsync_WithCorrectPath()
    {
        const string path = "/repos/MySolution.slnx";
        var emptyGraph = new SolutionGraph("MySolution", path, [], []);
        _graphService.BuildGraphAsync(path, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(emptyGraph);

        _ = await _sut.ComputeStatsAsync(path);

        await _graphService.Received(1).BuildGraphAsync(path, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComputeStatsAsync_PassesIncludePackagesFalse()
    {
        const string path = "/repos/MySolution.slnx";
        _graphService.BuildGraphAsync(path, false, Arg.Any<CancellationToken>())
            .Returns(new SolutionGraph("MySolution", path, [], []));

        _ = await _sut.ComputeStatsAsync(path);

        await _graphService.Received(1).BuildGraphAsync(path, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComputeStatsAsync_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        const string path = "/repos/MySolution.slnx";
        _graphService.BuildGraphAsync(path, false, cts.Token)
            .Returns(new SolutionGraph("MySolution", path, [], []));

        _ = await _sut.ComputeStatsAsync(path, cancellationToken: cts.Token);

        await _graphService.Received(1).BuildGraphAsync(path, false, cts.Token);
    }

    [Fact]
    public async Task ComputeStatsAsync_ReturnsStatsForBuiltGraph()
    {
        const string path = "/repos/MySolution.slnx";
        var libId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var graph = new SolutionGraph("MySolution", path,
            [
                new Project(libId, "MyLib", "/MyLib.csproj", "MyLib.csproj", "net10.0", ProjectType.Library),
                new Project(testId, "MyLib.Tests", "/MyLib.Tests.csproj", "MyLib.Tests.csproj", "net10.0",
                    ProjectType.Test)
            ],
            [new Dependency(testId, libId, DependencyType.ProjectReference)]);
        _graphService.BuildGraphAsync(path, false, Arg.Any<CancellationToken>()).Returns(graph);

        var stats = await _sut.ComputeStatsAsync(path);

        stats.SolutionName.Should().Be("MySolution");
        stats.TotalProjectCount.Should().Be(2);
        stats.TypeBreakdown["Library"].Should().Be(1);
        stats.TypeBreakdown["Test"].Should().Be(1);
        stats.HotspotProjects.Should().ContainSingle(h => h.Name == "MyLib" && h.InDegree == 1);
    }

    [Fact]
    public async Task ComputeStatsAsync_DefaultsTopNToFive()
    {
        const string path = "/repos/MySolution.slnx";
        // 10 libraries all pointing at a shared core
        var coreId = Guid.NewGuid();
        var projects = new List<Project>
        {
            new(coreId, "Core", "/Core.csproj", "Core.csproj", "net10.0", ProjectType.Library)
        };
        var deps = new List<Dependency>();
        for (var i = 0; i < 10; i++)
        {
            var id = Guid.NewGuid();
            projects.Add(
                new Project(id, $"Lib{i}", $"/Lib{i}.csproj", $"Lib{i}.csproj", "net10.0", ProjectType.Library));
            deps.Add(new Dependency(id, coreId, DependencyType.ProjectReference));
        }

        _graphService.BuildGraphAsync(path, false, Arg.Any<CancellationToken>())
            .Returns(new SolutionGraph("MySolution", path, projects, deps));

        // default topN = 5
        var stats = await _sut.ComputeStatsAsync(path);

        // Core has in-degree 10 and is the only hotspot; only 1 project has in-degree > 0
        stats.HotspotProjects.Should().HaveCount(1);
        stats.HotspotProjects[0].Name.Should().Be("Core");
        stats.HotspotProjects[0].InDegree.Should().Be(10);
    }
}
