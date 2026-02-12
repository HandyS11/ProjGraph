using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;
using ModelTypeKind = ProjGraph.Core.Models.TypeKind;

namespace ProjGraph.Tests.Unit.ClassDiagram;

/// <summary>
/// Tests for <see cref="TypeAnalyzer"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TypeAnalyzerTests
{
    [Fact]
    public void AnalyzeType_ClassWithProperty_ShouldExtractPropertyMember()
    {
        const string code = """
                            namespace Test;
                            public class Foo
                            {
                                public string Name { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Foo")!;

        var result = TypeAnalyzer.AnalyzeType(symbol);

        result.Name.Should().Be("Foo");
        result.Kind.Should().Be(ModelTypeKind.Class);
        result.Members.Should().Contain(m => m.Name == "Name" && m.Kind == MemberKind.Property);
    }

    [Fact]
    public void AnalyzeType_ClassWithField_ShouldExtractFieldMember()
    {
        const string code = """
                            namespace Test;
                            public class Bar
                            {
                                public int Count;
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Bar")!;

        var result = TypeAnalyzer.AnalyzeType(symbol);

        result.Members.Should().Contain(m => m.Name == "Count" && m.Kind == MemberKind.Field);
    }

    [Fact]
    public void AnalyzeType_ClassWithMethod_ShouldExtractMethodMember()
    {
        const string code = """
                            namespace Test;
                            public class Svc
                            {
                                public void DoWork(int count) { }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Svc")!;

        var result = TypeAnalyzer.AnalyzeType(symbol);

        var method = result.Members.FirstOrDefault(m => m.Name == "DoWork");
        method.Should().NotBeNull();
        method.Kind.Should().Be(MemberKind.Method);
        method.Parameters.Should().Contain(p => p.Name == "count");
    }

    [Fact]
    public void AnalyzeType_Enum_ShouldReturnEnumKind()
    {
        const string code = """
                            namespace Test;
                            public enum Color { Red, Green, Blue }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Color")!;

        var result = TypeAnalyzer.AnalyzeType(symbol);

        result.Kind.Should().Be(ModelTypeKind.Enum);
        result.Members.Should().Contain(m => m.Name == "Red" && m.Kind == MemberKind.Field);
    }

    [Fact]
    public void AnalyzeType_Interface_ShouldReturnInterfaceKind()
    {
        const string code = """
                            namespace Test;
                            public interface IService
                            {
                                void Execute();
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "IService")!;

        var result = TypeAnalyzer.AnalyzeType(symbol);

        result.Kind.Should().Be(ModelTypeKind.Interface);
    }

    [Fact]
    public void AnalyzeType_Record_ShouldReturnRecordKind()
    {
        const string code = """
                            namespace Test;
                            public record Person(string Name, int Age);
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Person")!;

        var result = TypeAnalyzer.AnalyzeType(symbol);

        result.Kind.Should().Be(ModelTypeKind.Record);
    }

    [Fact]
    public void AnalyzeType_AbstractClass_ShouldSetIsAbstract()
    {
        const string code = """
                            namespace Test;
                            public abstract class Base
                            {
                                public abstract void Run();
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Base")!;

        var result = TypeAnalyzer.AnalyzeType(symbol);

        result.IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void GetFullyQualifiedName_ShouldStripGlobalPrefix()
    {
        const string code = """
                            namespace Test;
                            public class Widget { }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Widget")!;

        var result = TypeAnalyzer.GetFullyQualifiedName(symbol);

        result.Should().Be("Test.Widget");
    }

    [Theory]
    [InlineData("public", Visibility.Public)]
    [InlineData("protected", Visibility.Protected)]
    [InlineData("internal", Visibility.Internal)]
    [InlineData("private", Visibility.Private)]
    public void AnalyzeType_PropertyVisibility_ShouldMapCorrectly(string modifier, Visibility expected)
    {
        var code = $$"""
                     namespace Test;
                     public class Target
                     {
                         {{modifier}} string Value { get; set; }
                     }
                     """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Target")!;

        var result = TypeAnalyzer.AnalyzeType(symbol);

        result.Members.Should().Contain(m => m.Name == "Value" && m.Visibility == expected);
    }

    [Fact]
    public void AnalyzeType_Struct_ShouldReturnStructKind()
    {
        const string code = """
                            namespace Test;
                            public struct Point
                            {
                                public int X;
                                public int Y;
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Point")!;

        var result = TypeAnalyzer.AnalyzeType(symbol);

        result.Kind.Should().Be(ModelTypeKind.Struct);
    }
}
