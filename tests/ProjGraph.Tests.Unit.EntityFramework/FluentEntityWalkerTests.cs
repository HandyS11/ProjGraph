using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Unit tests for <see cref="FluentEntityWalker"/>: the Roslyn fluent-chain entity/table walker that
/// replaces the regex entity-materialization and <c>ToTable</c> parsing on the DbContext path.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class FluentEntityWalkerTests
{
    /// <summary>
    /// Compiles <paramref name="source"/>, locates its OnModelCreating method, and seeds the entities
    /// dictionary/model from the named "DbSet" entity classes so the walker can be driven in isolation.
    /// The fluent-only entities under test are deliberately NOT seeded.
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
    public void Apply_FluentOnlyEntity_MaterializedFromGenericEntityCall()
    {
        const string source = """
            using System.Collections.Generic;
            public class Product { public int Id { get; set; } }
            public class Supplier { public int Id { get; set; } }
            public class ProductSupplier { public int ProductId { get; set; } public int SupplierId { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<ProductSupplier>().HasKey(ps => new { ps.ProductId, ps.SupplierId });
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Product", "Supplier");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities.Should().ContainKey("ProductSupplier");
        model.Entities.Should().ContainSingle(e => e.Name == "ProductSupplier");
        entities["ProductSupplier"].Properties.Select(p => p.Name)
            .Should().BeEquivalentTo("ProductId", "SupplierId");
    }

    [Fact]
    public void Apply_EntityAlreadyPresent_NotDuplicatedAndSameInstance()
    {
        const string source = """
            public class Blog { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Blog>().HasKey(b => b.Id);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog");
        var original = entities["Blog"];

        FluentEntityWalker.Apply(method, entities, model, compilation);

        model.Entities.Should().ContainSingle(e => e.Name == "Blog");
        entities["Blog"].Should().BeSameAs(original);
    }

    [Fact]
    public void Apply_StringEntityForm_MaterializesLastNamespaceSegment()
    {
        const string source = """
            namespace My.Ns { public class Widget { public int Id { get; set; } } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity("My.Ns.Widget");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source);

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities.Should().ContainKey("Widget");
        model.Entities.Should().ContainSingle(e => e.Name == "Widget");
    }

    [Fact]
    public void Apply_TypeReferencedOnlyInsideOwnsOne_NotMaterialized()
    {
        const string source = """
            public class Customer { public int Id { get; set; } public Address Address { get; set; } = null!; }
            public class Address { public string City { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Customer>(e =>
                    {
                        e.OwnsOne(c => c.Address, a => a.Property(p => p.City).HasMaxLength(50));
                    });
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Customer");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities.Should().NotContainKey("Address");
        model.Entities.Should().NotContain(e => e.Name == "Address");
    }

    [Fact]
    public void Apply_ToTable_SetsTableNameOnOwningEntity()
    {
        const string source = """
            public class Order { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Order>().ToTable("Orders");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities["Order"].TableName.Should().Be("Orders");
        model.Entities.Single(e => e.Name == "Order").TableName.Should().Be("Orders");
    }

    [Fact]
    public void Apply_ToTableSchemaOverload_SetsTableNameFromFirstArgument()
    {
        // Low #13: the old regex only matched single-arg .ToTable("X"); the two-arg schema overload
        // silently set no table name. The walker takes the first string-literal argument.
        const string source = """
            public class Order { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Order>().ToTable("Orders", "sales");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities["Order"].TableName.Should().Be("Orders");
    }

    [Fact]
    public void Apply_ToTableInsideOwnsOne_DoesNotLeakToOwner()
    {
        const string source = """
            public class Customer { public int Id { get; set; } public Address Address { get; set; } = null!; }
            public class Address { public string City { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Customer>(e =>
                    {
                        e.OwnsOne(c => c.Address, a => a.ToTable("Addresses"));
                    });
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Customer");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities["Customer"].TableName.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ChainedOwnsOneToTable_DoesNotLeakToOwner()
    {
        // Chained owned-type form with no builder lambda: the ToTable is a sibling after OwnsOne,
        // so it configures the owned Address table, not the owner Customer.
        const string source = """
            public class Customer { public int Id { get; set; } public Address Address { get; set; } = null!; }
            public class Address { public string City { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Customer>()
                        .OwnsOne(c => c.Address)
                        .ToTable("Addresses");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Customer");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities["Customer"].TableName.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ToTableOnFluentOnlyEntity_MaterializesAndSetsTableName()
    {
        const string source = """
            public class Product { public int Id { get; set; } }
            public class Supplier { public int Id { get; set; } }
            public class ProductSupplier { public int ProductId { get; set; } public int SupplierId { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<ProductSupplier>().ToTable("product_supplier");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Product", "Supplier");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities.Should().ContainKey("ProductSupplier");
        entities["ProductSupplier"].TableName.Should().Be("product_supplier");
        model.Entities.Single(e => e.Name == "ProductSupplier").TableName.Should().Be("product_supplier");
    }

    [Fact]
    public void Apply_AmbientEntity_RoutesBareBuilderToTableToAmbient()
    {
        const string source = """
            public class Widget { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic builder)
                {
                    builder.ToTable("widgets");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget");

        FluentEntityWalker.Apply(method, entities, model, compilation, ambientEntity: "Widget");

        entities["Widget"].TableName.Should().Be("widgets");
        model.Entities.Single(e => e.Name == "Widget").TableName.Should().Be("widgets");
    }

    [Fact]
    public void Apply_NoAmbient_BareBuilderToTableSetsNothing()
    {
        const string source = """
            public class Widget { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic builder)
                {
                    builder.ToTable("widgets");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities["Widget"].TableName.Should().BeEmpty();
    }
}
