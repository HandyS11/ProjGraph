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

    [Fact]
    public void IsNullable_ReferenceTypeWithoutNrt_ShouldReturnTrue()
    {
        // Without #nullable enable, reference types have NullableAnnotation.None
        // and should fall through to the IsReferenceType check returning true
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             public class MyClass
                                                             {
                                                                 public string Name { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "MyClass")!;
        var prop = type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Name");

        prop.Type.IsNullable().Should().BeTrue();
    }

    [Fact]
    public void IsNullable_NonNullableValueType_ShouldReturnFalse()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             #nullable enable
                                                             public class MyClass
                                                             {
                                                                 public int Id { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "MyClass")!;
        var prop = type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Id");

        prop.Type.IsNullable().Should().BeFalse();
    }

    [Fact]
    public void IsCollectionType_HashSet_ShouldReturnTrue()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System.Collections.Generic;
                                                             public class MyClass
                                                             {
                                                                 public HashSet<int> Tags { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "MyClass")!;
        var prop = type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Tags");
        var propType = (INamedTypeSymbol)prop.Type;

        propType.IsCollectionType().Should().BeTrue();
    }

    [Fact]
    public void IsCollectionType_IEnumerable_ShouldReturnTrue()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System.Collections.Generic;
                                                             public class MyClass
                                                             {
                                                                 public IEnumerable<string> Items { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "MyClass")!;
        var prop = type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Items");
        var propType = (INamedTypeSymbol)prop.Type;

        propType.IsCollectionType().Should().BeTrue();
    }

    [Fact]
    public void IsSystemOrPrimitiveType_GuidType_ShouldReturnTrue()
    {
        var compilation = RoslynTestHelper.CreateCompilation("""
                                                             using System;
                                                             public class MyClass
                                                             {
                                                                 public Guid Id { get; set; }
                                                             }
                                                             """);

        var type = RoslynTestHelper.GetTypeSymbol(compilation, "MyClass")!;
        var prop = type.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Id");

        ((INamedTypeSymbol)prop.Type).IsSystemOrPrimitiveType().Should().BeTrue();
    }
}
