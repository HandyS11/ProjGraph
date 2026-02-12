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
}
