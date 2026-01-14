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
        var dependencies = new List<Dependency>
        {
            new(guidA, guidB, DependencyType.ProjectReference)
        };

        var graph = new SolutionGraph("Test", "Test.sln", projects, dependencies);

        // Act
        var sccs = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);

        // Assert
        sccs.Should().HaveCount(2); // Each node is its own SCC
        sccs.All(scc => scc.Count == 1).Should().BeTrue();
    }
}
