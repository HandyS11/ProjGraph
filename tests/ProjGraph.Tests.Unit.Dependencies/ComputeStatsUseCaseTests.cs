using ProjGraph.Core.Models;
using ProjGraph.Lib.Dependencies.Application.UseCases;

namespace ProjGraph.Tests.Unit.Dependencies;

/// <summary>
/// Tests for <see cref="ComputeStatsUseCase"/>.
/// </summary>
[Trait("Category", "Stats")]
public sealed class ComputeStatsUseCaseTests
{
    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static Project MakeProject(Guid id, string name, ProjectType type)
    {
        return new Project(id, name, $"/{name}/{name}.csproj", $"{name}/{name}.csproj", "net10.0", type);
    }

    private static Dependency ProjectRef(Guid sourceId, Guid targetId)
    {
        return new Dependency(sourceId, targetId, DependencyType.ProjectReference);
    }

    // ─── Type Breakdown ────────────────────────────────────────────────────────

    [Fact]
    public void Execute_TypeBreakdown_CountsCorrectly()
    {
        var (a, b, c, d) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var graph = new SolutionGraph("Test", "/Test.slnx",
            [
                MakeProject(a, "LibA", ProjectType.Library),
                MakeProject(b, "LibB", ProjectType.Library),
                MakeProject(c, "Tests", ProjectType.Test),
                MakeProject(d, "App", ProjectType.Executable)
            ],
            [ProjectRef(d, a), ProjectRef(d, b), ProjectRef(c, a)]);

        var stats = ComputeStatsUseCase.Execute(graph);

        stats.TotalProjectCount.Should().Be(4);
        stats.TypeBreakdown["Library"].Should().Be(2);
        stats.TypeBreakdown["Test"].Should().Be(1);
        stats.TypeBreakdown["Executable"].Should().Be(1);
        stats.TypeBreakdown["Other"].Should().Be(0);
        stats.TypeBreakdown.Keys.Should().BeEquivalentTo("Library", "Executable", "Test", "Other");
    }

    [Fact]
    public void Execute_ExcludesPackageNodes_FromCounts()
    {
        var (lib, pkg) = (Guid.NewGuid(), Guid.NewGuid());
        var graph = new SolutionGraph("Test", "/Test.slnx",
            [
                MakeProject(lib, "MyLib", ProjectType.Library),
                MakeProject(pkg, "Newtonsoft.Json", ProjectType.Package)
            ],
            [ProjectRef(lib, pkg)]);

        var stats = ComputeStatsUseCase.Execute(graph);

        stats.TotalProjectCount.Should().Be(1);
        stats.TypeBreakdown["Library"].Should().Be(1);
    }

    // ─── Depth Stats ───────────────────────────────────────────────────────────

    [Fact]
    public void Execute_DepthStats_CorrectForLinearChain()
    {
        // A → B → C → D (chain of 4)
        var (a, b, c, d) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var graph = new SolutionGraph("Test", "/Test.slnx",
            [
                MakeProject(a, "A", ProjectType.Library),
                MakeProject(b, "B", ProjectType.Library),
                MakeProject(c, "C", ProjectType.Library),
                MakeProject(d, "D", ProjectType.Library)
            ],
            [ProjectRef(a, b), ProjectRef(b, c), ProjectRef(c, d)]);

        var stats = ComputeStatsUseCase.Execute(graph);

        stats.HasCycles.Should().BeFalse();
        stats.DepthStats.Max.Should().Be(3); // A has depth 3
        stats.DepthStats.Min.Should().Be(0); // D has depth 0 (leaf)
        stats.DepthStats.Average.Should().BeApproximately(1.5, 0.01); // (3+2+1+0)/4
    }

    [Fact]
    public void Execute_DepthStats_CorrectForDiamond()
    {
        //   A
        //  / \
        // B   C
        //  \ /
        //   D
        // depths: D=0, B=1, C=1, A=2; avg = 4/4 = 1.0
        var (a, b, c, d) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var graph = new SolutionGraph("Test", "/Test.slnx",
            [
                MakeProject(a, "A", ProjectType.Library),
                MakeProject(b, "B", ProjectType.Library),
                MakeProject(c, "C", ProjectType.Library),
                MakeProject(d, "D", ProjectType.Library)
            ],
            [ProjectRef(a, b), ProjectRef(a, c), ProjectRef(b, d), ProjectRef(c, d)]);

        var stats = ComputeStatsUseCase.Execute(graph);

        stats.HasCycles.Should().BeFalse();
        stats.DepthStats.Min.Should().Be(0);
        stats.DepthStats.Max.Should().Be(2);
        stats.DepthStats.Average.Should().BeApproximately(1.0, 0.01);
    }

    // ─── In-Degree / Hotspot Ranking ───────────────────────────────────────────

    [Fact]
    public void Execute_HotspotProjects_RankedByDirectInDegree()
    {
        // D→A, E→B, A→B, A→C, B→C
        // In-degrees: A=1(D), B=2(A,E), C=2(A,B), D=0, E=0
        var (a, b, c, d, e) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var graph = new SolutionGraph("Test", "/Test.slnx",
            [
                MakeProject(a, "A", ProjectType.Library),
                MakeProject(b, "B", ProjectType.Library),
                MakeProject(c, "C", ProjectType.Library),
                MakeProject(d, "D", ProjectType.Test),
                MakeProject(e, "E", ProjectType.Executable)
            ],
            [
                ProjectRef(d, a), ProjectRef(e, b),
                ProjectRef(a, b), ProjectRef(a, c), ProjectRef(b, c)
            ]);

        var stats = ComputeStatsUseCase.Execute(graph, 3);

        stats.HotspotProjects.Should().HaveCount(3);
        stats.HotspotProjects[0].InDegree.Should().Be(2);
        stats.HotspotProjects.Select(h => h.Name).Should().Contain("B").And.Contain("C");
        stats.HotspotProjects.Select(h => h.Name).Should().Contain("A");
    }

    [Fact]
    public void Execute_HotspotProjects_RespectsTopN()
    {
        var ids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        // All reference ids[0] — in-degree 5 for ids[0], 0 for others
        var projects = ids.Select((id, i) => MakeProject(id, $"P{i}", ProjectType.Library)).ToList();
        var deps = ids.Skip(1).Select(id => ProjectRef(id, ids[0])).ToList();
        var graph = new SolutionGraph("Test", "/Test.slnx", projects, deps);

        var stats2 = ComputeStatsUseCase.Execute(graph, 2);
        stats2.HotspotProjects.Should().HaveCount(1); // only ids[0] has in-degree > 0

        var stats5 = ComputeStatsUseCase.Execute(graph);
        stats5.HotspotProjects.Should().HaveCount(1);
    }

    // ─── Empty Graph ───────────────────────────────────────────────────────────

    [Fact]
    public void Execute_EmptyGraph_ReturnsZeroStats()
    {
        var graph = new SolutionGraph("Empty", "/Empty.slnx", [], []);

        var stats = ComputeStatsUseCase.Execute(graph);

        stats.TotalProjectCount.Should().Be(0);
        stats.HotspotProjects.Should().BeEmpty();
        stats.HasCycles.Should().BeFalse();
        stats.DepthStats.Average.Should().Be(0.0);
        stats.DepthStats.Min.Should().Be(0);
        stats.DepthStats.Max.Should().Be(0);
    }

    [Fact]
    public void Execute_SingleProject_NoDependencies_DepthZero()
    {
        var id = Guid.NewGuid();
        var graph = new SolutionGraph("Solo", "/Solo.slnx",
            [MakeProject(id, "Alone", ProjectType.Library)],
            []);

        var stats = ComputeStatsUseCase.Execute(graph);

        stats.TotalProjectCount.Should().Be(1);
        stats.DepthStats.Average.Should().Be(0.0);
        stats.DepthStats.Min.Should().Be(0);
        stats.DepthStats.Max.Should().Be(0);
        stats.HotspotProjects.Should().BeEmpty();
        stats.HasCycles.Should().BeFalse();
    }

    // ─── Cycle Detection ───────────────────────────────────────────────────────

    [Fact]
    public void Execute_CyclicGraph_HasCyclesTrue_DepthNegative()
    {
        // A → B → A (cycle)
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var graph = new SolutionGraph("Cyclic", "/Cyclic.slnx",
            [
                MakeProject(a, "A", ProjectType.Library),
                MakeProject(b, "B", ProjectType.Library)
            ],
            [ProjectRef(a, b), ProjectRef(b, a)]);

        var stats = ComputeStatsUseCase.Execute(graph);

        stats.HasCycles.Should().BeTrue();
        stats.DepthStats.Average.Should().BeNull();
        stats.DepthStats.Min.Should().BeNull();
        stats.DepthStats.Max.Should().BeNull();
    }

    [Fact]
    public void Execute_NoCycle_HasCyclesFalse()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var graph = new SolutionGraph("Clean", "/Clean.slnx",
            [
                MakeProject(a, "A", ProjectType.Library),
                MakeProject(b, "B", ProjectType.Library)
            ],
            [ProjectRef(a, b)]);

        var stats = ComputeStatsUseCase.Execute(graph);

        stats.HasCycles.Should().BeFalse();
    }

    [Fact]
    public void Execute_SelfReferencingProject_ShouldNotCrash()
    {
        // A self-referencing project is a degenerate edge case.
        // The algorithm should handle it without crashing.
        var id = Guid.NewGuid();
        var graph = new SolutionGraph("SelfRef", "/SelfRef.slnx",
            [MakeProject(id, "SelfRef", ProjectType.Library)],
            [ProjectRef(id, id)]);

        var stats = ComputeStatsUseCase.Execute(graph);

        stats.TotalProjectCount.Should().Be(1);
    }

    [Fact]
    public void Execute_DeepChain100Levels_ShouldComputeCorrectDepth()
    {
        const int depth = 100;
        var ids = Enumerable.Range(0, depth + 1).Select(_ => Guid.NewGuid()).ToArray();
        var projects = ids.Select((id, i) => MakeProject(id, $"P{i}", ProjectType.Library)).ToList();
        var deps = Enumerable.Range(0, depth).Select(i => ProjectRef(ids[i], ids[i + 1])).ToList();
        var graph = new SolutionGraph("Deep", "/Deep.slnx", projects, deps);

        var stats = ComputeStatsUseCase.Execute(graph);

        stats.HasCycles.Should().BeFalse();
        stats.DepthStats.Max.Should().Be(depth);
        stats.DepthStats.Min.Should().Be(0);
    }

    [Fact]
    public void Execute_UnicodeProjectNames_ShouldHandleCorrectly()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var graph = new SolutionGraph("Unicode", "/Unicode.slnx",
            [
                MakeProject(a, "Ünïcödé.Lîb", ProjectType.Library),
                MakeProject(b, "日本語プロジェクト", ProjectType.Library)
            ],
            [ProjectRef(a, b)]);

        var stats = ComputeStatsUseCase.Execute(graph);

        stats.TotalProjectCount.Should().Be(2);
        stats.HasCycles.Should().BeFalse();
        stats.DepthStats.Max.Should().Be(1);
    }
}
