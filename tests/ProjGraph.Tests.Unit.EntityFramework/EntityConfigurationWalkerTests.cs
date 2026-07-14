using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Unit tests for <see cref="EntityConfigurationWalker"/>: discovery of <c>IEntityTypeConfiguration&lt;T&gt;</c>
/// classes referenced from <c>OnModelCreating</c> and folding of their <c>Configure</c> bodies into the model
/// via the ambient-entity walkers.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class EntityConfigurationWalkerTests
{
    /// <summary>
    /// Compiles <paramref name="source"/>, locates its OnModelCreating method, and seeds the entities
    /// dictionary/model from the named DbSet entity classes so the orchestrator can be driven in isolation.
    /// </summary>
    /// <param name="source">The C# source to compile.</param>
    /// <param name="seededEntityNames">Entity class names to pre-analyze (simulating DbSet discovery).</param>
    private static (MethodDeclarationSyntax Method, Compilation Compilation, Dictionary<string, EfEntity> Entities, EfModel Model)
        Build(string source, params string[] seededEntityNames)
    {
        var compilation = RoslynTestHelper.CreateCompilation(source);
        var method = compilation.SyntaxTrees[0].GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "OnModelCreating");

        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel();
        foreach (var name in seededEntityNames)
        {
            var symbol = RoslynTestHelper.GetTypeSymbol(compilation, name)!;
            var entity = EntityAnalyzer.AnalyzeEntity(symbol);
            entities[name] = entity;
            model.Entities.Add(entity);
        }

        return (method, compilation, entities, model);
    }

    [Fact]
    public void Apply_ApplyConfiguration_FoldsPropertyConfigIntoSeededEntity()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class WidgetConfiguration : IEntityTypeConfiguration<Widget>
            {
                public void Configure(EntityTypeBuilder<Widget> builder)
                {
                    builder.HasKey(w => w.Id);
                    builder.Property(w => w.Name).IsRequired().HasMaxLength(120);
                }
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfiguration(new WidgetConfiguration());
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        var name = entities["Widget"].Properties.Single(p => p.Name == "Name");
        name.IsRequired.Should().BeTrue();
        name.MaxLength.Should().Be(120);
        entities["Widget"].Properties.Single(p => p.Name == "Id").IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_ApplyConfiguration_ConfiguresRelationship()
    {
        const string source = """
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = new(); }
            public class Post { public int Id { get; set; } public Blog Blog { get; set; } = null!; }
            public class BlogConfiguration : IEntityTypeConfiguration<Blog>
            {
                public void Configure(EntityTypeBuilder<Blog> builder)
                    => builder.HasMany(b => b.Posts).WithOne(p => p.Blog);
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfiguration(new BlogConfiguration());
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        model.Relationships.Should().ContainSingle(r => r.SourceEntity == "Blog" && r.TargetEntity == "Post");
    }

    [Fact]
    public void Apply_ConfigOnlyEntity_MaterializedFromConfiguration()
    {
        // Gadget has no DbSet and is not seeded; its configuration alone must materialize it.
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Gadget { public int Id { get; set; } public string Label { get; set; } = ""; }
            public class GadgetConfiguration : IEntityTypeConfiguration<Gadget>
            {
                public void Configure(EntityTypeBuilder<Gadget> builder)
                {
                    builder.HasKey(g => g.Id);
                    builder.Property(g => g.Label).HasMaxLength(40);
                }
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfiguration(new GadgetConfiguration());
            }
            """;
        var (method, compilation, entities, model) = Build(source);

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities.Should().ContainKey("Gadget");
        model.Entities.Should().ContainSingle(e => e.Name == "Gadget");
        entities["Gadget"].Properties.Single(p => p.Name == "Label").MaxLength.Should().Be(40);
    }

    [Fact]
    public void Apply_ApplyConfigurationsFromAssembly_AppliesAllConfigClasses()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Gadget { public int Id { get; set; } public string Label { get; set; } = ""; }
            public class WidgetConfiguration : IEntityTypeConfiguration<Widget>
            {
                public void Configure(EntityTypeBuilder<Widget> builder)
                    => builder.Property(w => w.Name).HasMaxLength(120);
            }
            public class GadgetConfiguration : IEntityTypeConfiguration<Gadget>
            {
                public void Configure(EntityTypeBuilder<Gadget> builder)
                    => builder.Property(g => g.Label).HasMaxLength(40);
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfigurationsFromAssembly(typeof(Ctx).Assembly);
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget", "Gadget");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities["Widget"].Properties.Single(p => p.Name == "Name").MaxLength.Should().Be(120);
        entities["Gadget"].Properties.Single(p => p.Name == "Label").MaxLength.Should().Be(40);
    }

    [Fact]
    public void Apply_ExplicitConfiguration_DoesNotApplyOtherConfigClasses()
    {
        // Only WidgetConfiguration is applied; GadgetConfiguration is present but not referenced.
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Gadget { public int Id { get; set; } public string Label { get; set; } = ""; }
            public class WidgetConfiguration : IEntityTypeConfiguration<Widget>
            {
                public void Configure(EntityTypeBuilder<Widget> builder)
                    => builder.Property(w => w.Name).HasMaxLength(120);
            }
            public class GadgetConfiguration : IEntityTypeConfiguration<Gadget>
            {
                public void Configure(EntityTypeBuilder<Gadget> builder)
                    => builder.Property(g => g.Label).HasMaxLength(40);
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfiguration(new WidgetConfiguration());
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget", "Gadget");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities["Widget"].Properties.Single(p => p.Name == "Name").MaxLength.Should().Be(120);
        entities["Gadget"].Properties.Single(p => p.Name == "Label").MaxLength.Should().BeNull();
    }

    [Fact]
    public void Apply_NoConfiguration_IsNoOp()
    {
        const string source = """
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity("Widget");
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities["Widget"].Properties.Single(p => p.Name == "Name").MaxLength.Should().BeNull();
        model.Entities.Should().ContainSingle(e => e.Name == "Widget");
    }
}
