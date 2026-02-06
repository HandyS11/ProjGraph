using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Lib.EntityFramework.Rendering;
using Projgraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

[Trait("Category", "EntityFramework")]
public class EfAnalysisAdvancedTests
{
    private readonly EfAnalysisService _service = CreateService();
    private readonly MermaidErdRenderer _renderer = new();

    private static EfAnalysisService CreateService()
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs);
        return new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs)
        );
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleManyToManyRelationships()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using System.Collections.Generic;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Student> Students { get; set; }
                                   public DbSet<Course> Courses { get; set; }
                               }
                               public class Student { 
                                   public int Id { get; set; } 
                                   public ICollection<Course> Courses { get; set; }
                               }
                               public class Course { 
                                   public int Id { get; set; } 
                                   public ICollection<Student> Students { get; set; }
                               }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        model.Entities.Should().Contain(e => e.IsJoinEntity && e.Name == "CourseStudent");
        model.Relationships.Should().Contain(r => r.SourceEntity == "Student" && r.TargetEntity == "CourseStudent");
        model.Relationships.Should().Contain(r => r.SourceEntity == "Course" && r.TargetEntity == "CourseStudent");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleFluentApiPropertyConfigurations()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<User> Users { get; set; }
                                   
                                   protected override void OnModelCreating(ModelBuilder modelBuilder)
                                   {
                                       modelBuilder.Entity<User>()
                                           .Property(u => u.Username)
                                           .IsRequired()
                                           .HasMaxLength(50);
                                           
                                       modelBuilder.Entity<User>()
                                           .Property(u => u.Score)
                                           .HasDefaultValue(100);
                                   }
                               }
                               public class User { 
                                   public int Id { get; set; } 
                                   public string Username { get; set; }
                                   public int Score { get; set; }
                               }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        var user = model.Entities.First(e => e.Name == "User");
        var username = user.Properties.First(p => p.Name == "Username");
        username.IsRequired.Should().BeTrue();
        username.MaxLength.Should().Be(50);

        var score = user.Properties.First(p => p.Name == "Score");
        score.DefaultValue.Should().Be("100");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleFluentApiShadowRelationships()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Blog> Blogs { get; set; }
                                   // Post is not in a DbSet initially
                                   
                                   protected override void OnModelCreating(ModelBuilder modelBuilder)
                                   {
                                       modelBuilder.Entity<Blog>()
                                           .HasMany<Post>()
                                           .WithOne();
                                           
                                       modelBuilder.Entity<Post>()
                                           .HasOne<Blog>()
                                           .WithMany();
                                   }
                               }
                               public class Blog { public int Id { get; set; } }
                               public class Post { public int Id { get; set; } }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        // Act
        // We need to make sure Post is discovered. Currently, DiscoverEntitiesFromDbSets only looks at DbSets.
        // If it's not in DbSet, it might not be analyzed unless something else triggers it.
        // Wait, the current implementation of EntityAnalyzer might not find it if it's not in a DbSet.
        // But FluentApiConfigurationParser might use it.
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        model.Entities.Should().Contain(e => e.Name == "Post");
        model.Relationships.Should().Contain(r =>
            r.SourceEntity == "Blog" && r.TargetEntity == "Post" && r.Type == EfRelationshipType.OneToMany);
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleFluentApiPrecisionConfig()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Product> Products { get; set; }
                                   
                                   protected override void OnModelCreating(ModelBuilder modelBuilder)
                                   {
                                       modelBuilder.Entity<Product>()
                                           .Property(p => p.Price)
                                           .HasPrecision(18, 2);
                                   }
                               }
                               public class Product { 
                                   public int Id { get; set; } 
                                   public decimal Price { get; set; }
                               }
                               """;
        var filePath = temp.CreateFile("ContextPrecision.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        var product = model.Entities.First(e => e.Name == "Product");
        var price = product.Properties.First(p => p.Name == "Price");
        price.Precision.Should().Be(18);
        price.Scale.Should().Be(2);
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleInheritance()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string baseContent = """
                                   namespace Test;
                                   public abstract class BaseEntity { public int Id { get; set; } }
                                   """;
        const string entityContent = """
                                     using Microsoft.EntityFrameworkCore;
                                     namespace Test;
                                     public class AppDbContext : DbContext 
                                     { 
                                         public DbSet<User> Users { get; set; }
                                     }
                                     public class User : BaseEntity { public string Name { get; set; } }
                                     """;
        temp.CreateFile("BaseEntity.cs", baseContent);
        var filePath = temp.CreateFile("ContextInheritance.cs", entityContent);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        var user = model.Entities.First(e => e.Name == "User");
        user.Properties.Should().Contain(p => p.Name == "Id");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleInheritanceInSameFile()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Manager> Managers { get; set; }
                               }
                               public abstract class Employee { public int Id { get; set; } }
                               public class Manager : Employee { public string Department { get; set; } }
                               """;
        var filePath = temp.CreateFile("ContextSameFile.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        var manager = model.Entities.First(e => e.Name == "Manager");
        manager.Properties.Should().Contain(p => p.Name == "Id");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleShadowEntityWithProperties()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   protected override void OnModelCreating(ModelBuilder modelBuilder)
                                   {
                                       modelBuilder.Entity<AuditLog>(eb =>
                                       {
                                           eb.Property(b => b.Message).IsRequired().HasMaxLength(500);
                                       });
                                   }
                               }
                               public class AuditLog { public int Id { get; set; } public string Message { get; set; } }
                               """;
        var filePath = temp.CreateFile("ContextShadowProps.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        var log = model.Entities.Should().Contain(e => e.Name == "AuditLog").Which;
        var msg = log.Properties.Should().Contain(p => p.Name == "Message").Which;
        msg.IsRequired.Should().BeTrue();
        msg.MaxLength.Should().Be(500);
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldShortenDefaultValueNamespaces()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using System;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Assistant> Assistants { get; set; }
                                   
                                   protected override void OnModelCreating(ModelBuilder modelBuilder)
                                   {
                                       modelBuilder.Entity<Assistant>()
                                           .Property(a => a.AiProviderName)
                                           .HasDefaultValue(Shared.Constants.Constants.AiProviderModels.AzureOpenAi);
                                           
                                       modelBuilder.Entity<Assistant>()
                                           .Property(a => a.Temperature)
                                           .HasDefaultValue(0.7f);
                                           
                                       modelBuilder.Entity<Assistant>()
                                           .Property(a => a.CreatedAt)
                                           .HasDefaultValueSql("GETUTCDATE()");
                                           
                                       modelBuilder.Entity<Assistant>().ToTable("tbl_Assistants");
                                   }
                               }
                               public class Assistant { 
                                   public Guid Id { get; set; }
                                   public string AiProviderName { get; set; }
                                   public float Temperature { get; set; }
                                   public DateTime CreatedAt { get; set; }
                               }
                               """;
        var filePath = temp.CreateFile("ContextDefaultValues.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        var assistant = model.Entities.First(e => e.Name == "Assistant");
        assistant.TableName.Should().Be("tbl_Assistants");

        var provider = assistant.Properties.First(p => p.Name == "AiProviderName");
        provider.DefaultValue.Should().Be("AzureOpenAi");

        var tempProp = assistant.Properties.First(p => p.Name == "Temperature");
        tempProp.DefaultValue.Should().Be("0.7f");

        var created = assistant.Properties.First(p => p.Name == "CreatedAt");
        created.DefaultValue.Should().Be("GETUTCDATE()");
    }

    [Fact]
    public void MermaidErdRenderer_ShouldRenderShortenedDefaultValues()
    {
        // Arrange
        var model = new EfModel
        {
            Entities =
            [
                new EfEntity
                {
                    Name = "Assistant",
                    Properties =
                    [
                        new EfProperty
                        {
                            Name = "AiProviderName",
                            Type = "string",
                            DefaultValue = "AzureOpenAi",
                            IsRequired = true,
                            IsExplicitlyRequired = true
                        }
                    ]
                }
            ]
        };

        // Act
        var result = _renderer.Render(model);

        // Assert
        result.Should().Contain("AiProviderName \"required, default:AzureOpenAi\"");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldMarkCompositePrimaryKeyAsForeignKey()
    {
        // This test verifies that when a property is part of a composite primary key
        // AND is also a foreign key (configured via Fluent API with lambda expressions),
        // both the PK and FK flags are correctly set.
        // Bug: Previously, lambda expressions like HasMany(a => a.Options) were not parsed,
        // causing the FK flag to be missing even though the PK flag was set.

        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using System;
                               using System.Collections.Generic;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Activity> Activities { get; set; }
                                   public DbSet<ActivityStep> ActivitySteps { get; set; }
                                   public DbSet<ActivityOption> ActivityOptions { get; set; }
                                   
                                   protected override void OnModelCreating(ModelBuilder modelBuilder)
                                   {
                                       modelBuilder.Entity<Activity>(e =>
                                       {
                                           e.HasMany(a => a.Steps)
                                               .WithOne()
                                               .HasForeignKey(s => s.ActivityId);
                                       })
                                       .Entity<ActivityStep>(e =>
                                       {
                                           e.HasMany(a => a.Options)
                                               .WithOne()
                                               .HasForeignKey(o => o.StepId);
                                       })
                                       .Entity<ActivityOption>(e =>
                                       {
                                           e.HasKey(o => new { o.StepId, o.Value });
                                       });
                                   }
                               }
                               public class Activity { 
                                   public Guid Id { get; set; }
                                   public List<ActivityStep> Steps { get; set; }
                               }
                               public class ActivityStep { 
                                   public Guid Id { get; set; }
                                   public Guid ActivityId { get; set; }
                                   public List<ActivityOption> Options { get; set; }
                               }
                               public class ActivityOption { 
                                   public Guid StepId { get; set; }
                                   public string Value { get; set; }
                               }
                               """;
        var filePath = temp.CreateFile("ContextCompositePK.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        var activityOption = model.Entities.First(e => e.Name == "ActivityOption");
        var stepId = activityOption.Properties.First(p => p.Name == "StepId");
        var value = activityOption.Properties.First(p => p.Name == "Value");

        // Both should be marked as PK
        stepId.IsPrimaryKey.Should().BeTrue("StepId is part of composite PK");
        value.IsPrimaryKey.Should().BeTrue("Value is part of composite PK");

        // StepId should also be marked as FK
        stepId.IsForeignKey.Should().BeTrue("StepId is a foreign key to ActivityStep");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldNotCreateDuplicateEntitiesOrRelationships()
    {
        // This test verifies that entities and relationships are not duplicated in the model
        // even when they are defined in multiple places (DbSets, Fluent API, navigation properties)

        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using System;
                               using System.Collections.Generic;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<Group> Groups { get; set; }
                                   public DbSet<User> Users { get; set; }
                                   public DbSet<GroupUser> GroupUsers { get; set; }
                                   
                                   protected override void OnModelCreating(ModelBuilder modelBuilder)
                                   {
                                       modelBuilder.Entity<GroupUser>(e =>
                                       {
                                           e.HasKey(gu => new { gu.GroupId, gu.UserId });
                                           
                                           e.HasOne<Group>()
                                               .WithMany()
                                               .HasForeignKey(gu => gu.GroupId);
                                               
                                           e.HasOne<User>()
                                               .WithMany()
                                               .HasForeignKey(gu => gu.UserId);
                                       });
                                   }
                               }
                               public class Group { 
                                   public Guid Id { get; set; }
                                   public List<GroupUser> GroupUsers { get; set; }
                               }
                               public class User { 
                                   public Guid Id { get; set; }
                                   public List<GroupUser> GroupUsers { get; set; }
                               }
                               public class GroupUser { 
                                   public Guid GroupId { get; set; }
                                   public Guid UserId { get; set; }
                               }
                               """;
        var filePath = temp.CreateFile("ContextNoDuplicates.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert - No duplicate entities
        var entityNames = model.Entities.Select(e => e.Name).ToList();
        var uniqueEntityNames = entityNames.Distinct().ToList();
        entityNames.Should().BeEquivalentTo(uniqueEntityNames, "there should be no duplicate entities");

        // Assert - No duplicate relationships
        var relationshipKeys = model.Relationships
            .Select(r => $"{r.SourceEntity}->{r.TargetEntity}:{r.Type}")
            .ToList();
        var uniqueRelationshipKeys = relationshipKeys.Distinct().ToList();
        relationshipKeys.Should().BeEquivalentTo(uniqueRelationshipKeys, "there should be no duplicate relationships");

        // Assert - Specific checks
        model.Entities.Count(e => e.Name == "GroupUser").Should().Be(1, "GroupUser should appear only once");
        model.Entities.Count(e => e.Name == "Group").Should().Be(1, "Group should appear only once");
        model.Entities.Count(e => e.Name == "User").Should().Be(1, "User should appear only once");
    }

    [Fact]
    public async Task AnalyzeContextAsync_ShouldHandleSelfReferencingEntity()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using System;
                               namespace Test;
                               public class AppDbContext : DbContext 
                               { 
                                   public DbSet<PermissionEntry> Permissions { get; set; }
                               }
                               public class PermissionEntry {
                                   public Guid Id { get; set; }
                                   public Guid? ParentPermissionId { get; set; }
                                   public virtual PermissionEntry? ParentPermission { get; set; }
                               }
                               """;
        var filePath = temp.CreateFile("SelfRef.cs", content);

        // Act
        var model = await _service.AnalyzeContextAsync(filePath, "AppDbContext");

        // Assert
        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.SourceEntity.Should().Be("PermissionEntry");
        rel.TargetEntity.Should().Be("PermissionEntry");
        rel.Type.Should().Be(EfRelationshipType.OneToMany);
    }
}