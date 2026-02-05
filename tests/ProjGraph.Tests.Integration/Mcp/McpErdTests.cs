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
using ProjGraph.Tests.Integration.Helpers;

namespace ProjGraph.Tests.Integration.Mcp;

public sealed class McpErdTests : IDisposable
{
    private readonly TestDirectory _temp = new();
    private readonly string _tempFile;

    public McpErdTests()
    {
        _tempFile = Path.Combine(_temp.DirectoryPath, "temp.cs");
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using System.Collections.Generic;

                               namespace TestNamespace;

                               public class Blog
                               {
                                   public int Id { get; set; }
                                   public string Title { get; set; }
                                   public List<Post> Posts { get; set; }
                               }

                               public class Post
                               {
                                   public int Id { get; set; }
                                   public string Content { get; set; }
                                   public int BlogId { get; set; }
                                   public Blog Blog { get; set; }
                               }

                               public class MyDbContext : DbContext
                               {
                                   public DbSet<Blog> Blogs { get; set; }
                                   public DbSet<Post> Posts { get; set; }
                               }
                               """;
        File.WriteAllText(_tempFile, content);
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

    #region Simple In-Memory DbContext Tests

    [Fact]
    public async Task GetErd_SimpleDbContext_ShouldReturnValidMermaid()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetErd(_tempFile);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("erDiagram");
        result.Should().Contain("Blog");
        result.Should().Contain("Post");
        result.Should().Contain("||--o{"); // Relationship
    }

    [Fact]
    public async Task GetErd_SimpleDbContext_ShouldShowProperties()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetErd(_tempFile);

        // Assert
        result.Should().Contain("int Id");
        result.Should().Contain("string Title");
        result.Should().Contain("string Content");
        result.Should().Contain("int BlogId");
    }

    [Fact]
    public async Task GetErd_SimpleDbContext_ShouldShowRelationship()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.GetErd(_tempFile);

        // Assert
        result.Should().Contain("Blog ||--o{ Post");
    }

    #endregion

    #region Sample Project Tests

    [Fact]
    public async Task GetErd_SimpleContext_ShouldGenerateCompleteErDiagram()
    {
        // Arrange
        var tools = CreateTools();
        var contextPath = GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act
        var result = await tools.GetErd(contextPath);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("erDiagram");
        result.Should().Contain("Author {");
        result.Should().Contain("Book {");
        result.Should().Contain("Category {");
        result.Should().Contain("Publisher {");
        result.Should().Contain("Review {");
    }

    [Fact]
    public async Task GetErd_SimpleContext_ShouldShowAllProperties()
    {
        // Arrange
        var tools = CreateTools();
        var contextPath = GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act
        var result = await tools.GetErd(contextPath);

        // Assert
        result.Should().Contain("int Id PK");
        result.Should().Contain("string Name");
        result.Should().Contain("string Title");
        result.Should().Contain("int Rating");
        result.Should().Contain("DateTime PublishedDate");
    }

    [Fact]
    public async Task GetErd_SimpleContext_ShouldShowOneToManyRelationships()
    {
        // Arrange
        var tools = CreateTools();
        var contextPath = GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act
        var result = await tools.GetErd(contextPath);

        // Assert
        result.Should().Contain("||--o{"); // One-to-Many notation
        result.Should().Contain("Publisher ||--o{ Book");
        result.Should().Contain("Book ||--o{ Review");
    }

    [Fact]
    public async Task GetErd_SimpleContext_WithContextName_ShouldSucceed()
    {
        // Arrange
        var tools = CreateTools();
        var contextPath = GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

        // Act
        var result = await tools.GetErd(contextPath, "MyDbContext");

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("erDiagram");
        result.Should().Contain("Author");
        result.Should().Contain("Book");
    }

    [Fact]
    public async Task GetErd_SimpleContext_ShouldShowForeignKeys()
    {
        // Arrange
        var tools = CreateTools();
        var contextPath = GetSamplePath("erd/simple-context/EntityFramework/MyDbContext.cs");

        // Act
        var result = await tools.GetErd(contextPath);

        // Assert
        result.Should().Contain("FK");
        result.Should().Contain("int PublisherId FK");
        result.Should().Contain("int BookId FK");
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task GetErd_NonExistentFile_ShouldReturnError()
    {
        // Arrange
        var tools = CreateTools();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cs");

        // Act
        var result = await tools.GetErd(nonExistentPath);

        // Assert
        result.Should().StartWith("Error");
    }

    [Fact]
    public async Task GetErd_InvalidCsFile_ShouldReturnError()
    {
        // Arrange
        var tools = CreateTools();
        var invalidFile = Path.Combine(_temp.DirectoryPath, "invalid.cs");
        await File.WriteAllTextAsync(invalidFile, "public class NotADbContext { }");

        // Act
        var result = await tools.GetErd(invalidFile);

        // Assert
        result.Should().StartWith("Error");
    }

    [Fact]
    public async Task GetErd_NonCsFile_ShouldReturnError()
    {
        // Arrange
        var tools = CreateTools();
        var nonCsFile = Path.Combine(_temp.DirectoryPath, "test.txt");
        await File.WriteAllTextAsync(nonCsFile, "Not a C# file");

        // Act
        var result = await tools.GetErd(nonCsFile);

        // Assert
        result.Should().StartWith("Error");
    }

    #endregion

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _temp.Dispose();
        }
    }
}



