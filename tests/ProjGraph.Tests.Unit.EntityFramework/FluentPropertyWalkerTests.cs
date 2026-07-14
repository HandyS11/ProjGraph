using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Unit tests for <see cref="FluentPropertyWalker"/>: the Roslyn fluent-chain property/key walker that
/// replaced the retired regex property parser.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class FluentPropertyWalkerTests
{
    /// <summary>
    /// Compiles <paramref name="source"/>, locates its OnModelCreating method, and builds the entities
    /// dictionary from the named entity classes so the walker can be driven in isolation.
    /// </summary>
    /// <param name="source">The C# source to compile.</param>
    /// <param name="entityNames">The names of the entity classes to analyze into <see cref="EfEntity"/> instances.</param>
    private static (MethodDeclarationSyntax Method, Compilation Compilation, Dictionary<string, EfEntity> Entities)
        Build(string source, params string[] entityNames)
    {
        var compilation = RoslynTestHelper.CreateCompilation(source);
        var method = compilation.SyntaxTrees[0].GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "OnModelCreating");

        var entities = new Dictionary<string, EfEntity>();
        foreach (var name in entityNames)
        {
            var symbol = RoslynTestHelper.GetTypeSymbol(compilation, name)!;
            entities[name] = EntityAnalyzer.AnalyzeEntity(symbol);
        }

        return (method, compilation, entities);
    }

    private static EfProperty Property(Dictionary<string, EfEntity> entities, string entity, string property)
        => entities[entity].Properties.Single(p => p.Name == property);

    [Fact]
    public void Apply_LambdaForm_AppliesRequiredAndMaxLength()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e =>
                    {
                        e.Property(a => a.Name).IsRequired().HasMaxLength(200);
                    });
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        var name = Property(entities, "Account", "Name");
        name.IsRequired.Should().BeTrue();
        name.MaxLength.Should().Be(200);
    }

    [Fact]
    public void Apply_ChainForm_ResolvesEntityFromEntityCall()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Account>().Property(a => a.Name).HasMaxLength(50);
                }
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "Name").MaxLength.Should().Be(50);
    }

    [Fact]
    public void Apply_HasPrecision_SetsPrecisionAndScale()
    {
        const string source = """
            public class Account { public int Id { get; set; } public decimal Balance { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => a.Balance).HasPrecision(18, 2));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        var balance = Property(entities, "Account", "Balance");
        balance.Precision.Should().Be(18);
        balance.Scale.Should().Be(2);
    }

    [Fact]
    public void Apply_HasColumnType_InfersMaxLengthFromParens()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Code { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => a.Code).HasColumnType("char(8)"));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "Code").MaxLength.Should().Be(8);
    }

    [Fact]
    public void Apply_HasDefaultValue_ResolvesEnumConstant()
    {
        const string source = """
            public enum Status { Inactive = 0, Active = 1 }
            public class User { public int Id { get; set; } public Status Status { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<User>(e => e.Property(u => u.Status).HasDefaultValue(Status.Active));
            }
            """;
        var (method, compilation, entities) = Build(source, "User");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "User", "Status").DefaultValue.Should().Be("1");
    }

    [Fact]
    public void Apply_HasDefaultValueSql_TrimsQuotes()
    {
        const string source = """
            public class Account { public int Id { get; set; } public System.DateTime CreatedAt { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => a.CreatedAt).HasDefaultValueSql("GETUTCDATE()"));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "CreatedAt").DefaultValue.Should().Be("GETUTCDATE()");
    }

    [Fact]
    public void Apply_PropertyInsideOwnsOne_IsIgnored()
    {
        // The regex parser absorbed nested Property calls (single-level arg capture) so they never
        // reached the outer entity; the walker must skip them too (owned types are Slice 3).
        const string source = """
            public class Address { public string City { get; set; } = ""; }
            public class Customer { public int Id { get; set; } public Address Address { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Customer>(e =>
                    {
                        e.OwnsOne(c => c.Address, a => a.Property(p => p.City).HasMaxLength(50));
                    });
            }
            """;
        var (method, compilation, entities) = Build(source, "Customer");

        FluentPropertyWalker.Apply(method, entities, compilation);

        entities["Customer"].Properties.Should().NotContain(p => p.Name == "City");
    }

    [Fact]
    public void Apply_ChainedOwnsOneProperty_DoesNotLeakOntoOwner()
    {
        // The chained owned-type form used throughout the EF docs: OwnsOne is not given a builder
        // lambda, so the Property call is a sibling after it rather than inside its argument list.
        // The owned City property must not land on the owner Customer entity.
        const string source = """
            public class Address { public string City { get; set; } = ""; }
            public class Customer { public int Id { get; set; } public Address Address { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Customer>()
                        .OwnsOne(c => c.Address)
                        .Property(a => a.City).HasMaxLength(50);
            }
            """;
        var (method, compilation, entities) = Build(source, "Customer");

        FluentPropertyWalker.Apply(method, entities, compilation);

        entities["Customer"].Properties.Should().NotContain(p => p.Name == "City");
    }

    [Fact]
    public void Apply_UnknownEntity_DoesNothing()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Unknown>(e => e.Property(a => a.Name).HasMaxLength(10));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        var act = () => FluentPropertyWalker.Apply(method, entities, compilation);

        act.Should().NotThrow();
        entities["Account"].Properties.Should().NotContain(p => p.Name == "Name" && p.MaxLength == 10);
    }

    [Fact]
    public void Apply_ParenthesizedLambda_AppliesConfig()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property((a) => a.Name).HasMaxLength(75));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "Name").MaxLength.Should().Be(75);
    }

    [Fact]
    public void Apply_HasKey_SingleProperty_MarksPrimaryKey()
    {
        const string source = """
            public class Account { public int Id { get; set; } public int LegacyId { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.HasKey(a => a.LegacyId));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "LegacyId").IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_HasKey_CompositeAnonymousObject_MarksAllPrimaryKeys()
    {
        const string source = """
            public class ProductSupplier { public int ProductId { get; set; } public int SupplierId { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<ProductSupplier>().HasKey(ps => new { ps.ProductId, ps.SupplierId });
            }
            """;
        var (method, compilation, entities) = Build(source, "ProductSupplier");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "ProductSupplier", "ProductId").IsPrimaryKey.Should().BeTrue();
        Property(entities, "ProductSupplier", "SupplierId").IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_HasKey_StringArray_MarksAllPrimaryKeys()
    {
        // The regex parser scanned string literals anywhere in the args, so array forms must work too.
        const string source = """
            public class ProductSupplier { public int ProductId { get; set; } public int SupplierId { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<ProductSupplier>().HasKey(new[] { "ProductId", "SupplierId" });
            }
            """;
        var (method, compilation, entities) = Build(source, "ProductSupplier");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "ProductSupplier", "ProductId").IsPrimaryKey.Should().BeTrue();
        Property(entities, "ProductSupplier", "SupplierId").IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_HasKeyInsideUsingEntity_IsIgnored()
    {
        // Join-entity key config inside UsingEntity belongs to the join builder, not the outer entity (Slice 3).
        const string source = """
            using System.Collections.Generic;
            public class Product { public int Id { get; set; } public List<Customer> Customers { get; set; } = []; }
            public class Customer { public int Id { get; set; } public List<Product> Products { get; set; } = []; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Customer>(e =>
                        e.HasMany(c => c.Products).WithMany(p => p.Customers)
                            .UsingEntity<Dictionary<string, object>>("CustomerProduct",
                                j => j.HasOne<Product>().WithMany().HasForeignKey("ProductId"),
                                j => j.HasOne<Customer>().WithMany().HasForeignKey("CustomerId"),
                                j => j.HasKey("ProductId", "CustomerId")));
            }
            """;
        var (method, compilation, entities) = Build(source, "Customer", "Product");

        FluentPropertyWalker.Apply(method, entities, compilation);

        entities["Customer"].Properties.Should().NotContain(p => p.Name == "ProductId");
        entities["Customer"].Properties.Should().NotContain(p => p.Name == "CustomerId");
    }

    [Fact]
    public void Apply_HasKey_ParenthesizedLambda_MarksPrimaryKey()
    {
        const string source = """
            public class Account { public int Id { get; set; } public int LegacyId { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.HasKey((a) => a.LegacyId));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "LegacyId").IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_AmbientEntity_RoutesBareBuilderChainToAmbient()
    {
        // A config class's Configure(EntityTypeBuilder<Widget> builder) body: chains are rooted at the
        // bare `builder` parameter with no Entity<T>() call, so the owning entity is the ambient T.
        const string source = """
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic builder)
                {
                    builder.HasKey(w => w.Id);
                    builder.Property(w => w.Name).IsRequired().HasMaxLength(120);
                }
            }
            """;
        var (method, compilation, entities) = Build(source, "Widget");

        FluentPropertyWalker.Apply(method, entities, compilation, ambientEntity: "Widget");

        var name = Property(entities, "Widget", "Name");
        name.IsRequired.Should().BeTrue();
        name.MaxLength.Should().Be(120);
        Property(entities, "Widget", "Id").IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_NoAmbient_BareBuilderChainAppliesNothing()
    {
        const string source = """
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic builder)
                {
                    builder.Property(w => w.Name).HasMaxLength(120);
                }
            }
            """;
        var (method, compilation, entities) = Build(source, "Widget");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Widget", "Name").MaxLength.Should().BeNull();
    }

    [Fact]
    public void Apply_SnapshotStringForm_UsesGenericTypeAndAppliesChain()
    {
        // The generated-ModelSnapshot shape: string entity, Property<T>("name") with generic type and
        // string-literal name, unknown generated calls (ValueGeneratedOnAdd) as no-ops in the chain.
        const string source = """
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity("SnapFx.Journal", b =>
                    {
                        b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
                        b.Property<string>("Name").IsRequired().HasMaxLength(200);
                        b.HasKey("Id");
                    });
                }
            }
            """;
        var (method, compilation, entities) = Build(source);
        entities["Journal"] = new EfEntity { Name = "Journal" };

        FluentPropertyWalker.Apply(method, entities, compilation);

        var id = Property(entities, "Journal", "Id");
        id.Type.Should().Be("int");
        id.IsPrimaryKey.Should().BeTrue();
        var name = Property(entities, "Journal", "Name");
        name.Type.Should().Be("string");
        name.IsRequired.Should().BeTrue();
        name.MaxLength.Should().Be(200);
    }

    [Fact]
    public void Apply_HasKeyStringLiterals_MarksCompositePrimaryKey()
    {
        const string source = """
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity("SnapFx.ShipmentItem", b =>
                    {
                        b.Property<int>("OrderId");
                        b.Property<int>("ProductId");
                        b.HasKey("OrderId", "ProductId");
                    });
                }
            }
            """;
        var (method, compilation, entities) = Build(source);
        entities["ShipmentItem"] = new EfEntity { Name = "ShipmentItem" };

        FluentPropertyWalker.Apply(method, entities, compilation);

        entities["ShipmentItem"].Properties.Should().HaveCount(2);
        Property(entities, "ShipmentItem", "OrderId").IsPrimaryKey.Should().BeTrue();
        Property(entities, "ShipmentItem", "ProductId").IsPrimaryKey.Should().BeTrue();
        entities["ShipmentItem"].Properties.Should().NotContain(p => p.Name.Contains(','));
    }
}
