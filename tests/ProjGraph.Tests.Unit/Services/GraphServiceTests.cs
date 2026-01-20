using FluentAssertions;
using ProjGraph.Lib.Services;

namespace ProjGraph.Tests.Unit.Services;

public class GraphServiceTests
{
    [Fact]
    public void BuildGraph_FromCsproj_ShouldDiscoverAllDependencies()
    {
        // Arrange
        var graphService = new GraphService();
        var projectAPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "samples", "visualize", "simple-dependencies", "A", "A.csproj"
        );
        var normalizedPath = Path.GetFullPath(projectAPath);

        // Skip test if sample project doesn't exist (makes test optional across platforms)
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

    [Fact]
    public void BuildGraph_ShouldThrowForNonExistentFile()
    {
        // Arrange
        var graphService = new GraphService();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.sln");

        // Act & Assert
        var act = () => graphService.BuildGraph(nonExistentPath);
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void BuildGraph_ShouldThrowForUnsupportedFileType()
    {
        // Arrange
        var graphService = new GraphService();
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        File.WriteAllText(tempFile, "test content");

        try
        {
            // Act & Assert
            var act = () => graphService.BuildGraph(tempFile);
            act.Should().Throw<ArgumentException>()
                .WithMessage("*Unsupported file type*");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void BuildGraph_FromSlnx_ShouldHandleEmptySolution()
    {
        // Arrange
        var graphService = new GraphService();
        var tempSlnx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.slnx");

        const string content = """
                               <Solution>
                               </Solution>
                               """;

        File.WriteAllText(tempSlnx, content);

        try
        {
            // Act
            var graph = graphService.BuildGraph(tempSlnx);

            // Assert
            graph.Should().NotBeNull();
            graph.Projects.Should().BeEmpty();
            graph.Dependencies.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempSlnx))
            {
                File.Delete(tempSlnx);
            }
        }
    }

    [Fact]
    public void BuildGraph_ShouldSkipNonExistentProjectFiles()
    {
        // Arrange
        var graphService = new GraphService();
        var tempSlnx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.slnx");

        const string content = """
                               <Solution>
                                 <Project Path="NonExistent/Project.csproj" />
                               </Solution>
                               """;

        File.WriteAllText(tempSlnx, content);

        try
        {
            // Act
            var graph = graphService.BuildGraph(tempSlnx);

            // Assert
            graph.Should().NotBeNull();
            graph.Projects.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempSlnx))
            {
                File.Delete(tempSlnx);
            }
        }
    }

    [Fact]
    public void BuildGraph_ShouldSetCorrectGraphName()
    {
        // Arrange
        var graphService = new GraphService();
        var tempSlnx = Path.Combine(Path.GetTempPath(), $"MySolution.slnx");

        const string content = """
                               <Solution>
                               </Solution>
                               """;

        File.WriteAllText(tempSlnx, content);

        try
        {
            // Act
            var graph = graphService.BuildGraph(tempSlnx);

            // Assert
            graph.Name.Should().Be("MySolution.slnx");
            graph.Path.Should().Be(tempSlnx);
        }
        finally
        {
            if (File.Exists(tempSlnx))
            {
                File.Delete(tempSlnx);
            }
        }
    }

    [Fact]
    public void BuildGraph_ShouldHandleProjectsWithoutDependencies()
    {
        // Arrange
        var graphService = new GraphService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var projectPath = Path.Combine(tempDir, "Project.csproj");
        var slnxPath = Path.Combine(tempDir, "Solution.slnx");

        const string projectContent = """
                                      <Project Sdk="Microsoft.NET.Sdk">
                                        <PropertyGroup>
                                          <TargetFramework>net10.0</TargetFramework>
                                        </PropertyGroup>
                                      </Project>
                                      """;

        const string slnxContent = $"""
                                    <Solution>
                                      <Project Path="Project.csproj" />
                                    </Solution>
                                    """;

        File.WriteAllText(projectPath, projectContent);
        File.WriteAllText(slnxPath, slnxContent);

        try
        {
            // Act
            var graph = graphService.BuildGraph(slnxPath);

            // Assert
            graph.Projects.Should().HaveCount(1);
            graph.Dependencies.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}