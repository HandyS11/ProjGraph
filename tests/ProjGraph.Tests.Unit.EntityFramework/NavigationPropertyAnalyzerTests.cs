using Microsoft.CodeAnalysis;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="NavigationPropertyAnalyzer"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NavigationPropertyAnalyzerTests
{
    private static IPropertySymbol GetProperty(string source, string className, string propertyName)
    {
        var compilation = RoslynTestHelper.CreateCompilation(source);
        var type = RoslynTestHelper.GetTypeSymbol(compilation, className)!;
        return type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == propertyName);
    }

    [Fact]
    public void IsNavigationProperty_CollectionOfEntities_ShouldReturnTrueAndIsCollection()
    {
        var prop = GetProperty("""
                               using System.Collections.Generic;
                               public class Order
                               {
                                   public int Id { get; set; }
                                   public List<OrderItem> Items { get; set; }
                               }
                               public class OrderItem
                               {
                                   public int Id { get; set; }
                               }
                               """, "Order", "Items");

        var result = NavigationPropertyAnalyzer.IsNavigationProperty(prop, out var targetType, out var isCollection);

        result.Should().BeTrue();
        isCollection.Should().BeTrue();
        targetType.Should().NotBeNull();
        targetType.Name.Should().Be("OrderItem");
    }

    [Fact]
    public void IsNavigationProperty_ReferenceToEntity_ShouldReturnTrueAndNotCollection()
    {
        var prop = GetProperty("""
                               public class OrderItem
                               {
                                   public int Id { get; set; }
                                   public Order Order { get; set; }
                               }
                               public class Order
                               {
                                   public int Id { get; set; }
                               }
                               """, "OrderItem", "Order");

        var result = NavigationPropertyAnalyzer.IsNavigationProperty(prop, out var targetType, out var isCollection);

        result.Should().BeTrue();
        isCollection.Should().BeFalse();
        targetType.Should().NotBeNull();
        targetType.Name.Should().Be("Order");
    }

    [Fact]
    public void IsNavigationProperty_CollectionOfPrimitives_ShouldReturnFalse()
    {
        // EF 8+ primitive collections (List<string> Tags) are scalar columns, not navigations. The
        // element type is not an entity candidate, so the property must fall through to a column
        // rather than being classified as a navigation (which would drop it from the ERD entirely).
        var prop = GetProperty("""
                               using System.Collections.Generic;
                               public class Post
                               {
                                   public int Id { get; set; }
                                   public List<string> Tags { get; set; }
                               }
                               """, "Post", "Tags");

        var result = NavigationPropertyAnalyzer.IsNavigationProperty(prop, out var targetType, out var isCollection);

        result.Should().BeFalse();
        isCollection.Should().BeFalse();
        targetType.Should().BeNull();
    }

    [Fact]
    public void IsNavigationProperty_PrimitiveType_ShouldReturnFalse()
    {
        var prop = GetProperty("""
                               public class Order
                               {
                                   public int Id { get; set; }
                                   public string Name { get; set; }
                               }
                               """, "Order", "Name");

        var result = NavigationPropertyAnalyzer.IsNavigationProperty(prop, out _, out var isCollection);

        result.Should().BeFalse();
        isCollection.Should().BeFalse();
    }

    [Fact]
    public void IsNavigationProperty_IntType_ShouldReturnFalse()
    {
        var prop = GetProperty("""
                               public class Order
                               {
                                   public int Id { get; set; }
                               }
                               """, "Order", "Id");

        var result = NavigationPropertyAnalyzer.IsNavigationProperty(prop, out _, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasInverseCollection_WithInverseCollection_ShouldReturnTrue()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System.Collections.Generic;
                                                             public class Order
                                                             {
                                                                 public int Id { get; set; }
                                                                 public Customer Customer { get; set; }
                                                             }
                                                             public class Customer
                                                             {
                                                                 public int Id { get; set; }
                                                                 public List<Order> Orders { get; set; }
                                                             }
                                                             """);
        var orderType = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;
        var customerType = RoslynTestHelper.GetTypeSymbol(compilation, "Customer")!;
        var customerProp = orderType.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Customer");

        var result = NavigationPropertyAnalyzer.HasInverseCollection(customerProp, customerType);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasInverseCollection_NoInverseCollection_ShouldReturnFalse()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Order
                                                             {
                                                                 public int Id { get; set; }
                                                                 public Customer Customer { get; set; }
                                                             }
                                                             public class Customer
                                                             {
                                                                 public int Id { get; set; }
                                                                 public string Name { get; set; }
                                                             }
                                                             """);
        var orderType = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;
        var customerType = RoslynTestHelper.GetTypeSymbol(compilation, "Customer")!;
        var customerProp = orderType.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Customer");

        var result = NavigationPropertyAnalyzer.HasInverseCollection(customerProp, customerType);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasInverseReference_WithInverseReference_ShouldReturnTrue()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Order
                                                             {
                                                                 public int Id { get; set; }
                                                                 public OrderDetail Detail { get; set; }
                                                             }
                                                             public class OrderDetail
                                                             {
                                                                 public int Id { get; set; }
                                                                 public Order Order { get; set; }
                                                             }
                                                             """);
        var orderType = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;
        var detailType = RoslynTestHelper.GetTypeSymbol(compilation, "OrderDetail")!;
        var detailProp = orderType.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Detail");

        var result = NavigationPropertyAnalyzer.HasInverseReference(detailProp, detailType);

        result.Should().BeTrue();
    }
}
