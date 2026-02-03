using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Services.EfAnalysis;

namespace ProjGraph.Tests.Unit.Services.EfAnalysis;

public class ConstStringDefaultValueTests
{
    [Fact]
    public void AnalyzeContextAsync_ShouldUseConstStringValueForDefaultValue()
    {
        // Arrange
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using System;

                               namespace TestNamespace
                               {
                                   public static class AppConstants
                                   {
                                       public const string DefaultRole = "GuestUser";
                                   }

                                   public class User
                                   {
                                       public Guid Id { get; set; }
                                       public string Role { get; set; }
                                   }

                                   public class AppDbContext : DbContext 
                                   { 
                                       public DbSet<User> Users { get; set; }
                                       
                                       protected override void OnModelCreating(ModelBuilder modelBuilder)
                                       {
                                           modelBuilder.Entity<User>()
                                               .Property(u => u.Role)
                                               .HasDefaultValue(AppConstants.DefaultRole);
                                       }
                                   }
                               }
                               """;

        var syntaxTree = CSharpSyntaxTree.ParseText(content);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddReferences(
                MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        var contextType = compilation.GetTypeByMetadataName("TestNamespace.AppDbContext");
        contextType.Should().NotBeNull();
        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel { ContextName = "AppDbContext" };

        // Act
        FluentApiConfigurationParser.ApplyFluentApiConstraints(contextType, entities, model, compilation);

        // Assert
        var user = model.Entities.Should().Contain(e => e.Name == "User").Which;
        var role = user.Properties.Should().Contain(p => p.Name == "Role").Which;

        role.DefaultValue.Should().Be("GuestUser");
    }

    [Fact]
    public void AnalyzeContextAsync_ShouldUseConstStringValueForNestedConstDefaultValue()
    {
        // Arrange
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using System;

                               namespace TestNamespace
                               {
                                   public static class Outer
                                   {
                                       public static class Inner
                                       {
                                           public const string DefaultRole = "NestedGuest";
                                       }
                                   }

                                   public class User
                                   {
                                       public Guid Id { get; set; }
                                       public string Role { get; set; }
                                   }

                                   public class AppDbContext : DbContext 
                                   { 
                                       public DbSet<User> Users { get; set; }
                                       
                                       protected override void OnModelCreating(ModelBuilder modelBuilder)
                                       {
                                           modelBuilder.Entity<User>()
                                               .Property(u => u.Role)
                                               .HasDefaultValue(Outer.Inner.DefaultRole);
                                       }
                                   }
                               }
                               """;

        var syntaxTree = CSharpSyntaxTree.ParseText(content);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddReferences(
                MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        var contextType = compilation.GetTypeByMetadataName("TestNamespace.AppDbContext");
        contextType.Should().NotBeNull();
        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel { ContextName = "AppDbContext" };

        // Act
        FluentApiConfigurationParser.ApplyFluentApiConstraints(contextType, entities, model, compilation);

        // Assert
        var user = model.Entities.Should().Contain(e => e.Name == "User").Which;
        var role = user.Properties.Should().Contain(p => p.Name == "Role").Which;

        role.DefaultValue.Should().Be("NestedGuest");
    }

    [Fact]
    public void AnalyzeContextAsync_ShouldUseConstStringValueForSimpleIdentifierDefaultValue()
    {
        // Arrange
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using System;

                               namespace TestNamespace
                               {
                                   public class User
                                   {
                                       public Guid Id { get; set; }
                                       public string Role { get; set; }
                                   }

                                   public class AppDbContext : DbContext 
                                   { 
                                       public const string DefaultRole = "SimpleGuest";
                                       public DbSet<User> Users { get; set; }
                                       
                                       protected override void OnModelCreating(ModelBuilder modelBuilder)
                                       {
                                           modelBuilder.Entity<User>()
                                               .Property(u => u.Role)
                                               .HasDefaultValue(DefaultRole);
                                       }
                                   }
                               }
                               """;

        var syntaxTree = CSharpSyntaxTree.ParseText(content);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddReferences(
                MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        var contextType = compilation.GetTypeByMetadataName("TestNamespace.AppDbContext");
        contextType.Should().NotBeNull();
        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel { ContextName = "AppDbContext" };

        // Act
        FluentApiConfigurationParser.ApplyFluentApiConstraints(contextType, entities, model, compilation);

        // Assert
        var user = model.Entities.Should().Contain(e => e.Name == "User").Which;
        var role = user.Properties.Should().Contain(p => p.Name == "Role").Which;

        role.DefaultValue.Should().Be("SimpleGuest");
    }
}