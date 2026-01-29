using FluentAssertions;
using ProjGraph.Lib.Services.EfAnalysis;
using ProjGraph.Tests.Unit.Helpers;

namespace ProjGraph.Tests.Unit.Services.EfAnalysis;

public class EfAnalysisServiceTests
{
    private readonly EfAnalysisService _service = new();

    [Fact]
    public async Task DiscoverContextsAsync_ShouldFindDbContextInFile()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Blog> Blogs { get; set; }
                               }
                               public class Blog { public int Id { get; set; } }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        // Act
        var contexts = await _service.DiscoverContextsAsync(filePath);

        // Assert
        contexts.Should().Contain("AppDbContext");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldExtractEntitiesFromDbSets()
    {
        // Arrange
        using var temp = new TestDirectory();
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
        var filePath = temp.CreateFile("Context.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        model.Entities.Should().ContainSingle(e => e.Name == "Post");
        var post = model.Entities.First(e => e.Name == "Post");
        post.Properties.Should().Contain(p => p.Name == "Id" && p.IsPrimaryKey);
        post.Properties.Should().Contain(p => p.Name == "Title");
    }

    [Fact]
    public async Task DiscoverContextsAsync_ShouldThrowForNonCsFile()
    {
        // Arrange
        using var temp = new TestDirectory();
        var filePath = temp.CreateFile("test.txt", "test content");

        // Act & Assert
        var act = async () => await _service.DiscoverContextsAsync(filePath);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Only .cs files are supported*");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldThrowForNonCsFile()
    {
        // Arrange
        using var temp = new TestDirectory();
        var filePath = temp.CreateFile("test.txt", "test content");

        // Act & Assert
        var act = async () => await _service.AnalyzeContextAsync(filePath);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Only .cs files are supported*");
    }

    [Fact]
    public async Task DiscoverContextsAsync_ShouldReturnEmptyListWhenNoContextsFound()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               namespace Test;
                               public class RegularClass 
                               { 
                                   public int Id { get; set; }
                               }
                               """;
        var filePath = temp.CreateFile("Regular.cs", content);

        // Act
        var contexts = await _service.DiscoverContextsAsync(filePath);

        // Assert
        contexts.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverContextsAsync_ShouldFindMultipleContexts()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext { }
                               public class SecondDbContext : DbContext { }
                               """;
        var filePath = temp.CreateFile("Contexts.cs", content);

        // Act
        var contexts = await _service.DiscoverContextsAsync(filePath);

        // Assert
        contexts.Should().HaveCount(2);
        contexts.Should().Contain("AppDbContext");
        contexts.Should().Contain("SecondDbContext");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleMultipleDbSets()
    {
        // Arrange
        using var temp = new TestDirectory();
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
        var filePath = temp.CreateFile("Context.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        model.Entities.Should().HaveCount(3);
        model.Entities.Should().Contain(e => e.Name == "User");
        model.Entities.Should().Contain(e => e.Name == "Post");
        model.Entities.Should().Contain(e => e.Name == "Comment");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldAnalyzeFirstContextWhenNameNotSpecified()
    {
        // Arrange
        using var temp = new TestDirectory();
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
        var filePath = temp.CreateFile("Context.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath);

        // Assert
        model.ContextName.Should().Be("FirstDbContext");
        model.Entities.Should().ContainSingle(e => e.Name == "User");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandlePropertiesWithDifferentTypes()
    {
        // Arrange
        using var temp = new TestDirectory();
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
        var filePath = temp.CreateFile("Context.cs", content);

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

    [Fact]
    public async Task AnalyzeContextAsync_ShouldIdentifyPrimaryKeysByConvention()
    {
        // Arrange
        using var temp = new TestDirectory();
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
        var filePath = temp.CreateFile("Context.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        var customer = model.Entities.First(e => e.Name == "Customer");
        customer.Properties.Should().Contain(p => p.Name == "Id" && p.IsPrimaryKey);
    }

    [Fact]
    public async Task DiscoverContextsAsync_ShouldHandleEmptyFile()
    {
        // Arrange
        using var temp = new TestDirectory();
        var filePath = temp.CreateFile("Empty.cs", string.Empty);

        // Act
        var contexts = await _service.DiscoverContextsAsync(filePath);

        // Assert
        contexts.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleContextWithNoDbSets()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   // No DbSets
                               }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        model.ContextName.Should().Be("AppDbContext");
        model.Entities.Should().BeEmpty();
    }
}