using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="EntityAnalyzer"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EntityAnalyzerTests
{
    [Fact]
    public void AnalyzeEntity_SimpleClass_ShouldExtractProperties()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Order
                                                             {
                                                                 public int Id { get; set; }
                                                                 public string Name { get; set; }
                                                                 public decimal Total { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Name.Should().Be("Order");
        entity.Properties.Should().HaveCount(3);
        entity.Properties.Should().Contain(p => p.Name == "Id");
        entity.Properties.Should().Contain(p => p.Name == "Name");
        entity.Properties.Should().Contain(p => p.Name == "Total");
    }

    [Fact]
    public void AnalyzeEntity_ShouldSkipStaticIndexerAndComputedProperties()
    {
        // EF Core maps only instance, non-indexer, settable properties. A static property, an
        // indexer, and a get-only computed property must not become entity columns.
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Order
                                                             {
                                                                 public int Id { get; set; }
                                                                 public string Name { get; set; }
                                                                 public static Order Default { get; } = new();
                                                                 public string Display => Name;
                                                                 public int this[int i] => i;
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.Select(p => p.Name).Should().BeEquivalentTo("Id", "Name");
    }

    [Fact]
    public void AnalyzeEntity_IdProperty_ShouldBeIdentifiedAsPrimaryKey()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Order
                                                             {
                                                                 public int Id { get; set; }
                                                                 public string Name { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.First(p => p.Name == "Id").IsPrimaryKey.Should().BeTrue();
        entity.Properties.First(p => p.Name == "Name").IsPrimaryKey.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeEntity_EntityNameIdConvention_ShouldBeIdentifiedAsPrimaryKey()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Order
                                                             {
                                                                 public int OrderId { get; set; }
                                                                 public string Name { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.First(p => p.Name == "OrderId").IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeEntity_WithInheritance_ShouldIncludeBaseProperties()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class BaseEntity
                                                             {
                                                                 public int Id { get; set; }
                                                             }
                                                             public class Order : BaseEntity
                                                             {
                                                                 public string Name { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.Should().Contain(p => p.Name == "Id");
        entity.Properties.Should().Contain(p => p.Name == "Name");
    }

    [Fact]
    public void AnalyzeEntity_NavigationProperties_ShouldBeExcluded()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System.Collections.Generic;
                                                             public class Order
                                                             {
                                                                 public int Id { get; set; }
                                                                 public string Name { get; set; }
                                                                 public List<OrderItem> Items { get; set; }
                                                             }
                                                             public class OrderItem
                                                             {
                                                                 public int Id { get; set; }
                                                                 public int OrderId { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.Should().NotContain(p => p.Name == "Items");
        entity.Properties.Should().HaveCount(2); // Id and Name only
    }

    [Fact]
    public void AnalyzeEntity_ValueTypeProperty_ShouldMarkIsValueType()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Order
                                                             {
                                                                 public int Id { get; set; }
                                                                 public decimal Total { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.First(p => p.Name == "Total").IsValueType.Should().BeTrue();
    }

    [Fact]
    public void FindEntitySymbol_ExistingEntity_ShouldReturnSymbol()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Order
                                                             {
                                                                 public int Id { get; set; }
                                                             }
                                                             """);
        var entity = new EfEntity
        {
            Name = "Order"
        };

        var symbol = EntityAnalyzer.FindEntitySymbol(entity, compilation);

        symbol.Should().NotBeNull();
        symbol.Name.Should().Be("Order");
    }

    [Fact]
    public void FindEntitySymbol_NonExistingEntity_ShouldReturnNull()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Order
                                                             {
                                                                 public int Id { get; set; }
                                                             }
                                                             """);
        var entity = new EfEntity
        {
            Name = "NonExistent"
        };

        var symbol = EntityAnalyzer.FindEntitySymbol(entity, compilation);

        symbol.Should().BeNull();
    }

    [Fact]
    public void AnalyzeEntity_KeyAttribute_ShouldMarkAsPrimaryKey()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System.ComponentModel.DataAnnotations;
                                                             public class Ticket
                                                             {
                                                                 [Key]
                                                                 public string Code { get; set; }
                                                                 public string Title { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Ticket")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.First(p => p.Name == "Code").IsPrimaryKey.Should().BeTrue();
        entity.Properties.First(p => p.Name == "Title").IsPrimaryKey.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeEntity_RecordWithTypeLevelPrimaryKeyAttribute_SyntaxPathMarksPrimaryKey()
    {
        // The EF Core [PrimaryKey] attribute is not resolvable in these fixtures (no EF reference), so
        // only the syntax fallback can read it - and that fallback filtered on ClassDeclarationSyntax,
        // silently skipping record-declared entities. Regression for the widening to
        // TypeDeclarationSyntax (issue #163, item 2).
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using Microsoft.EntityFrameworkCore;
                                                             [PrimaryKey(nameof(Code))]
                                                             public record Ticket
                                                             {
                                                                 public string Code { get; set; }
                                                                 public string Title { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Ticket")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.First(p => p.Name == "Code").IsPrimaryKey.Should().BeTrue(
            "the type-level [PrimaryKey] attribute lives on a RecordDeclarationSyntax, which the " +
            "syntax fallback must not skip");
        entity.Properties.First(p => p.Name == "Title").IsPrimaryKey.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeEntity_PositionalRecordWithPropertyTargetedKeyAttribute_MarksPrimaryKey()
    {
        // A positional record's [property: Key] lives on a primary-constructor parameter; the
        // synthesized property carries the attribute at runtime, but the syntax fallback saw only
        // PropertyDeclarationSyntax members and missed it (issue #163 hazard family).
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System.ComponentModel.DataAnnotations;
                                                             public record Voucher([property: Key] string Code, string Label);
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Voucher")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.First(p => p.Name == "Code").IsPrimaryKey.Should().BeTrue(
            "EF reads [property: Key] off the property synthesized from the record parameter");
        entity.Properties.First(p => p.Name == "Label").IsPrimaryKey.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeEntity_RequiredAttribute_ShouldMarkAsRequired()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System.ComponentModel.DataAnnotations;
                                                             public class Product
                                                             {
                                                                 public int Id { get; set; }
                                                                 [Required]
                                                                 public string Name { get; set; }
                                                                 public string? Description { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Product")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.First(p => p.Name == "Name").IsRequired.Should().BeTrue();
        entity.Properties.First(p => p.Name == "Name").IsExplicitlyRequired.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeEntity_MaxLengthAttribute_ShouldExtractMaxLength()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System.ComponentModel.DataAnnotations;
                                                             public class Category
                                                             {
                                                                 public int Id { get; set; }
                                                                 [MaxLength(100)]
                                                                 public string Name { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Category")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.First(p => p.Name == "Name").MaxLength.Should().Be(100);
    }

    [Fact]
    public void AnalyzeEntity_StringLengthAttribute_ShouldExtractMaxLength()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System.ComponentModel.DataAnnotations;
                                                             public class Tag
                                                             {
                                                                 public int Id { get; set; }
                                                                 [StringLength(50)]
                                                                 public string Label { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Tag")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.First(p => p.Name == "Label").MaxLength.Should().Be(50);
    }

    [Fact]
    public void AnalyzeEntity_ColumnAttributeWithPrecision_ShouldExtractPrecisionAndScale()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System.ComponentModel.DataAnnotations.Schema;
                                                             public class Invoice
                                                             {
                                                                 public int Id { get; set; }
                                                                 [Column(TypeName = "decimal(18,4)")]
                                                                 public decimal Amount { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Invoice")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        var amount = entity.Properties.First(p => p.Name == "Amount");
        amount.Precision.Should().Be(18);
        amount.Scale.Should().Be(4);
    }

    [Fact]
    public void AnalyzeEntity_PrimaryKeyAttributeOnClass_ShouldIdentifyCompositeKey()
    {
        // Syntax-based PrimaryKey extraction (class-level attribute)
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using Microsoft.EntityFrameworkCore;
                                                             [PrimaryKey("OrderId", "ProductId")]
                                                             public class OrderItem
                                                             {
                                                                 public int OrderId { get; set; }
                                                                 public int ProductId { get; set; }
                                                                 public int Quantity { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "OrderItem")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.First(p => p.Name == "OrderId").IsPrimaryKey.Should().BeTrue();
        entity.Properties.First(p => p.Name == "ProductId").IsPrimaryKey.Should().BeTrue();
        entity.Properties.First(p => p.Name == "Quantity").IsPrimaryKey.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeEntity_DuplicatePropertyInHierarchy_ShouldAddOnce()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Base
                                                             {
                                                                 public int Id { get; set; }
                                                                 public string Name { get; set; }
                                                             }
                                                             public class Derived : Base
                                                             {
                                                                 public new string Name { get; set; }
                                                                 public int Extra { get; set; }
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Derived")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        // "Name" should appear only once despite being in both base and derived
        entity.Properties.Count(p => p.Name == "Name").Should().Be(1);
    }

    [Fact]
    public void AnalyzeEntity_NonNullableProperty_ShouldBeRequired()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             #nullable enable
                                                             public class Item
                                                             {
                                                                 public int Id { get; set; }
                                                                 public string Title { get; set; } = "";
                                                             }
                                                             """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Item")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        // Non-nullable string property should be required
        entity.Properties.First(p => p.Name == "Title").IsRequired.Should().BeTrue();
    }

    /// <summary>
    /// Fixture-local <c>[PrimaryKey]</c>/<c>[Key]</c>/<c>[Column]</c> attributes so the semantic paths
    /// (which read <c>AttributeClass.Name</c>) resolve without an EF Core reference.
    /// </summary>
    private const string DataAnnotationAttributes = """
        using System;
        public sealed class PrimaryKeyAttribute : Attribute
        {
            public PrimaryKeyAttribute(string propertyName, params string[] additionalPropertyNames) { }
        }
        public sealed class KeyAttribute : Attribute { }
        public sealed class ColumnAttribute : Attribute { public string? TypeName { get; set; } }
        """;

    [Fact]
    public void AnalyzeEntity_PrimaryKeyAttributeWithNameof_ShouldMarkCompositeKey()
    {
        var compilation = RoslynTestHelper.CreateCompilation(DataAnnotationAttributes, """
            [PrimaryKey(nameof(TenantId), nameof(Code))]
            public class Tenant
            {
                public int TenantId { get; set; }
                public string Code { get; set; } = "";
                public string Name { get; set; } = "";
            }
            """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Tenant")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.Where(p => p.IsPrimaryKey).Select(p => p.Name)
            .Should().BeEquivalentTo("TenantId", "Code");
        entity.Properties.First(p => p.Name == "Name").IsPrimaryKey.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeEntity_PrimaryKeyAttributeWithStringLiterals_ShouldMarkCompositeKey()
    {
        var compilation = RoslynTestHelper.CreateCompilation(DataAnnotationAttributes, """
            [PrimaryKey("OrderId", "LineNumber")]
            public class OrderLine
            {
                public int OrderId { get; set; }
                public int LineNumber { get; set; }
                public string Sku { get; set; } = "";
            }
            """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "OrderLine")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.Where(p => p.IsPrimaryKey).Select(p => p.Name)
            .Should().BeEquivalentTo("OrderId", "LineNumber");
    }

    [Fact]
    public void AnalyzeEntity_KeyAttributeOnProperty_ShouldMarkPrimaryKey()
    {
        var compilation = RoslynTestHelper.CreateCompilation(DataAnnotationAttributes, """
            public class Product
            {
                [Key]
                public string Sku { get; set; } = "";
                public string Name { get; set; } = "";
            }
            """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Product")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        entity.Properties.First(p => p.Name == "Sku").IsPrimaryKey.Should().BeTrue();
        entity.Properties.First(p => p.Name == "Name").IsPrimaryKey.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeEntity_ColumnTypeNameWithPrecision_ShouldExtractPrecisionAndScale()
    {
        var compilation = RoslynTestHelper.CreateCompilation(DataAnnotationAttributes, """
            public class Invoice
            {
                public int Id { get; set; }
                [Column(TypeName = "decimal(18,4)")]
                public decimal Amount { get; set; }
            }
            """);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Invoice")!;

        var entity = EntityAnalyzer.AnalyzeEntity(type);

        var amount = entity.Properties.First(p => p.Name == "Amount");
        amount.Precision.Should().Be(18);
        amount.Scale.Should().Be(4);
    }
}
