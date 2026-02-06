using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework;

[Trait("Category", "EntityFramework")]
public class EnumDefaultValueTests
{
    [Fact]
    public void AnalyzeContextAsync_ShouldUseEnumValueForDefaultValue()
    {
        // Arrange
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using System;

                               namespace TestNamespace
                               {
                                   public enum UserStatus
                                   {
                                       Inactive = 0,
                                       Active = 1,
                                       Pending = 2
                                   }

                                   public class User
                                   {
                                       public Guid Id { get; set; }
                                       public UserStatus Status { get; set; }
                                   }

                                   public class AppDbContext : DbContext 
                                   { 
                                       public DbSet<User> Users { get; set; }
                                       
                                       protected override void OnModelCreating(ModelBuilder modelBuilder)
                                       {
                                           modelBuilder.Entity<User>()
                                               .Property(u => u.Status)
                                               .HasDefaultValue(UserStatus.Active);
                                       }
                                   }
                               }
                               """;

        // We need to create a temporary file or use a compilation directly.
        // EfAnalysisService.AnalyzeContextAsync takes a project path or a context name.
        // For unit tests, we usually use compilation and ModelSnapshotParser or similar.

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
        var status = user.Properties.Should().Contain(p => p.Name == "Status").Which;

        // This is what failing currently: it returns "Active" but we want "1"
        status.DefaultValue.Should().Be("1");
    }

    [Fact]
    public void AnalyzeContextAsync_ShouldStillShortenNamesWhenNotResolvable()
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
                                       public DbSet<User> Users { get; set; }
                                       
                                       protected override void OnModelCreating(ModelBuilder modelBuilder)
                                       {
                                           modelBuilder.Entity<User>()
                                               .Property(u => u.Role)
                                               .HasDefaultValue(UnknownNamespace.Roles.Guest);
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

        // Should still shorten to "Guest" even if not resolvable
        role.DefaultValue.Should().Be("Guest");
    }
}