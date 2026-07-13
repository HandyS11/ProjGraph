using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Unit tests for <see cref="FluentRelationshipWalker"/>: the Roslyn fluent-chain relationship
/// walker that replaces the regex <c>RelationshipConfigParser</c> on the DbContext path.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class FluentRelationshipWalkerTests
{
    /// <summary>
    /// Compiles <paramref name="source"/>, locates its OnModelCreating method, and builds the
    /// entities dictionary from the named entity classes so the walker can be driven in isolation.
    /// </summary>
    /// <param name="source">The C# source to compile.</param>
    /// <param name="entityNames">The entity class names to analyze into <see cref="EfEntity"/> instances.</param>
    private static (MethodDeclarationSyntax Method, Compilation Compilation, Dictionary<string, EfEntity> Entities, EfModel Model)
        Build(string source, params string[] entityNames)
    {
        var compilation = RoslynTestHelper.CreateCompilation(source);
        var method = compilation.SyntaxTrees[0].GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "OnModelCreating");

        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel();
        foreach (var name in entityNames)
        {
            var symbol = RoslynTestHelper.GetTypeSymbol(compilation, name)!;
            var entity = EntityAnalyzer.AnalyzeEntity(symbol);
            entities[name] = entity;
            model.Entities.Add(entity);
        }

        return (method, compilation, entities, model);
    }

    [Fact]
    public void Apply_HasManyWithOne_CreatesOneToManyWithoutSwap()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = []; }
            public class Post { public int Id { get; set; } public int BlogId { get; set; } public Blog Blog { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Blog>().HasMany(b => b.Posts).WithOne(p => p.Blog);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.SourceEntity.Should().Be("Blog");
        rel.TargetEntity.Should().Be("Post");
        rel.Type.Should().Be(EfRelationshipType.OneToMany);
        rel.IsRequired.Should().BeTrue();
    }

    [Fact]
    public void Apply_HasOneWithMany_SwapsSourceAndTarget()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = []; }
            public class Post { public int Id { get; set; } public Blog Blog { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Post>().HasOne(p => p.Blog).WithMany(b => b.Posts);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.SourceEntity.Should().Be("Blog");   // swapped: target of HasOne becomes source
        rel.TargetEntity.Should().Be("Post");
        rel.Type.Should().Be(EfRelationshipType.OneToMany);
    }

    [Fact]
    public void Apply_HasOneWithOne_CreatesOptionalOneToOne()
    {
        const string source = """
            public class Author { public int Id { get; set; } public Profile Profile { get; set; } = null!; }
            public class Profile { public int Id { get; set; } public Author Author { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Author>().HasOne(a => a.Profile).WithOne(p => p.Author);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Author", "Profile");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.Type.Should().Be(EfRelationshipType.OneToOne);
        rel.IsRequired.Should().BeFalse();      // convention default for 1:1
    }

    [Fact]
    public void Apply_HasOneWithoutWith_CreatesNoRelationship()
    {
        const string source = """
            public class Blog { public int Id { get; set; } public Author Owner { get; set; } = null!; }
            public class Author { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Blog>().HasOne(b => b.Owner);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Author");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        model.Relationships.Should().BeEmpty();
    }

    [Fact]
    public void Apply_EntityLambdaForm_ResolvesReceiverEntity()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = []; }
            public class Post { public int Id { get; set; } public Blog Blog { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Blog>(e =>
                    {
                        e.HasMany(b => b.Posts).WithOne(p => p.Blog);
                    });
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.SourceEntity.Should().Be("Blog");
        rel.TargetEntity.Should().Be("Post");
        rel.Type.Should().Be(EfRelationshipType.OneToMany);
    }
}
