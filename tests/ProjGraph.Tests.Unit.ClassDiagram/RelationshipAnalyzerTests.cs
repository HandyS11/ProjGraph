using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.ClassDiagram;

/// <summary>
/// Tests for <see cref="RelationshipAnalyzer"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RelationshipAnalyzerTests
{
    [Fact]
    public void AddInheritanceRelationships_WithBaseClass_ShouldAddInheritance()
    {
        const string code = """
                            namespace Test;
                            public class Animal { }
                            public class Dog : Animal { }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Dog")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddInheritanceRelationships(symbol, related);

        related.Should().Contain(r => r.Kind == RelationshipKind.Inheritance);
        related.Should().Contain(r => r.Symbol.Name == "Animal");
    }

    [Fact]
    public void AddInheritanceRelationships_WithInterface_ShouldAddRealization()
    {
        const string code = """
                            namespace Test;
                            public interface IRunnable { void Run(); }
                            public class Runner : IRunnable { public void Run() { } }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Runner")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddInheritanceRelationships(symbol, related);

        related.Should().Contain(r => r.Kind == RelationshipKind.Realization && r.Symbol.Name == "IRunnable");
    }

    [Fact]
    public void AddInheritanceRelationships_NoBase_ShouldNotAddInheritance()
    {
        const string code = """
                            namespace Test;
                            public class Plain { }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Plain")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddInheritanceRelationships(symbol, related);

        related.Should().BeEmpty();
    }

    [Fact]
    public void AddDependencyRelationships_PropertyWithUserType_ShouldAddAssociation()
    {
        const string code = """
                            namespace Test;
                            public class Address { }
                            public class Person
                            {
                                public Address HomeAddress { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Person")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r =>
            r.Kind == RelationshipKind.Association &&
            r.Symbol.Name == "Address" &&
            r.Label == "HomeAddress");
    }

    [Fact]
    public void AddDependencyRelationships_CollectionProperty_ShouldHaveStarCardinality()
    {
        const string code = """
                            namespace Test;
                            public class Item { }
                            public class Order
                            {
                                public System.Collections.Generic.List<Item> Items { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r =>
            r.Symbol.Name == "Item" &&
            r.Cardinality == "*");
    }

    [Fact]
    public void AddDependencyRelationships_ArrayProperty_ShouldAddAssociationWithStarCardinality()
    {
        // An array-typed member is a collection, exactly like List<T>, and must produce an
        // association to its element type with '*' cardinality (not be silently dropped).
        const string code = """
                            namespace Test;
                            public class Item { }
                            public class Order
                            {
                                public Item[] Items { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r =>
            r.Kind == RelationshipKind.Association &&
            r.Symbol.Name == "Item" &&
            r.Label == "Items" &&
            r.Cardinality == "*");
    }

    [Fact]
    public void AddDependencyRelationships_SameNameDifferentNamespaces_MethodTypes_ShouldAddBoth()
    {
        // Dedupe keyed on simple names dropped the second A.Order/B.Order edge silently.
        const string code = """
                            namespace A { public class Order { } }
                            namespace B { public class Order { } }
                            namespace Test
                            {
                                public class Report
                                {
                                    public A.Order GetFirst() => new();
                                    public B.Order GetSecond() => new();
                                }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Report")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Where(r => r.Kind == RelationshipKind.Dependency && r.Symbol.Name == "Order")
            .Should().HaveCount(2);
    }

    [Fact]
    public void AddDependencyRelationships_SameNameDifferentNamespaces_GenericArguments_ShouldAddBoth()
    {
        const string code = """
                            namespace A { public class Order { } }
                            namespace B { public class Order { } }
                            namespace Test
                            {
                                public class Mapping
                                {
                                    public System.Collections.Generic.Dictionary<A.Order, B.Order> Map { get; set; }
                                }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Mapping")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Where(r => r.Symbol.Name == "Order" && r.Label == "Map")
            .Should().HaveCount(2);
    }

    [Fact]
    public void AddDependencyRelationships_UserGenericProperty_ShouldKeepOuterAndArgumentTypes()
    {
        // Result<Order> must produce edges to BOTH the user-defined container Result<T> and the
        // argument Order. Only BCL containers (List<T>, ...) are reduced to their arguments.
        const string code = """
                            namespace Test;
                            public class Order { }
                            public class Result<T> { }
                            public class Handler
                            {
                                public Result<Order> Outcome { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Handler")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r => r.Symbol.Name == "Result" && r.Label == "Outcome");
        related.Should().Contain(r => r.Symbol.Name == "Order" && r.Label == "Outcome");
    }

    [Fact]
    public void AddDependencyRelationships_BclGenericProperty_ShouldNotAddContainerType()
    {
        const string code = """
                            namespace Test;
                            public class Item { }
                            public class Order
                            {
                                public System.Collections.Generic.List<Item> Items { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r => r.Symbol.Name == "Item");
        related.Should().NotContain(r => r.Symbol.Name == "List");
    }

    [Fact]
    public void AddDependencyRelationships_NonCollectionGenericContainingSet_ShouldHaveSingleCardinality()
    {
        // "Settings" contains the substring "Set" — the old name heuristic wrongly classified
        // it as a collection. Collection-ness must come from IEnumerable, not the type name.
        const string code = """
                            namespace Test;
                            public class Theme { }
                            public class Settings<T> { }
                            public class App
                            {
                                public Settings<Theme> Config { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "App")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r => r.Symbol.Name == "Theme" && r.Cardinality == "1");
    }

    [Fact]
    public void AddDependencyRelationships_CustomEnumerableGeneric_ShouldHaveStarCardinality()
    {
        // A user collection whose name matches no magic substring must still count as one.
        const string code = """
                            namespace Test;
                            public class Item { }
                            public class ItemBag<T> : System.Collections.Generic.List<T> { }
                            public class Order
                            {
                                public ItemBag<Item> Items { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r => r.Symbol.Name == "Item" && r.Cardinality == "*");
    }

    [Fact]
    public void AddDependencyRelationships_UnresolvedCollectionNamedType_ShouldKeepStarCardinality()
    {
        // Unresolved (error) symbols carry no interface info; the name heuristic remains the
        // best-effort fallback so existing behavior for unresolved collections is preserved.
        const string code = """
                            namespace Test;
                            public class Item { }
                            public class Order
                            {
                                public MyCollection<Item> Items { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r => r.Symbol.Name == "Item" && r.Cardinality == "*");
    }

    [Fact]
    public void AddDependencyRelationships_ArrayMethodReturnAndParameter_ShouldAddDependency()
    {
        // The array unwrap added for members must also apply to method signatures:
        // Order[] GetAll() / Save(Order[] batch) are dependencies exactly like List<Order>.
        const string code = """
                            namespace Test;
                            public class Order { }
                            public class Repository
                            {
                                public Order[] GetAll() => [];
                                public void Save(Order[] batch) { }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Repository")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r =>
            r.Kind == RelationshipKind.Dependency &&
            r.Symbol.Name == "Order");
    }

    [Fact]
    public void AddDependencyRelationships_Enum_ShouldReturnEmpty()
    {
        const string code = """
                            namespace Test;
                            public enum Status { Active, Inactive }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Status")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().BeEmpty();
    }

    [Fact]
    public void AddDependencyRelationships_MethodParameter_ShouldAddDependency()
    {
        const string code = """
                            namespace Test;
                            public class Config { }
                            public class Service
                            {
                                public void Init(Config config) { }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Service")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r =>
            r.Kind == RelationshipKind.Dependency &&
            r.Symbol.Name == "Config");
    }
}
