using FluentAssertions;
using ProjGraph.Lib.Application.Services;
using ProjGraph.Lib.Infrastructure.Parsers;
using ProjGraph.Tests.Unit.Helpers;

namespace ProjGraph.Tests.Unit.Services;

public class GraphServiceTests
{
    private readonly GraphService _graphService = new(new SlnParser(), new SlnxParser(), new ProjectParser());

    [Fact]
    public void BuildGraph_FromCsproj_ShouldDiscoverAllDependencies()
    {
        // Arrange
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
        var graph = _graphService.BuildGraph(normalizedPath);

        // Assert
        graph.Should().NotBeNull();
        graph.Projects.Should().HaveCount(4); // A, B, C, D
        graph.Projects.Should().Contain(p => p.Name == "A");
        graph.Projects.Should().Contain(p => p.Name == "B");
        graph.Projects.Should().Contain(p => p.Name == "C");
        graph.Projects.Should().Contain(p => p.Name == "D");

        // Verify dependencies: A -> B, B -> C, B -> D
        graph.Dependencies.Should().HaveCount(3);

        var projectA = graph.Projects.First(p => p.Name == "A");
        var projectB = graph.Projects.First(p => p.Name == "B");
        var projectC = graph.Projects.First(p => p.Name == "C");
        var projectD = graph.Projects.First(p => p.Name == "D");

        graph.Dependencies.Should().Contain(d => d.SourceId == projectA.Id && d.TargetId == projectB.Id);
        graph.Dependencies.Should().Contain(d => d.SourceId == projectB.Id && d.TargetId == projectC.Id);
        graph.Dependencies.Should().Contain(d => d.SourceId == projectB.Id && d.TargetId == projectD.Id);
    }

    [Fact]
    public void BuildGraph_ShouldThrowForNonExistentFile()
    {
        // Arrange
        using var temp = new TestDirectory();
        var nonExistentPath = temp.GetTempFilePath(".sln");

        // Act & Assert
        var act = () => _graphService.BuildGraph(nonExistentPath);
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void BuildGraph_ShouldThrowForUnsupportedFileType()
    {
        // Arrange
        using var temp = new TestDirectory();
        var tempFile = temp.CreateFile("test.txt", "test content");

        // Act & Assert
        var act = () => _graphService.BuildGraph(tempFile);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unsupported file type*");
    }

    [Fact]
    public void BuildGraph_FromSlnx_ShouldHandleEmptySolution()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                               </Solution>
                               """;

        var tempSlnx = temp.CreateFile("empty.slnx", content);

        // Act
        var graph = _graphService.BuildGraph(tempSlnx);

        // Assert
        graph.Should().NotBeNull();
        graph.Projects.Should().BeEmpty();
        graph.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void BuildGraph_ShouldSkipNonExistentProjectFiles()
    {
        // Arrange

        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                                 <Project Path="NonExistent/Project.csproj" />
                               </Solution>
                               """;

        var tempSlnx = temp.CreateFile("missing_projects.slnx", content);

        // Act
        var graph = _graphService.BuildGraph(tempSlnx);

        // Assert
        graph.Should().NotBeNull();
        graph.Projects.Should().BeEmpty();
    }

    [Fact]
    public void BuildGraph_ShouldSetCorrectGraphName()
    {
        // Arrange

        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                               </Solution>
                               """;

        var tempSlnx = temp.CreateFile("MySolution.slnx", content);

        // Act
        var graph = _graphService.BuildGraph(tempSlnx);

        // Assert
        graph.Name.Should().Be("MySolution.slnx");
        graph.Path.Should().Be(tempSlnx);
    }

    [Fact]
    public void BuildGraph_ShouldHandleProjectsWithoutDependencies()
    {
        // Arrange

        using var temp = new TestDirectory();

        const string projectContent = """
                                      <Project Sdk="Microsoft.NET.Sdk">
                                        <PropertyGroup>
                                          <TargetFramework>net10.0</TargetFramework>
                                        </PropertyGroup>
                                      </Project>
                                      """;

        const string slnxContent = """
                                   <Solution>
                                     <Project Path="Project.csproj" />
                                   </Solution>
                                   """;

        temp.CreateFile("Project.csproj", projectContent);
        var slnxPath = temp.CreateFile("Solution.slnx", slnxContent);

        // Act
        var graph = _graphService.BuildGraph(slnxPath);

        // Assert
        graph.Projects.Should().HaveCount(1);
        graph.Dependencies.Should().BeEmpty();
    }
}