using FluentAssertions;
using ProjGraph.Lib.Services.EfAnalysis;

namespace ProjGraph.Tests.Unit.Services;

public class EfAnalysisServiceTests
{
    private readonly EfAnalysisService _service = new();

    [Fact]
    public async Task DiscoverContextsAsync_ShouldFindDbContextInFile()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".cs";
        const string content = """

                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Blog> Blogs { get; set; }
                               }
                               public class Blog { public int Id { get; set; } }

                               """;
        await File.WriteAllTextAsync(filePath, content);

        try
        {
            // Act
            var contexts = await _service.DiscoverContextsAsync(filePath);

            // Assert
            contexts.Should().Contain("AppDbContext");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldExtractEntitiesFromDbSets()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".cs";
        const string content = """

                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Post> Posts { get; set; }
                               }
                               public class Post { 
                                   public int Id { get; set; } 
                                   public string Title { get; set; }
                               }

                               """;
        await File.WriteAllTextAsync(filePath, content);

        try
        {
            // Act
            var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

            // Assert
            model.Entities.Should().ContainSingle(e => e.Name == "Post");
            var post = model.Entities.First(e => e.Name == "Post");
            post.Properties.Should().Contain(p => p.Name == "Id" && p.IsPrimaryKey);
            post.Properties.Should().Contain(p => p.Name == "Title");
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}