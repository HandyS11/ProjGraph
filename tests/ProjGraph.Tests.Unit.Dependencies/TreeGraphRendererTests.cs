using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Dependencies.Rendering;

namespace ProjGraph.Tests.Unit.Dependencies;

/// <summary>
/// Tests for <see cref="TreeGraphRenderer"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TreeGraphRendererTests
{
    private readonly TreeGraphRenderer _sut = new();


    private static SolutionGraph CreateGraph(
        string name,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Dependency> dependencies)
    {
        return new SolutionGraph(name, "/test/path", projects, dependencies);
    }

    [Fact]
    public void Format_ShouldBeTree()
    {
        _sut.Format.Should().Be("tree");
    }

    [Fact]
    public void Render_SingleRootProject_ShouldContainProjectName()
    {
        var project = new Project(Guid.NewGuid(), "Root", "/root.csproj", "root.csproj", "net10.0",
            ProjectType.Library);
        var graph = CreateGraph("Solution", [project], []);

        var result = _sut.Render(graph);

        result.Should().Contain("Root");
    }

    [Fact]
    public async Task Render_ConcurrentCalls_ProduceIndependentResults()
    {
        // The renderer is registered as a singleton, so concurrent Render calls on one instance
        // must not share mutable state (the console/writer are now per-call, not instance fields).
        var graphA = CreateGraph("SolutionA",
            [new Project(Guid.NewGuid(), "AlphaProj", "/a.csproj", "a.csproj", "net10.0", ProjectType.Library)], []);
        var graphB = CreateGraph("SolutionB",
            [new Project(Guid.NewGuid(), "BetaProj", "/b.csproj", "b.csproj", "net10.0", ProjectType.Library)], []);

        var tasks = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => i % 2 == 0 ? _sut.Render(graphA) : _sut.Render(graphB)))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < results.Length; i++)
        {
            if (i % 2 == 0)
            {
                results[i].Should().Contain("AlphaProj").And.NotContain("BetaProj");
            }
            else
            {
                results[i].Should().Contain("BetaProj").And.NotContain("AlphaProj");
            }
        }
    }

    [Fact]
    public void Render_SharedSubtree_IsNotReExpanded()
    {
        // Diamond: App -> Lib1, App -> Lib2; Lib1 -> Shared, Lib2 -> Shared; Shared -> Deep.
        // The Shared subtree must be expanded once and shown as a reference on the second path, so
        // its descendant "Deep" appears exactly once — no exponential re-expansion of shared nodes.
        var (app, lib1, lib2, shared, deep) =
            (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        static Project P(Guid id, string n) =>
            new(id, n, $"/{n}.csproj", $"{n}.csproj", "net10.0", ProjectType.Library);

        var graph = CreateGraph("S",
            [P(app, "App"), P(lib1, "Lib1"), P(lib2, "Lib2"), P(shared, "Shared"), P(deep, "Deep")],
            [
                new Dependency(app, lib1, DependencyType.ProjectReference),
                new Dependency(app, lib2, DependencyType.ProjectReference),
                new Dependency(lib1, shared, DependencyType.ProjectReference),
                new Dependency(lib2, shared, DependencyType.ProjectReference),
                new Dependency(shared, deep, DependencyType.ProjectReference)
            ]);

        var result = _sut.Render(graph);

        var deepOccurrences = result.Split("Deep", StringSplitOptions.None).Length - 1;
        deepOccurrences.Should().Be(1);
    }

    [Fact]
    public void Render_WithDependency_ShouldContainBothProjects()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var parent = new Project(idA, "Parent", "/parent.csproj", "parent.csproj", "net10.0",
            ProjectType.Executable);
        var child = new Project(idB, "Child", "/child.csproj", "child.csproj", "net10.0",
            ProjectType.Library);
        var dep = new Dependency(idA, idB, DependencyType.ProjectReference);
        var graph = CreateGraph("TreeSolution", [parent, child], [dep]);

        var result = _sut.Render(graph);

        result.Should().Contain("Parent");
        result.Should().Contain("Child");
    }

    [Fact]
    public void Render_EmptyGraph_ShouldNotThrow()
    {
        var graph = CreateGraph("Empty", [], []);

        var act = () => _sut.Render(graph);

        act.Should().NotThrow();
    }

    [Fact]
    public void Render_CyclicDependencies_ShouldContainCycleWarning()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var a = new Project(idA, "CycleA", "/a.csproj", "a.csproj", "net10.0", ProjectType.Library);
        var b = new Project(idB, "CycleB", "/b.csproj", "b.csproj", "net10.0", ProjectType.Library);
        var dep1 = new Dependency(idA, idB, DependencyType.ProjectReference);
        var dep2 = new Dependency(idB, idA, DependencyType.ProjectReference);
        var graph = CreateGraph("CyclicSolution", [a, b], [dep1, dep2]);

        var result = _sut.Render(graph);

        result.Should().Contain("Cycles detected");
    }

    [Fact]
    public void Render_WithShowTitleFalse_ShouldNotContainDependencyTree()
    {
        var project = new Project(Guid.NewGuid(), "Proj", "/p.csproj", "p.csproj", "net10.0",
            ProjectType.Library);
        var graph = CreateGraph("Solution", [project], []);
        var options = new DiagramOptions(false);

        var result = _sut.Render(graph, options);

        result.Should().NotContain("Dependency Tree");
    }

    [Fact]
    public void Render_DiamondDependency_ShouldContainAllProjects()
    {
        var idTop = Guid.NewGuid();
        var idLeft = Guid.NewGuid();
        var idRight = Guid.NewGuid();
        var idBottom = Guid.NewGuid();
        var top = new Project(idTop, "Top", "/top.csproj", "top.csproj", "net10.0", ProjectType.Executable);
        var left = new Project(idLeft, "Left", "/left.csproj", "left.csproj", "net10.0", ProjectType.Library);
        var right = new Project(idRight, "Right", "/right.csproj", "right.csproj", "net10.0",
            ProjectType.Library);
        var bottom = new Project(idBottom, "Bottom", "/bottom.csproj", "bottom.csproj", "net10.0",
            ProjectType.Library);
        var deps = new[]
        {
            new Dependency(idTop, idLeft, DependencyType.ProjectReference),
            new Dependency(idTop, idRight, DependencyType.ProjectReference),
            new Dependency(idLeft, idBottom, DependencyType.ProjectReference),
            new Dependency(idRight, idBottom, DependencyType.ProjectReference)
        };
        var graph = CreateGraph("DiamondSolution", [top, left, right, bottom], deps);

        var result = _sut.Render(graph);

        result.Should().Contain("Top");
        result.Should().Contain("Left");
        result.Should().Contain("Right");
        result.Should().Contain("Bottom");
    }

    [Fact]
    public void Render_WithPackage_ShouldContainPkgPrefix()
    {
        var parentId = Guid.NewGuid();
        var pkgId = Guid.NewGuid();
        var parent = new Project(parentId, "App", "/app.csproj", "app.csproj", "net10.0", ProjectType.Executable);
        var pkg = new Project(pkgId, "Newtonsoft.Json", "13.0.1", "13.0.1", "net10.0", ProjectType.Package);
        var dep = new Dependency(parentId, pkgId, DependencyType.PackageReference);
        var graph = CreateGraph("PkgSolution", [parent, pkg], [dep]);

        var result = _sut.Render(graph);

        result.Should().Contain("Newtonsoft.Json");
        result.Should().Contain("(13.0.1)");
    }
}
