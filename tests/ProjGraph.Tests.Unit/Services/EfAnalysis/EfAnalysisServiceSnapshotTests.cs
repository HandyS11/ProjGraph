using FluentAssertions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Services.EfAnalysis;
using ProjGraph.Tests.Unit.Helpers;

namespace ProjGraph.Tests.Unit.Services.EfAnalysis;

public class EfAnalysisServiceSnapshotTests
{
    private readonly EfAnalysisService _service = new();

    [Fact]
    public async Task DiscoverSnapshotsAsync_ShouldFindModelSnapshotInFile()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using Microsoft.EntityFrameworkCore.Infrastructure;
                               using Microsoft.EntityFrameworkCore.Metadata;
                               using Microsoft.EntityFrameworkCore.Migrations;
                               using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

                               [DbContext(typeof(TestDbContext))]
                               partial class TestDbContextModelSnapshot : ModelSnapshot
                               {
                                   protected override void BuildModel(ModelBuilder modelBuilder)
                                   {
                                   }
                               }
                               """;
        var filePath = temp.CreateFile("Snapshot.cs", content);

        // Act
        var snapshots = await _service.DiscoverSnapshotsAsync(filePath);

        // Assert
        snapshots.Should().Contain("TestDbContextModelSnapshot");
    }

    [Fact]
    public async Task AnalyzeSnapshotAsync_ShouldExtractEntitiesAndProperties()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using Microsoft.EntityFrameworkCore.Infrastructure;
                               using Microsoft.EntityFrameworkCore.Metadata;
                               using Microsoft.EntityFrameworkCore.Migrations;
                               using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

                               [DbContext(typeof(TestDbContext))]
                               partial class TestDbContextModelSnapshot : ModelSnapshot
                               {
                                   protected override void BuildModel(ModelBuilder modelBuilder)
                                   {
                                       modelBuilder.Entity("TestNamespace.Blog", b =>
                                           {
                                               b.Property<int>("Id")
                                                   .ValueGeneratedOnAdd()
                                                   .HasColumnType("int");

                                               b.Property<string>("Name")
                                                   .IsRequired()
                                                   .HasColumnType("nvarchar(max)");

                                               b.HasKey("Id");

                                               b.ToTable("Blogs");
                                           });
                                   }
                               }
                               """;
        var filePath = temp.CreateFile("Snapshot.cs", content);

        // Act
        var model = await _service.AnalyzeSnapshotAsync(filePath, "TestDbContextModelSnapshot");

        // Assert
        model.ContextName.Should().Be("TestDbContext");
        model.Entities.Should().ContainSingle(e => e.Name == "Blog");
        var blog = model.Entities.First(e => e.Name == "Blog");
        blog.TableName.Should().Be("Blogs");
        blog.Properties.Should().Contain(p => p.Name == "Id");
        blog.Properties.Should().Contain(p => p.Name == "Name" && p.IsRequired);
    }

    [Fact]
    public async Task AnalyzeSnapshotAsync_ShouldExtractRelationships()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               using Microsoft.EntityFrameworkCore.Infrastructure;
                               using Microsoft.EntityFrameworkCore.Metadata;
                               using Microsoft.EntityFrameworkCore.Migrations;
                               using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

                               [DbContext(typeof(TestDbContext))]
                               partial class TestDbContextModelSnapshot : ModelSnapshot
                               {
                                   protected override void BuildModel(ModelBuilder modelBuilder)
                                   {
                                       modelBuilder.Entity("TestNamespace.Blog", b =>
                                           {
                                               b.Property<int>("Id").HasColumnType("int");
                                               b.HasKey("Id");
                                           });

                                       modelBuilder.Entity("TestNamespace.Post", b =>
                                           {
                                               b.Property<int>("Id").HasColumnType("int");
                                               b.Property<int>("BlogId").HasColumnType("int");
                                               b.HasKey("Id");
                                               
                                               b.HasOne("TestNamespace.Blog", "Blog")
                                                   .WithMany("Posts")
                                                   .HasForeignKey("BlogId")
                                                   .OnDelete(DeleteBehavior.Cascade)
                                                   .IsRequired();
                                           });
                                   }
                               }
                               """;
        var filePath = temp.CreateFile("Snapshot.cs", content);

        // Act
        var model = await _service.AnalyzeSnapshotAsync(filePath, "TestDbContextModelSnapshot");

        // Assert
        model.Relationships.Should().ContainSingle();
        var rel = model.Relationships.First();
        rel.SourceEntity.Should().Be("Blog"); // Because it's OneToMany from Blog to Post
        rel.TargetEntity.Should().Be("Post");
        rel.Type.Should().Be(EfRelationshipType.OneToMany);
    }
}
