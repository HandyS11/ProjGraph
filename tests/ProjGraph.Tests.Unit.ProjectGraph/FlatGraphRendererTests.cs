using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.ProjectGraph.Rendering;

namespace ProjGraph.Tests.Unit.ProjectGraph;

/// <summary>
/// Tests for <see cref="FlatGraphRenderer"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FlatGraphRendererTests : IDisposable
{
    private readonly FlatGraphRenderer _sut = new();

    public void Dispose()
    {
        _sut.Dispose();
    }

    private static SolutionGraph CreateGraph(
        string name,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Dependency> dependencies)
    {
        return new SolutionGraph(name, "/test/path", projects, dependencies);
    }

    [Fact]
    public void Format_ShouldBeFlat()
    {
        _sut.Format.Should().Be("flat");
    }

    [Fact]
    public void Render_SingleProjectNoDependencies_ShouldContainProjectName()
    {
        var project = new Project(Guid.NewGuid(), "MyProject", "/path/MyProject.csproj", "MyProject.csproj",
            "net10.0", ProjectType.Library);
        var graph = CreateGraph("TestSolution", [project], []);

        var result = _sut.Render(graph);

        result.Should().Contain("MyProject");
    }

    [Fact]
    public void Render_WithDependencies_ShouldContainSourceAndTarget()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var projectA = new Project(idA, "ProjectA", "/a.csproj", "a.csproj", "net10.0", ProjectType.Library);
        var projectB = new Project(idB, "ProjectB", "/b.csproj", "b.csproj", "net10.0", ProjectType.Library);
        var dep = new Dependency(idA, idB, DependencyType.ProjectReference);
        var graph = CreateGraph("TestSolution", [projectA, projectB], [dep]);

        var result = _sut.Render(graph);

        result.Should().Contain("ProjectA");
        result.Should().Contain("ProjectB");
    }

    [Fact]
    public void Render_EmptyGraph_ShouldNotThrow()
    {
        var graph = CreateGraph("Empty", [], []);

        var act = () => _sut.Render(graph);

        act.Should().NotThrow();
    }

    [Fact]
    public void Render_WithShowTitleFalse_ShouldNotContainDependencyGraph()
    {
        var project = new Project(Guid.NewGuid(), "Proj", "/p.csproj", "p.csproj", "net10.0", ProjectType.Library);
        var graph = CreateGraph("TestSolution", [project], []);
        var options = new DiagramOptions(false);

        var result = _sut.Render(graph, options);

        result.Should().NotContain("Dependency Graph");
    }

    [Fact]
    public void Render_CyclicDependencies_ShouldContainCycleWarning()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var projectA = new Project(idA, "A", "/a.csproj", "a.csproj", "net10.0", ProjectType.Library);
        var projectB = new Project(idB, "B", "/b.csproj", "b.csproj", "net10.0", ProjectType.Library);
        var dep1 = new Dependency(idA, idB, DependencyType.ProjectReference);
        var dep2 = new Dependency(idB, idA, DependencyType.ProjectReference);
        var graph = CreateGraph("CyclicSolution", [projectA, projectB], [dep1, dep2]);

        var result = _sut.Render(graph);

        result.Should().Contain("Cycles detected");
    }

    [Fact]
    public void Render_MultipleProjects_ShouldRenderAll()
    {
        var projects = Enumerable.Range(1, 5)
            .Select(i => new Project(Guid.NewGuid(), $"Project{i}", $"/p{i}.csproj", $"p{i}.csproj", "net10.0",
                ProjectType.Library))
            .ToList();
        var graph = CreateGraph("MultiSolution", projects, []);

        var result = _sut.Render(graph);

        foreach (var p in projects)
        {
            result.Should().Contain(p.Name);
        }
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
