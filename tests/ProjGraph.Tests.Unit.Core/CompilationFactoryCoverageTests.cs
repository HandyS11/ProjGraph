using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProjGraph.Lib.Core.Infrastructure;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Coverage for the compilation options and metadata-reference set produced by
/// <see cref="CompilationFactory"/>. The analyzers downstream depend on the nullable context being
/// enabled, on collection interfaces binding, and on the reference set being shared across calls.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CompilationFactoryCoverageTests
{
    private readonly CompilationFactory _sut = new();

    [Fact]
    public void CreateCompilation_ShouldEnableNullableContextWithoutPerFileDirective()
    {
        // The factory enables the nullable context globally. Without it, source that omits
        // '#nullable enable' would bind '?' annotations with warnings and the analyzers would see
        // different nullability than the real project build.
        var tree = CSharpSyntaxTree.ParseText(
            """
            namespace Test;
            public class Holder
            {
                public string? Maybe { get; set; }
            }
            """);

        var compilation = _sut.CreateCompilation([tree]);
        var symbol = compilation.GetTypeByMetadataName("Test.Holder")!;
        var property = symbol.GetMembers("Maybe").OfType<IPropertySymbol>().Single();

        property.Type.NullableAnnotation.Should().Be(NullableAnnotation.Annotated);
    }

    [Fact]
    public void CreateCompilation_ShouldProduceDynamicallyLinkedLibraryWithoutEntryPoint()
    {
        // A library output kind means no entry point is required; a console output kind would make
        // every entry-point-less analysis target report CS5001.
        var tree = CSharpSyntaxTree.ParseText("namespace Test; public class NoMain { }");

        var compilation = _sut.CreateCompilation([tree]);

        compilation.Options.OutputKind.Should().Be(OutputKind.DynamicallyLinkedLibrary);
        compilation.GetDiagnostics().Should().NotContain(d => d.Id == "CS5001");
    }

    [Fact]
    public void CreateCompilation_ShouldResolveCollectionInterfaces()
    {
        // Relationship analysis classifies collection-typed members by binding ICollection<T> and
        // IEnumerable<T>; those references must be present in the reference set.
        var tree = CSharpSyntaxTree.ParseText(
            """
            using System.Collections.Generic;
            namespace Test;
            public class Bag
            {
                public ICollection<string> Items { get; set; } = [];
                public IEnumerable<int> Numbers { get; set; } = [];
                public List<string> Names { get; set; } = [];
            }
            """);

        var compilation = _sut.CreateCompilation([tree]);

        compilation.GetDiagnostics().Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void CreateCompilation_ShouldBindTypesAcrossSeparateSyntaxTrees()
    {
        // Directory analysis feeds one tree per file; a type in one file must resolve a base type
        // declared in another.
        var baseTree = CSharpSyntaxTree.ParseText("namespace Test; public class Animal { }");
        var derivedTree = CSharpSyntaxTree.ParseText("namespace Test; public class Dog : Animal { }");

        var compilation = _sut.CreateCompilation([baseTree, derivedTree]);
        var dog = compilation.GetTypeByMetadataName("Test.Dog")!;

        dog.BaseType!.Name.Should().Be("Animal");
        dog.BaseType.TypeKind.Should().NotBe(TypeKind.Error);
    }

    [Fact]
    public void CreateCompilation_CalledTwice_ShouldReuseTheSameCachedReferenceSet()
    {
        // The reference set is built once per process and reused; rebuilding it would re-read
        // several assemblies from disk on every analysis.
        var first = _sut.CreateCompilation([]);
        var second = _sut.CreateCompilation([]);

        second.References.Should().BeEquivalentTo(first.References);
        second.References.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateCompilation_UnresolvableType_ShouldYieldErrorSymbolRatherThanThrow()
    {
        // Unresolved types are the normal case for workspace discovery: the factory must surface
        // them as error symbols so the resolver can search for their definition.
        var tree = CSharpSyntaxTree.ParseText("namespace Test; public class Service : Ghost { }");

        var compilation = _sut.CreateCompilation([tree]);
        var service = compilation.GetTypeByMetadataName("Test.Service")!;

        service.BaseType!.TypeKind.Should().Be(TypeKind.Error);
        service.BaseType.Name.Should().Be("Ghost");
    }

    [Fact]
    public void CreateCompilation_ShouldReferenceTheEmbeddedReferenceAssemblies()
    {
        // The BCL references are embedded in Lib.Core rather than read from Assembly.Location, which
        // is empty under Native AOT. JIT runs must use the same set so the unit tests exercise it.
        var compilation = _sut.CreateCompilation([]);
        var displays = compilation.References.Select(r => r.Display ?? "").ToList();

        displays.Should().Contain([
            "refs/netstandard.dll",
            "refs/System.Collections.dll",
            "refs/System.ComponentModel.Annotations.dll",
            "refs/System.Runtime.dll"
        ]);
        displays.Should().OnlyContain(d =>
            d.StartsWith("refs/", StringComparison.Ordinal) ||
            d.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("System.Threading.SemaphoreSlim")]
    [InlineData("System.Threading.Interlocked")]
    [InlineData("System.Threading.Monitor")]
    [InlineData("System.Threading.ReaderWriterLockSlim")]
    [InlineData("System.Threading.ThreadLocal`1")]
    [InlineData("System.Threading.AsyncLocal`1")]
    [InlineData("System.Threading.ManualResetEventSlim")]
    [InlineData("System.Threading.Mutex")]
    [InlineData("System.Threading.Thread")]
    [InlineData("System.Threading.SynchronizationContext")]
    [InlineData("System.Collections.Concurrent.ConcurrentQueue`1")]
    [InlineData("System.Text.UTF8Encoding")]
    [InlineData("System.Numerics.Vector3")]
    [InlineData("System.Numerics.Matrix4x4")]
    [InlineData("System.Diagnostics.Tracing.EventSource")]
    [InlineData("System.Runtime.InteropServices.Marshal")]
    [InlineData("System.MemoryExtensions")]
    public void CreateCompilation_BclTypesTheRuntimeReferenceSetBound_ShouldStillBind(string metadataName)
    {
        // The runtime reference set used to include System.Private.CoreLib, which binds every public
        // CoreLib type. The embedded reference assemblies must cover the same surface: an unbound
        // BCL type is no longer recognized as a System type and leaks into class diagrams as a node.
        var compilation = _sut.CreateCompilation([]);

        var type = compilation.GetTypeByMetadataName(metadataName);

        type.Should().NotBeNull();
        type.TypeKind.Should().NotBe(TypeKind.Error);
    }

    [Fact]
    public void CreateCompilation_CollectionsAndDataAnnotations_ShouldBindWithoutErrorTypes()
    {
        var tree = CSharpSyntaxTree.ParseText(
            """
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations;
            namespace Test;
            public class Order
            {
                [Required]
                public string Name { get; set; } = "";
                public List<string> Lines { get; set; } = [];
                public IEnumerable<int> Quantities { get; set; } = [];
            }
            """);

        var compilation = _sut.CreateCompilation([tree]);
        var order = compilation.GetTypeByMetadataName("Test.Order")!;

        compilation.GetDiagnostics().Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        order.GetMembers().OfType<IPropertySymbol>().Should().OnlyContain(p => p.Type.TypeKind != TypeKind.Error);
        order.GetMembers("Name").Single().GetAttributes().Should().ContainSingle()
            .Which.AttributeClass!.TypeKind.Should().NotBe(TypeKind.Error);
    }
}
