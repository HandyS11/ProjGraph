using FluentAssertions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Algorithms;

namespace ProjGraph.Tests.Unit.Algorithms;

public class TarjanSccAlgorithmTests
{
    [Fact]
    public void FindStronglyConnectedComponents_ShouldDetectCycles()
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

        // A -> B -> C -> A (Cycle!)
        var dependencies = new List<Dependency>
        {
            new(guidA, guidB, DependencyType.ProjectReference),
            new(guidB, guidC, DependencyType.ProjectReference),
            new(guidC, guidA, DependencyType.ProjectReference)
        };

        var graph = new SolutionGraph("Test", "Test.sln", projects, dependencies);

        // Act
        var sccs = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);

        // Assert
        sccs.Should().HaveCount(1);
        sccs[0].Should().HaveCount(3);
        sccs[0].Should().Contain(guidA);
        sccs[0].Should().Contain(guidB);
        sccs[0].Should().Contain(guidC);
    }

    [Fact]
    public void FindStronglyConnectedComponents_ShouldHandleAcyclicGraph()
    {
        // Arrange
        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "A", "A.csproj", "A.csproj", "net10.0", ProjectType.Library),
            new(guidB, "B", "B.csproj", "B.csproj", "net10.0", ProjectType.Library)
        };

        // A -> B
        var dependencies = new List<Dependency> { new(guidA, guidB, DependencyType.ProjectReference) };

        var graph = new SolutionGraph("Test", "Test.sln", projects, dependencies);

        // Act
        var sccs = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);

        // Assert
        sccs.Should().HaveCount(2); // Each node is its own SCC
        sccs.All(scc => scc.Count == 1).Should().BeTrue();
    }

    [Fact]
    public void FindStronglyConnectedComponents_ShouldHandleEmptyGraph()
    {
        // Arrange
        var graph = new SolutionGraph("Empty", "Empty.sln", [], []);

        // Act
        var sccs = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);

        // Assert
        sccs.Should().BeEmpty();
    }

    [Fact]
    public void FindStronglyConnectedComponents_ShouldHandleSelfLoop()
    {
        // Arrange
        var guidA = Guid.NewGuid();

        var projects = new List<Project> { new(guidA, "A", "A.csproj", "A.csproj", "net10.0", ProjectType.Library) };

        // A -> A (Self loop)
        var dependencies = new List<Dependency> { new(guidA, guidA, DependencyType.ProjectReference) };

        var graph = new SolutionGraph("Test", "Test.sln", projects, dependencies);

        // Act
        var sccs = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);

        // Assert
        sccs.Should().HaveCount(1);
        sccs[0].Should().ContainSingle().Which.Should().Be(guidA);
    }

    [Fact]
    public void FindStronglyConnectedComponents_ShouldHandleMultipleSCCs()
    {
        // Arrange
        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();
        var guidC = Guid.NewGuid();
        var guidD = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "A", "A.csproj", "A.csproj", "net10.0", ProjectType.Library),
            new(guidB, "B", "B.csproj", "B.csproj", "net10.0", ProjectType.Library),
            new(guidC, "C", "C.csproj", "C.csproj", "net10.0", ProjectType.Library),
            new(guidD, "D", "D.csproj", "D.csproj", "net10.0", ProjectType.Library)
        };

        // A <-> B (SCC1), C <-> D (SCC2)
        var dependencies = new List<Dependency>
        {
            new(guidA, guidB, DependencyType.ProjectReference),
            new(guidB, guidA, DependencyType.ProjectReference),
            new(guidC, guidD, DependencyType.ProjectReference),
            new(guidD, guidC, DependencyType.ProjectReference)
        };

        var graph = new SolutionGraph("Test", "Test.sln", projects, dependencies);

        // Act
        var sccs = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);

        // Assert
        sccs.Should().HaveCount(2);
        sccs.Should().OnlyContain(scc => scc.Count == 2);
    }

    [Fact]
    public void FindStronglyConnectedComponents_ShouldHandleComplexGraph()
    {
        // Arrange
        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();
        var guidC = Guid.NewGuid();
        var guidD = Guid.NewGuid();
        var guidE = Guid.NewGuid();

        var projects = new List<Project>
        {
            new(guidA, "A", "A.csproj", "A.csproj", "net10.0", ProjectType.Library),
            new(guidB, "B", "B.csproj", "B.csproj", "net10.0", ProjectType.Library),
            new(guidC, "C", "C.csproj", "C.csproj", "net10.0", ProjectType.Library),
            new(guidD, "D", "D.csproj", "D.csproj", "net10.0", ProjectType.Library),
            new(guidE, "E", "E.csproj", "E.csproj", "net10.0", ProjectType.Library)
        };

        // A -> B <-> C -> D, E (isolated)
        var dependencies = new List<Dependency>
        {
            new(guidA, guidB, DependencyType.ProjectReference),
            new(guidB, guidC, DependencyType.ProjectReference),
            new(guidC, guidB, DependencyType.ProjectReference),
            new(guidC, guidD, DependencyType.ProjectReference)
        };

        var graph = new SolutionGraph("Test", "Test.sln", projects, dependencies);

        // Act
        var sccs = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);

        // Assert
        sccs.Should().HaveCount(4); // A, B<->C, D, E
        sccs.Should().Contain(scc => scc.Count == 2); // B<->C
        sccs.Should().Contain(scc => scc.Count == 1 && scc.Contains(guidA));
        sccs.Should().Contain(scc => scc.Count == 1 && scc.Contains(guidD));
        sccs.Should().Contain(scc => scc.Count == 1 && scc.Contains(guidE));
    }

    [Fact]
    public void FindStronglyConnectedComponents_ShouldHandleDisconnectedNodes()
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

        // No dependencies - all disconnected
        var graph = new SolutionGraph("Test", "Test.sln", projects, []);

        // Act
        var sccs = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);

        // Assert
        sccs.Should().HaveCount(3);
        sccs.Should().OnlyContain(scc => scc.Count == 1);
    }
}