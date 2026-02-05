using FluentAssertions;
using ProjGraph.Lib.Application.Services;
using ProjGraph.Lib.Application.UseCases.ClassAnalysis;
using ProjGraph.Lib.Application.UseCases.EfAnalysis;
using ProjGraph.Lib.Application.UseCases.SolutionGraph;
using ProjGraph.Lib.Infrastructure.Analysis;
using ProjGraph.Lib.Infrastructure.Analysis.ClassAnalysis;
using ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis;
using ProjGraph.Lib.Infrastructure.Parsers;
using ProjGraph.Lib.Infrastructure.Rendering;
using ProjGraph.Mcp;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpIntegrationTests
{
    [Fact]
    public void GetProjectGraph_SimpleDependencies_Slnx_ShouldReturnValidMermaid()
    {
        // Arrange
        var tools = CreateTools();
        var slnxPath = GetSamplePath(@"visualize\simple-dependencies\simple-dependencies.slnx");

        // Act
        var result = tools.GetProjectGraph(slnxPath);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().StartWith("```mermaid");
        result.Should().Contain("graph TD");
        result.Should().Contain("A");
        result.Should().Contain("B");
        result.Should().Contain("C");
        result.Should().Contain("D");
        result.Should().Contain("-->");
        result.Trim().Should().EndWith("```");
    }

    [Fact]
    public void GetProjectGraph_SimpleDependencies_SingleProject_ShouldDiscoverAllDependencies()
    {
        // Arrange
        var tools = CreateTools();
        var projPath = GetSamplePath(@"visualize\simple-dependencies\A\A.csproj");

        // Act
        var result = tools.GetProjectGraph(projPath);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().StartWith("```mermaid");
        result.Should().Contain("graph TD");
        result.Should().Contain("A");
        result.Should().Contain("B");
        result.Should().Contain("-->");
        result.Trim().Should().EndWith("```");
    }

    [Fact]
    public void GetProjectGraph_ProjGraphSolution_Slnx_ShouldReturnValidMermaid()
    {
        // Arrange
        var tools = CreateTools();
        var slnxPath = GetRootPath("ProjGraph.slnx");

        // Act
        var result = tools.GetProjectGraph(slnxPath);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().StartWith("```mermaid");
        result.Should().Contain("graph TD");
        result.Should().Contain("ProjGraph_Cli");
        result.Should().Contain("ProjGraph_Core");
        result.Should().Contain("ProjGraph_Lib");
        result.Should().Contain("ProjGraph_Mcp");
        result.Trim().Should().EndWith("```");
    }

    [Fact]
    public void GetProjectGraph_SimpleDependencies_ShouldShowCorrectRelationships()
    {
        // Arrange
        var tools = CreateTools();
        var slnxPath = GetSamplePath("visualize/simple-dependencies/simple-dependencies.slnx");

        // Act
        var result = tools.GetProjectGraph(slnxPath);

        // Assert
        result.Should().Contain("A --> B");
        result.Should().Contain("B --> C");
        result.Should().Contain("B --> D");
    }

    [Fact]
    public void GetProjectGraph_NonExistentFile_ShouldReturnError()
    {
        // Arrange
        var tools = CreateTools();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "this", "path", "does", "not", "exist.slnx");

        // Act
        var result = tools.GetProjectGraph(nonExistentPath);

        // Assert
        result.Should().StartWith("Error");
    }

    [Fact]
    public void GetProjectGraph_InvalidFile_ShouldReturnError()
    {
        // Arrange
        var tools = CreateTools();
        var invalidPath = GetRootPath("README.md"); // Not a solution/project file

        // Act
        var result = tools.GetProjectGraph(invalidPath);

        // Assert
        result.Should().StartWith("Error");
    }

    private static string GetSamplePath(string relativePath)
    {
        // Split path by both forward and backward slashes to support cross-platform
        var parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var pathParts = new[] { Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "samples" }
            .Concat(parts)
            .ToArray();
        var path = Path.Combine(pathParts);
        return Path.GetFullPath(path);
    }

    private static string GetRootPath(string relativePath)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", relativePath);
        return Path.GetFullPath(path);
    }

    private static ProjGraphTools CreateTools()
    {
        var slnParser = new SlnParser();
        var slnxParser = new SlnxParser();
        var projectParser = new ProjectParser();
        var graphService = new GraphService(new BuildGraphUseCase(slnParser, slnxParser, projectParser,
            new ProjectDiscoveryService(projectParser)));

        var compilationFactory = new CompilationFactory();
        var typeProcessor = new TypeProcessor();

        var efService = new EfAnalysisService(new AnalyzeContextUseCase(new EfModelAnalyzer(compilationFactory)),
            new DiscoverContextsUseCase(new EfModelAnalyzer(compilationFactory)),
            new AnalyzeSnapshotUseCase(new EfModelAnalyzer(compilationFactory)),
            new DiscoverSnapshotsUseCase(new EfModelAnalyzer(compilationFactory)));
        var classService = new ClassAnalysisService(new AnalyzeFileUseCase(compilationFactory, typeProcessor));

        return new ProjGraphTools(
            graphService,
            efService,
            classService,
            new MermaidGraphRenderer(),
            new MermaidClassDiagramRenderer(),
            new MermaidErdRenderer());
    }
}



