using Microsoft.CodeAnalysis;
using ProjGraph.Lib.EntityFramework.Infrastructure.Extensions;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="TypeSymbolExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TypeSymbolExtensionsTests
{
    [Fact]
    public void IsNullable_NullableAnnotated_ShouldReturnTrue()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             #nullable enable
                                                             public class MyClass
                                                             {
                                                                 public string? Name { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "MyClass")!;
        var prop = type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Name");

        prop.Type.IsNullable().Should().BeTrue();
    }

    [Fact]
    public void IsNullable_ValueType_ShouldReturnFalse()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class MyClass
                                                             {
                                                                 public int Count { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "MyClass")!;
        var prop = type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Count");

        prop.Type.IsNullable().Should().BeFalse();
    }

    [Fact]
    public void IsEfValueType_IntType_ShouldReturnTrue()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class MyClass
                                                             {
                                                                 public int Id { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "MyClass")!;
        var prop = type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Id");

        prop.Type.IsEfValueType().Should().BeTrue();
    }

    [Fact]
    public void IsEfValueType_StringType_ShouldReturnFalse()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class MyClass
                                                             {
                                                                 public string Name { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "MyClass")!;
        var prop = type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Name");

        prop.Type.IsEfValueType().Should().BeFalse();
    }

    [Fact]
    public void IsSystemOrPrimitiveType_SystemString_ShouldReturnTrue()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class MyClass
                                                             {
                                                                 public string Name { get; set; }
                                                             }
                                                             """);

        var stringType = compilation.GetSpecialType(SpecialType.System_String);

        stringType.IsSystemOrPrimitiveType().Should().BeTrue();
    }

    [Fact]
    public void IsSystemOrPrimitiveType_UserDefinedClass_ShouldReturnFalse()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class Order
                                                             {
                                                                 public int Id { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;

        type.IsSystemOrPrimitiveType().Should().BeFalse();
    }

    [Fact]
    public void IsCollectionType_List_ShouldReturnTrue()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System.Collections.Generic;
                                                             public class MyClass
                                                             {
                                                                 public List<string> Items { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "MyClass")!;
        var prop = type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Items");
        var propType = (INamedTypeSymbol)prop.Type;

        propType.IsCollectionType().Should().BeTrue();
    }

    [Fact]
    public void IsCollectionType_NonCollectionType_ShouldReturnFalse()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class MyClass
                                                             {
                                                                 public string Name { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "MyClass")!;

        type.IsCollectionType().Should().BeFalse();
    }
}
