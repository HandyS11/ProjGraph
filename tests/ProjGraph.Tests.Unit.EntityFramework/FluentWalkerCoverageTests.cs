using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Edge-case unit tests for the Fluent API walkers (<see cref="FluentEntityWalker"/>,
/// <see cref="FluentPropertyWalker"/>, <see cref="FluentRelationshipWalker"/>,
/// <see cref="FluentOwnedTypeWalker"/> and <see cref="EntityConfigurationWalker"/>): the guard paths that
/// must degrade gracefully rather than fabricate model content — unreadable (non-literal) arguments,
/// navigations whose CLR type cannot be resolved, configuration chained onto an out-of-scope join-entity
/// builder, and config classes that are not really config classes.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class FluentWalkerCoverageTests
{
    /// <summary>
    /// Compiles <paramref name="sources"/>, locates the <c>OnModelCreating</c> method in the first source,
    /// and seeds the entities dictionary and model from <paramref name="entityNames"/> (simulating DbSet
    /// discovery) so a walker can be driven in isolation.
    /// </summary>
    /// <param name="sources">The C# sources to compile; the first must declare <c>OnModelCreating</c>.</param>
    /// <param name="entityNames">The entity class names to pre-analyze into <see cref="EfEntity"/> instances.</param>
    private static (MethodDeclarationSyntax Method, Compilation Compilation, Dictionary<string, EfEntity> Entities, EfModel Model)
        Build(string[] sources, params string[] entityNames)
    {
        var compilation = RoslynTestHelper.CreateCompilation(sources);
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

    /// <summary>Compiles a single source. See <see cref="Build(string[], string[])"/>.</summary>
    /// <param name="source">The C# source to compile.</param>
    /// <param name="entityNames">The entity class names to pre-analyze.</param>
    private static (MethodDeclarationSyntax Method, Compilation Compilation, Dictionary<string, EfEntity> Entities, EfModel Model)
        Build(string source, params string[] entityNames)
        => Build([source], entityNames);

    private static EfProperty Property(Dictionary<string, EfEntity> entities, string entity, string property)
        => entities[entity].Properties.Single(p => p.Name == property);

    // ---------------------------------------------------------------- FluentEntityWalker

    [Fact]
    public void Apply_ToTableWithNonLiteralArgument_LeavesTableNameUnset()
    {
        const string source = """
            public static class Tables { public const string Account = "accounts"; }
            public class Account { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>().ToTable(Tables.Account);
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Account");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities["Account"].TableName.Should().BeEmpty(
            "the syntax-only walker cannot read a constant reference, and must not invent a table name");
    }

    [Fact]
    public void Apply_ToTableChainedOntoUsingEntity_DoesNotLeakOntoOuterEntity()
    {
        const string source = """
            public class Post { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Post>().UsingEntity("PostTag").ToTable("post_tag");
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Post");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities["Post"].TableName.Should().BeEmpty(
            "the table belongs to the out-of-scope join entity, not to Post");
    }

    [Fact]
    public void CollectEntityNames_EntityWithUnreadableArgument_IsSkipped()
    {
        const string source = """
            public class Account { public int Id { get; set; } }
            public class Order { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity(typeof(Account));
                    modelBuilder.Entity<Order>();
                }
            }
            """;
        var (method, _, _, _) = Build(source);

        var names = FluentEntityWalker.CollectEntityNames(method);

        names.Should().BeEquivalentTo(["Order"],
            "a typeof argument yields no readable entity name and must not produce a phantom entry");
    }

    // ---------------------------------------------------------------- FluentPropertyWalker

    [Fact]
    public void Apply_PropertyWithUnnameableArgument_ConfiguresNothing()
    {
        const string source = """
            public static class Names { public const string Label = "Label"; }
            public class Account { public int Id { get; set; } public string Label { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(Names.Label).HasMaxLength(50));
            }
            """;
        var (method, compilation, entities, _) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "Label").MaxLength.Should().BeNull();
        entities["Account"].Properties.Should().HaveCount(2, "no phantom property may be fabricated");
    }

    [Fact]
    public void Apply_PropertyLambdaBodyIsNotAMemberAccess_ConfiguresNothing()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Label { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => 1).HasMaxLength(50));
            }
            """;
        var (method, compilation, entities, _) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        entities["Account"].Properties.Should().HaveCount(2)
            .And.OnlyContain(p => p.MaxLength == null);
    }

    [Fact]
    public void Apply_ParenthesizedLambdaProperty_AppliesConfiguration()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Label { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property((a) => a.Label).HasMaxLength(75));
            }
            """;
        var (method, compilation, entities, _) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "Label").MaxLength.Should().Be(75);
    }

    [Fact]
    public void Apply_HasKeyChainedOntoUsingEntity_DoesNotLeakOntoOuterEntity()
    {
        const string source = """
            public class Post { public int Id { get; set; } public string Code { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Post>().UsingEntity("PostTag").HasKey("Code");
            }
            """;
        var (method, compilation, entities, _) = Build(source, "Post");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Post", "Code").IsPrimaryKey.Should().BeFalse(
            "the key belongs to the out-of-scope join entity, not to Post");
    }

    [Fact]
    public void Apply_HasMaxLengthWithNonNumericArgument_LeavesMaxLengthUnset()
    {
        const string source = """
            public static class Limits { public const int Label = 50; }
            public class Account { public int Id { get; set; } public string Label { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => a.Label).HasMaxLength(Limits.Label));
            }
            """;
        var (method, compilation, entities, _) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "Label").MaxLength.Should().BeNull();
    }

    [Fact]
    public void Apply_HasPrecisionWithNonNumericArgument_LeavesPrecisionUnset()
    {
        const string source = """
            public static class Limits { public const int Precision = 18; }
            public class Account { public int Id { get; set; } public decimal Balance { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => a.Balance).HasPrecision(Limits.Precision));
            }
            """;
        var (method, compilation, entities, _) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        var balance = Property(entities, "Account", "Balance");
        balance.Precision.Should().BeNull();
        balance.Scale.Should().BeNull();
    }

    [Fact]
    public void Apply_HasPrecisionWithoutScale_SetsPrecisionOnly()
    {
        const string source = """
            public class Account { public int Id { get; set; } public decimal Balance { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => a.Balance).HasPrecision(18));
            }
            """;
        var (method, compilation, entities, _) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        var balance = Property(entities, "Account", "Balance");
        balance.Precision.Should().Be(18);
        balance.Scale.Should().BeNull("the single-argument overload declares no scale");
    }

    [Fact]
    public void Apply_HasPrecisionWithUnreadableScale_KeepsPrecisionAndDropsScale()
    {
        const string source = """
            public static class Limits { public const int Scale = 2; }
            public class Account { public int Id { get; set; } public decimal Balance { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => a.Balance).HasPrecision(18, Limits.Scale));
            }
            """;
        var (method, compilation, entities, _) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        var balance = Property(entities, "Account", "Balance");
        balance.Precision.Should().Be(18);
        balance.Scale.Should().BeNull();
    }

    [Fact]
    public void Apply_IsRequiredFalse_MarksPropertyOptional()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Label { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => a.Label).IsRequired(false));
            }
            """;
        var (method, compilation, entities, _) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        var label = Property(entities, "Account", "Label");
        label.IsRequired.Should().BeFalse("an explicit IsRequired(false) overrides the non-nullable convention");
        label.IsExplicitlyRequired.Should().BeFalse();
    }

    // ---------------------------------------------------------------- FluentRelationshipWalker

    [Fact]
    public void Apply_HasOneWithUnnameableArgument_ProducesNoRelationship()
    {
        const string source = """
            public class Order { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>(e => e.HasOne(NavigationSelector).WithMany());
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        model.Relationships.Should().BeEmpty("an unreadable target must not be guessed at");
    }

    [Fact]
    public void Apply_HasOneLambdaBodyIsNotAMemberAccess_ProducesNoRelationship()
    {
        const string source = """
            public class Order { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>(e => e.HasOne(o => 1).WithMany());
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        model.Relationships.Should().BeEmpty();
    }

    [Fact]
    public void Apply_NavigationNotDeclaredOnSourceEntity_FallsBackToNavigationName()
    {
        const string source = """
            public class Order { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>(e => e.HasOne(o => o.Ghost).WithMany());
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var relationship = model.Relationships.Should().ContainSingle().Subject;
        relationship.SourceEntity.Should().Be("Ghost",
            "with no symbol to resolve, the raw navigation name is the best available target");
        relationship.TargetEntity.Should().Be("Order");
        relationship.Type.Should().Be(EfRelationshipType.OneToMany);
    }

    [Fact]
    public void Apply_NavigationTargetIsNotAKnownEntity_FallsBackToNavigationName()
    {
        const string source = """
            public class Person { public int Id { get; set; } }
            public class Order { public int Id { get; set; } public Person Buyer { get; set; } = new(); }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>(e => e.HasOne(o => o.Buyer).WithMany());
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        model.Relationships.Should().ContainSingle().Subject.SourceEntity.Should().Be("Buyer",
            "Person is not a modelled entity here, so its type name must not be substituted");
    }

    [Fact]
    public void Apply_HasForeignKeyWithNoReadableArgument_FabricatesNoProperty()
    {
        const string source = """
            using System.Collections.Generic;
            public class Item { public int Id { get; set; } public Order Order { get; set; } = new(); }
            public class Order { public int Id { get; set; } public List<Item> Items { get; set; } = new(); }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>(e => e.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey());
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order", "Item");
        var itemPropertyCount = entities["Item"].Properties.Count;

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        model.Relationships.Should().ContainSingle("the relationship itself is still valid");
        entities["Item"].Properties.Should().HaveCount(itemPropertyCount,
            "an argument-less HasForeignKey names no column, so none may be invented");
    }

    [Fact]
    public void Apply_HasForeignKeyForUnknownDependentType_FabricatesNoProperty()
    {
        const string source = """
            using System.Collections.Generic;
            public class Item { public int Id { get; set; } public Order Order { get; set; } = new(); }
            public class Order { public int Id { get; set; } public List<Item> Items { get; set; } = new(); }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>(e =>
                        e.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey<Ghost>("GhostRef"));
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order", "Item");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        entities["Item"].Properties.Should().NotContain(p => p.Name == "GhostRef");
        entities["Order"].Properties.Should().NotContain(p => p.Name == "GhostRef");
    }

    // ---------------------------------------------------------------- FluentOwnedTypeWalker

    [Fact]
    public void Apply_OwnsOneWhenOwnerIsNotKnown_CapturesNothing()
    {
        const string source = """
            public class Address { public string City { get; set; } = ""; }
            public class Order { public int Id { get; set; } public Address ShipTo { get; set; } = new(); }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>().OwnsOne(o => o.ShipTo, a => a.Property(x => x.City));
            }
            """;
        var (method, compilation, entities, model) = Build(source);

        FluentOwnedTypeWalker.Apply(method, entities, model, compilation);

        entities.Should().BeEmpty("the owner was never discovered, so its owned type cannot be attached");
        model.Entities.Should().BeEmpty();
    }

    [Fact]
    public void Apply_OwnsOneWithUnnameableNavigation_CapturesNothing()
    {
        const string source = """
            public class Order { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>().OwnsOne(AddressConfig, a => a.Property(x => x.City));
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentOwnedTypeWalker.Apply(method, entities, model, compilation);

        model.Entities.Should().NotContain(e => e.IsOwned);
    }

    [Fact]
    public void Apply_OwnsOneWithUnresolvableNavigationType_CapturesBareOwnedEntity()
    {
        const string source = """
            public class Order { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>().OwnsOne(o => o.Mystery, a => a.Property("City"));
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentOwnedTypeWalker.Apply(method, entities, model, compilation);

        var owned = model.Entities.Should().ContainSingle(e => e.IsOwned).Subject;
        owned.Name.Should().Be("Mystery", "with no CLR type to resolve, the navigation name is the fallback");
        owned.OwnerEntity.Should().Be("Order");
        owned.Properties.Should().ContainSingle(p => p.Name == "City",
            "the owned builder's own configuration is still captured");
        entities.Should().ContainKey("Order.Mystery");
    }

    [Fact]
    public void Apply_ExplicitGenericOwnsOne_SeedsOwnedTypeFromGenericArgument()
    {
        const string source = """
            public class Address { public string City { get; set; } = ""; public string Zip { get; set; } = ""; }
            public class Order { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>().OwnsOne<Address>(o => o.Shipping, a => a.Property(x => x.City));
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentOwnedTypeWalker.Apply(method, entities, model, compilation);

        var owned = model.Entities.Should().ContainSingle(e => e.IsOwned).Subject;
        owned.Name.Should().Be("Address");
        owned.NavigationName.Should().Be("Shipping");
        owned.Properties.Select(p => p.Name).Should().Contain("City").And.Contain("Zip",
            "the explicit generic argument names the CLR type to seed columns from");
    }

    [Fact]
    public void Apply_OwnsManyOnNonGenericNavigation_UsesTheNavigationTypeItself()
    {
        const string source = """
            public class Address { public string City { get; set; } = ""; }
            public class Order { public int Id { get; set; } public Address Billing { get; set; } = new(); }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>().OwnsMany(o => o.Billing, a => a.Property(x => x.City));
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentOwnedTypeWalker.Apply(method, entities, model, compilation);

        var owned = model.Entities.Should().ContainSingle(e => e.IsOwned).Subject;
        owned.Name.Should().Be("Address", "there is no element type to unwrap, so the navigation type is used");
        owned.IsCollection.Should().BeTrue();
    }

    [Fact]
    public void Apply_WithOwnerHasForeignKeyLambdaForm_MarksOwnedForeignKey()
    {
        const string source = """
            using System.Collections.Generic;
            public class Line { public int Id { get; set; } public int OwnerRef { get; set; } }
            public class Order { public int Id { get; set; } public List<Line> Lines { get; set; } = new(); }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>().OwnsMany(o => o.Lines, b => b.WithOwner().HasForeignKey(x => x.OwnerRef));
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentOwnedTypeWalker.Apply(method, entities, model, compilation);

        var owned = entities["Order.Lines"];
        owned.Name.Should().Be("Line", "OwnsMany unwraps the collection's element type");
        owned.Properties.Single(p => p.Name == "OwnerRef").IsForeignKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_HasForeignKeyWithoutWithOwner_IsIgnored()
    {
        const string source = """
            using System.Collections.Generic;
            public class Line { public int Id { get; set; } public int OwnerRef { get; set; } }
            public class Order { public int Id { get; set; } public List<Line> Lines { get; set; } = new(); }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Order>().OwnsMany(o => o.Lines, b => b.HasForeignKey("OwnerRef"));
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentOwnedTypeWalker.Apply(method, entities, model, compilation);

        entities["Order.Lines"].Properties.Single(p => p.Name == "OwnerRef").IsForeignKey.Should().BeFalse(
            "only the WithOwner().HasForeignKey(...) form declares the owned type's FK back to its owner");
    }

    [Fact]
    public void ResolveTables_OwnedTypeWithoutResolvableOwner_LeavesTableUnresolved()
    {
        var orphan = new EfEntity
        {
            Name = "Address",
            Key = "Order.ShipTo",
            IsOwned = true,
            OwnerEntity = null,
            NavigationName = "ShipTo"
        };
        var dangling = new EfEntity
        {
            Name = "Address",
            Key = "Ghost.BillTo",
            IsOwned = true,
            OwnerEntity = "Ghost",
            NavigationName = "BillTo"
        };
        var entities = new Dictionary<string, EfEntity>
        {
            ["Order.ShipTo"] = orphan,
            ["Ghost.BillTo"] = dangling
        };
        var model = new EfModel();
        model.Entities.Add(orphan);
        model.Entities.Add(dangling);

        FluentOwnedTypeWalker.ResolveTables(entities, model);

        entities["Order.ShipTo"].TableName.Should().BeEmpty();
        entities["Ghost.BillTo"].TableName.Should().BeEmpty(
            "an owner that is not in the dictionary supplies no table to inherit");
    }

    [Fact]
    public void StripShadowKeys_OwnedTypeOnItsOwnTable_KeepsPrimaryAndForeignKeys()
    {
        var owner = new EfEntity { Name = "Order", TableName = "Orders" };
        var owned = new EfEntity
        {
            Name = "Line",
            Key = "Order.Lines",
            IsOwned = true,
            OwnerEntity = "Order",
            NavigationName = "Lines",
            IsCollection = true,
            TableName = "Orders_Lines",
            Properties =
            {
                new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true },
                new EfProperty { Name = "OrderId", Type = "int", IsForeignKey = true }
            }
        };
        var entities = new Dictionary<string, EfEntity> { ["Order"] = owner, ["Order.Lines"] = owned };
        var model = new EfModel();
        model.Entities.Add(owner);
        model.Entities.Add(owned);

        FluentOwnedTypeWalker.StripShadowKeys(entities, model);

        var stripped = entities["Order.Lines"];
        stripped.Properties.Should().HaveCount(2);
        stripped.Properties.Single(p => p.Name == "Id").IsPrimaryKey.Should().BeTrue(
            "an owned type on its own table draws its own box, where the key is real");
        stripped.Properties.Should().ContainSingle(p => p.Name == "OrderId" && p.IsForeignKey,
            "the FK to the owner is a separate, real column when the tables differ");
    }

    // ---------------------------------------------------------------- EntityConfigurationWalker

    [Fact]
    public void Apply_DuplicateConfigClassNames_AppliesOnlyTheFirst()
    {
        const string context = """
            public class Account { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfiguration(new AccountConfiguration());
            }
            """;
        const string first = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class AccountConfiguration : IEntityTypeConfiguration<Account>
            {
                public void Configure(EntityTypeBuilder<Account> builder) => builder.ToTable("accounts_first");
            }
            """;
        const string second = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class AccountConfiguration : IEntityTypeConfiguration<Account>
            {
                public void Configure(EntityTypeBuilder<Account> builder) => builder.ToTable("accounts_second");
            }
            """;
        var (method, compilation, entities, model) = Build([context, first, second], "Account");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities["Account"].TableName.Should().Be("accounts_first",
            "a config-class name is applied once; a same-named duplicate must not re-run and overwrite it");
    }

    [Fact]
    public void Apply_ConfigClassWithoutConfigureMethod_IsSkipped()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Widget { public int Id { get; set; } }
            public class WidgetConfiguration : IEntityTypeConfiguration<Widget>
            {
                public void Setup(EntityTypeBuilder<Widget> builder) => builder.ToTable("widgets");
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfigurationsFromAssembly(typeof(Ctx).Assembly);
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities["Widget"].TableName.Should().BeEmpty(
            "without a Configure method there is no body EF would ever run");
    }

    [Fact]
    public void Apply_ApplyConfigurationWithNonObjectCreationArgument_FoldsNothing()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Widget { public int Id { get; set; } }
            public class WidgetConfiguration : IEntityTypeConfiguration<Widget>
            {
                public void Configure(EntityTypeBuilder<Widget> builder) => builder.ToTable("widgets");
            }
            public class Ctx
            {
                private readonly WidgetConfiguration _config = new();
                void OnModelCreating(dynamic modelBuilder) => modelBuilder.ApplyConfiguration(_config);
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities["Widget"].TableName.Should().BeEmpty(
            "the syntax-only walker cannot tell which config class a field reference denotes");
    }

    [Fact]
    public void Apply_GenericBaseTypeThatIsNotTheConfigurationInterface_IsSkipped()
    {
        const string source = """
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Widget { public int Id { get; set; } }
            public class WidgetBag : List<Widget>
            {
                public void Configure(EntityTypeBuilder<Widget> builder) => builder.ToTable("from_bag");
            }
            public class WidgetPair : IEntityTypeConfiguration<Widget, int>
            {
                public void Configure(EntityTypeBuilder<Widget> builder) => builder.ToTable("from_pair");
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfigurationsFromAssembly(typeof(Ctx).Assembly);
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities["Widget"].TableName.Should().BeEmpty(
            "neither an unrelated generic base nor a two-argument look-alike is IEntityTypeConfiguration<T>");
    }
}
