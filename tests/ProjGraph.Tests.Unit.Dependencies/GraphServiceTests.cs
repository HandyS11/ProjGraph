using Microsoft.Extensions.Logging.Abstractions;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;
using ProjGraph.Lib.Dependencies.Application;
using ProjGraph.Lib.Dependencies.Application.UseCases;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.Dependencies;

[Trait("Category", "ProjectGraph")]
public class GraphServiceTests
{
    private readonly GraphService _graphService = CreateService();

    private static GraphService CreateService()
    {
        var fs = new PhysicalFileSystem();
        var console = new NullOutputConsole();
        var projectParser = new ProjectParser(fs);
        var discoveryService = new ProjectDiscoveryService(projectParser, fs, console,
            NullLogger<ProjectDiscoveryService>.Instance);
        var useCase = new BuildGraphUseCase(new SlnParser(fs), new SlnxParser(fs), projectParser, discoveryService, fs,
            console, NullLogger<BuildGraphUseCase>.Instance);
        return new GraphService(useCase);
    }

    [Fact]
    public async Task BuildGraphAsync_FromCsproj_ShouldDiscoverAllDependencies()
    {
        // Arrange
        var projectAPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "samples", "visualize", "simple-dependencies", "A", "A.csproj"
        );
        var normalizedPath = Path.GetFullPath(projectAPath);

        // Skip test if sample project doesn't exist (e.g., in CI without samples)
        if (!File.Exists(normalizedPath))
        {
            throw new SkipTestException($"Sample project not found at: {normalizedPath}");
        }

        // Act
        var graph = await _graphService.BuildGraphAsync(normalizedPath);

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
    public async Task BuildGraphAsync_ShouldThrowForNonExistentFile()
    {
        // Arrange
        using var temp = new TestDirectory();
        var nonExistentPath = temp.GetTempFilePath(".sln");

        // Act & Assert
        var act = () => _graphService.BuildGraphAsync(nonExistentPath);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task BuildGraphAsync_ShouldThrowForUnsupportedFileType()
    {
        // Arrange
        using var temp = new TestDirectory();
        var tempFile = temp.CreateFile("test.txt", "test content");

        // Act & Assert
        var act = () => _graphService.BuildGraphAsync(tempFile);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unsupported file type*");
    }

    [Fact]
    public async Task BuildGraphAsync_FromSlnx_ShouldHandleEmptySolution()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                               </Solution>
                               """;

        var tempSlnx = temp.CreateFile("empty.slnx", content);

        // Act
        var graph = await _graphService.BuildGraphAsync(tempSlnx);

        // Assert
        graph.Should().NotBeNull();
        graph.Projects.Should().BeEmpty();
        graph.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildGraphAsync_ShouldSkipNonExistentProjectFiles()
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
        var graph = await _graphService.BuildGraphAsync(tempSlnx);

        // Assert
        graph.Should().NotBeNull();
        graph.Projects.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildGraphAsync_ShouldSetCorrectGraphName()
    {
        // Arrange

        using var temp = new TestDirectory();
        const string content = """
                               <Solution>
                               </Solution>
                               """;

        var tempSlnx = temp.CreateFile("MySolution.slnx", content);

        // Act
        var graph = await _graphService.BuildGraphAsync(tempSlnx);

        // Assert
        graph.Name.Should().Be("MySolution.slnx");
        graph.Path.Should().Be(tempSlnx);
    }

    [Fact]
    public async Task BuildGraphAsync_ShouldHandleProjectsWithoutDependencies()
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
        var graph = await _graphService.BuildGraphAsync(slnxPath);

        // Assert
        graph.Projects.Should().HaveCount(1);
        graph.Dependencies.Should().BeEmpty();
    }
}
