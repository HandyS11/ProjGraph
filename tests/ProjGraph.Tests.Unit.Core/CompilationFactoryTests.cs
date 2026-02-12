using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProjGraph.Lib.Core.Infrastructure;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Tests for <see cref="CompilationFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CompilationFactoryTests
{
    private readonly CompilationFactory _sut = new();

    [Fact]
    public void CreateCompilation_EmptySyntaxTrees_ShouldReturnCompilation()
    {
        var result = _sut.CreateCompilation([]);

        result.Should().NotBeNull();
        result.SyntaxTrees.Should().BeEmpty();
    }

    [Fact]
    public void CreateCompilation_WithSyntaxTree_ShouldIncludeTree()
    {
        const string code = """
                            namespace Test;
                            public class Foo { }
                            """;
        var tree = CSharpSyntaxTree.ParseText(code);

        var result = _sut.CreateCompilation([tree]);

        result.SyntaxTrees.Should().HaveCount(1);
    }

    [Fact]
    public void CreateCompilation_ShouldResolveSystemTypes()
    {
        const string code = """
                            using System;
                            namespace Test;
                            public class Bar
                            {
                                public string Name { get; set; }
                                public int Count { get; set; }
                            }
                            """;
        var tree = CSharpSyntaxTree.ParseText(code);

        var compilation = _sut.CreateCompilation([tree]);
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void CreateCompilation_ShouldSupportNullableContext()
    {
        const string code = """
                            #nullable enable
                            namespace Test;
                            public class Baz
                            {
                                public string? NullableProp { get; set; }
                            }
                            """;
        var tree = CSharpSyntaxTree.ParseText(code);

        var compilation = _sut.CreateCompilation([tree]);
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void CreateCompilation_MultipleTrees_ShouldIncludeAll()
    {
        const string code1 = "namespace A; public class First { }";
        const string code2 = "namespace B; public class Second { }";
        var tree1 = CSharpSyntaxTree.ParseText(code1);
        var tree2 = CSharpSyntaxTree.ParseText(code2);

        var result = _sut.CreateCompilation([tree1, tree2]);

        result.SyntaxTrees.Should().HaveCount(2);
    }
}
