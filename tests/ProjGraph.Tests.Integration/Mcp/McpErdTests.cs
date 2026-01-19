using FluentAssertions;
using ProjGraph.Lib.Services;
using ProjGraph.Lib.Services.EfAnalysis;
using ProjGraph.Mcp;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpErdTests : IDisposable
{
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

    [Fact]
    public async Task GetErd_ShouldReturnValidMermaid()
    {
        // Arrange
        var graphService = new GraphService();
        var efService = new EfAnalysisService();
        var tools = new ProjGraphTools(graphService, efService);

        // Act
        var result = await tools.GetErd(_tempFile);

        // Assert
        result.Should().NotStartWith("Error");
        result.Should().Contain("erDiagram");
        result.Should().Contain("Blog");
        result.Should().Contain("Post");
        result.Should().Contain("||--o{"); // Relationship
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }
}