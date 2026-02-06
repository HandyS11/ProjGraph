using ProjGraph.Core.Models;

namespace ProjGraph.Tests.Unit.Core.Models;

[Trait("Category", "Core")]
public class SolutionGraphTests
{
    [Fact]
    public void SolutionGraph_ShouldCreateWithAllProperties()
    {
        // Arrange
        const string name = "MySolution";
        var path = Path.Combine("MySolution", "MySolution.sln");

        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "ProjectA", "ProjectA.csproj", "ProjectA.csproj", "net10.0", ProjectType.Library),
            new(guidB, "ProjectB", "ProjectB.csproj", "ProjectB.csproj", "net10.0", ProjectType.Library)
        };

        var dependencies = new List<Dependency> { new(guidA, guidB, DependencyType.ProjectReference) };

        // Act
        var graph = new SolutionGraph(name, path, projects, dependencies);

        // Assert
        graph.Name.Should().Be(name);
        graph.Path.Should().Be(path);
        graph.Projects.Should().HaveCount(2);
        graph.Projects.Should().Contain(projects[0]);
        graph.Projects.Should().Contain(projects[1]);
        graph.Dependencies.Should().HaveCount(1);
        graph.Dependencies.Should().Contain(dependencies[0]);
    }

    [Fact]
    public void SolutionGraph_ShouldSupportRecordEquality()
    {
        // Arrange
        var projects = new List<Project>
        {
            new(Guid.NewGuid(), "ProjectA", "ProjectA.csproj", "ProjectA.csproj", "net10.0", ProjectType.Library)
        };

        var graph1 = new SolutionGraph("Test", "Test.sln", projects, []);
        var graph2 = new SolutionGraph("Test", "Test.sln", projects, []);

        // Act & Assert
        graph1.Should().Be(graph2);
    }

    [Fact]
    public void SolutionGraph_ShouldHandleEmptyProjectsAndDependencies()
    {
        // Arrange & Act
        var graph = new SolutionGraph("Empty", "Empty.sln", [], []);

        // Assert
        graph.Name.Should().Be("Empty");
        graph.Path.Should().Be("Empty.sln");
        graph.Projects.Should().BeEmpty();
        graph.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void SolutionGraph_ProjectsCollectionShouldBeReadable()
    {
        // Arrange
        var guidA = Guid.NewGuid();
        var projects = new List<Project>
        {
            new(guidA, "ProjectA", "ProjectA.csproj", "ProjectA.csproj", "net10.0", ProjectType.Library)
        };

        var graph = new SolutionGraph("Test", "Test.sln", projects, []);

        // Act
        var foundProject = graph.Projects.FirstOrDefault(p => p.Id == guidA);

        // Assert
        foundProject.Should().NotBeNull();
        foundProject.Name.Should().Be("ProjectA");
    }

    [Fact]
    public void SolutionGraph_DependenciesCollectionShouldBeReadable()
    {
        // Arrange
        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "ProjectA", "ProjectA.csproj", "ProjectA.csproj", "net10.0", ProjectType.Library),
            new(guidB, "ProjectB", "ProjectB.csproj", "ProjectB.csproj", "net10.0", ProjectType.Library)
        };

        var dependencies = new List<Dependency> { new(guidA, guidB, DependencyType.ProjectReference) };

        var graph = new SolutionGraph("Test", "Test.sln", projects, dependencies);

        // Act
        var foundDependency = graph.Dependencies.FirstOrDefault(d => d.SourceId == guidA);

        // Assert
        foundDependency.Should().NotBeNull();
        foundDependency.TargetId.Should().Be(guidB);
    }

    [Fact]
    public void SolutionGraph_ShouldAllowMultipleDependenciesFromSameProject()
    {
        // Arrange
        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();
        var guidC = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "A", "A.csproj", "A.csproj", "net10.0", ProjectType.Library),
            new(guidB, "B", "B.csproj", "B.csproj", "net10.0", ProjectType.Library),
            new(guidC, "C", "C.csproj", "C.csproj", "net10.0", ProjectType.Library)
        };

        var dependencies = new List<Dependency>
        {
            new(guidA, guidB, DependencyType.ProjectReference), new(guidA, guidC, DependencyType.ProjectReference)
        };

        // Act
        var graph = new SolutionGraph("Test", "Test.sln", projects, dependencies);

        // Assert
        graph.Dependencies.Count(d => d.SourceId == guidA).Should().Be(2);
    }
}