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

    [Fact]
    public async Task DiscoverContextsAsync_ShouldThrowForNonCsFile()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".txt";
        await File.WriteAllTextAsync(filePath, "test content");

        try
        {
            // Act & Assert
            var act = async () => await _service.DiscoverContextsAsync(filePath);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Only .cs files are supported*");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldThrowForNonCsFile()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".txt";
        await File.WriteAllTextAsync(filePath, "test content");

        try
        {
            // Act & Assert
            var act = async () => await _service.AnalyzeContextAsync(filePath);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Only .cs files are supported*");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task DiscoverContextsAsync_ShouldReturnEmptyListWhenNoContextsFound()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".cs";
        const string content = """

                               namespace Test;
                               public class RegularClass 
                               { 
                                   public int Id { get; set; }
                               }

                               """;
        await File.WriteAllTextAsync(filePath, content);

        try
        {
            // Act
            var contexts = await _service.DiscoverContextsAsync(filePath);

            // Assert
            contexts.Should().BeEmpty();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task DiscoverContextsAsync_ShouldFindMultipleContexts()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".cs";
        const string content = """

                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext { }
                               public class SecondDbContext : DbContext { }

                               """;
        await File.WriteAllTextAsync(filePath, content);

        try
        {
            // Act
            var contexts = await _service.DiscoverContextsAsync(filePath);

            // Assert
            contexts.Should().HaveCount(2);
            contexts.Should().Contain("AppDbContext");
            contexts.Should().Contain("SecondDbContext");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleMultipleDbSets()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".cs";
        const string content = """

                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<User> Users { get; set; }
                                   public DbSet<Post> Posts { get; set; }
                                   public DbSet<Comment> Comments { get; set; }
                               }
                               public class User { public int Id { get; set; } }
                               public class Post { public int Id { get; set; } }
                               public class Comment { public int Id { get; set; } }

                               """;
        await File.WriteAllTextAsync(filePath, content);

        try
        {
            // Act
            var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

            // Assert
            model.Entities.Should().HaveCount(3);
            model.Entities.Should().Contain(e => e.Name == "User");
            model.Entities.Should().Contain(e => e.Name == "Post");
            model.Entities.Should().Contain(e => e.Name == "Comment");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldAnalyzeFirstContextWhenNameNotSpecified()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".cs";
        const string content = """

                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class FirstDbContext : DbContext 
                               { 
                                   public DbSet<User> Users { get; set; }
                               }
                               public class SecondDbContext : DbContext 
                               { 
                                   public DbSet<Post> Posts { get; set; }
                               }
                               public class User { public int Id { get; set; } }
                               public class Post { public int Id { get; set; } }

                               """;
        await File.WriteAllTextAsync(filePath, content);

        try
        {
            // Act
            var model = await _service.AnalyzeContextAsync(filePath);

            // Assert
            model.ContextName.Should().Be("FirstDbContext");
            model.Entities.Should().ContainSingle(e => e.Name == "User");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandlePropertiesWithDifferentTypes()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".cs";
        const string content = """

                               using Microsoft.EntityFrameworkCore;
                               using System;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Product> Products { get; set; }
                               }
                               public class Product 
                               { 
                                   public int Id { get; set; }
                                   public string Name { get; set; }
                                   public decimal Price { get; set; }
                                   public DateTime CreatedAt { get; set; }
                                   public bool IsActive { get; set; }
                               }

                               """;
        await File.WriteAllTextAsync(filePath, content);

        try
        {
            // Act
            var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

            // Assert
            var product = model.Entities.First(e => e.Name == "Product");
            product.Properties.Count.Should().BeGreaterThanOrEqualTo(5);
            product.Properties.Should().Contain(p => p.Name == "Name");
            product.Properties.Should().Contain(p => p.Name == "Price");
            product.Properties.Should().Contain(p => p.Name == "CreatedAt");
            product.Properties.Should().Contain(p => p.Name == "IsActive");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldIdentifyPrimaryKeysByConvention()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".cs";
        const string content = """

                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Customer> Customers { get; set; }
                               }
                               public class Customer 
                               { 
                                   public int Id { get; set; }
                                   public string Name { get; set; }
                               }

                               """;
        await File.WriteAllTextAsync(filePath, content);

        try
        {
            // Act
            var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

            // Assert
            var customer = model.Entities.First(e => e.Name == "Customer");
            customer.Properties.Should().Contain(p => p.Name == "Id" && p.IsPrimaryKey);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task DiscoverContextsAsync_ShouldHandleEmptyFile()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".cs";
        await File.WriteAllTextAsync(filePath, string.Empty);

        try
        {
            // Act
            var contexts = await _service.DiscoverContextsAsync(filePath);

            // Assert
            contexts.Should().BeEmpty();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleContextWithNoDbSets()
    {
        // Arrange
        var filePath = Path.GetTempFileName() + ".cs";
        const string content = """

                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   // No DbSets
                               }

                               """;
        await File.WriteAllTextAsync(filePath, content);

        try
        {
            // Act
            var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

            // Assert
            model.ContextName.Should().Be("AppDbContext");
            model.Entities.Should().BeEmpty();
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}