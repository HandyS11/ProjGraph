using FluentAssertions;
using ProjGraph.Lib.Services;
using ProjGraph.Lib.Services.EfAnalysis;
using ProjGraph.Mcp;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpErdTests : IDisposable
{
    private const string SamplesPath = @"..\..\..\..\..\samples";
    private readonly string _tempFile;

    public McpErdTests()
    {
        _tempFile = Path.GetTempFileName() + ".cs";
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
        var path = Path.Combine(Directory.GetCurrentDirectory(), SamplesPath, relativePath);
        return Path.GetFullPath(path);
    }

    private static ProjGraphTools CreateTools()
    {
        var graphService = new GraphService();
        var efService = new EfAnalysisService();
        return new ProjGraphTools(graphService, efService);
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
        var contextPath = GetSamplePath(@"erd\simple-context\EntityFramework\MyDbContext.cs");

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
        const string nonExistentPath = @"C:\this\path\does\not\exist.cs";

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
        var invalidFile = Path.GetTempFileName() + ".cs";
        await File.WriteAllTextAsync(invalidFile, "public class NotADbContext { }");

        try
        {
            // Act
            var result = await tools.GetErd(invalidFile);

            // Assert
            result.Should().StartWith("Error");
        }
        finally
        {
            if (File.Exists(invalidFile))
            {
                File.Delete(invalidFile);
            }
        }
    }

    [Fact]
    public async Task GetErd_NonCsFile_ShouldReturnError()
    {
        // Arrange
        var tools = CreateTools();
        var nonCsFile = Path.GetTempFileName() + ".txt";
        await File.WriteAllTextAsync(nonCsFile, "Not a C# file");

        try
        {
            // Act
            var result = await tools.GetErd(nonCsFile);

            // Assert
            result.Should().StartWith("Error");
        }
        finally
        {
            if (File.Exists(nonCsFile))
            {
                File.Delete(nonCsFile);
            }
        }
    }

    #endregion

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }
}