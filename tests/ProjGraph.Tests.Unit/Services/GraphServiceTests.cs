using ProjGraph.Lib.Services;
using System.Runtime.InteropServices;

namespace ProjGraph.Tests.Unit.Services;

public class GraphServiceTests
{
    [Fact]
    public void BuildGraph_FromCsproj_ShouldDiscoverAllDependencies()
    {
        // Skip on Linux/macOS - MSBuild issues with sample projects on CI
        // Functionality is validated by integration tests which use real project files
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange
        var graphService = new GraphService();
        var projectAPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "samples", "visualize", "simple-dependencies", "A", "A.csproj"
        );
        var normalizedPath = Path.GetFullPath(projectAPath);

        // Skip test if sample project doesn't exist
        if (!File.Exists(normalizedPath))
        {
            return;
        }

        // Act
        var graph = graphService.BuildGraph(normalizedPath);

        // Assert
        Assert.NotNull(graph);
        Assert.Equal(4, graph.Projects.Count); // A, B, C, D
        Assert.Contains(graph.Projects, p => p.Name == "A");
        Assert.Contains(graph.Projects, p => p.Name == "B");
        Assert.Contains(graph.Projects, p => p.Name == "C");
        Assert.Contains(graph.Projects, p => p.Name == "D");

        // Verify dependencies: A -> B, B -> C, B -> D
        Assert.Equal(3, graph.Dependencies.Count);

        var projectA = graph.Projects.First(p => p.Name == "A");
        var projectB = graph.Projects.First(p => p.Name == "B");
        var projectC = graph.Projects.First(p => p.Name == "C");
        var projectD = graph.Projects.First(p => p.Name == "D");

        Assert.Contains(graph.Dependencies, d => d.SourceId == projectA.Id && d.TargetId == projectB.Id);
        Assert.Contains(graph.Dependencies, d => d.SourceId == projectB.Id && d.TargetId == projectC.Id);
        Assert.Contains(graph.Dependencies, d => d.SourceId == projectB.Id && d.TargetId == projectD.Id);
    }
}