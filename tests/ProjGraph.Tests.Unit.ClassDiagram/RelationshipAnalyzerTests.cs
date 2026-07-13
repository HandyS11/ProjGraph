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
