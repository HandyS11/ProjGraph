using Microsoft.CodeAnalysis;
using NSubstitute;
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Tests.Shared.Helpers;
using System.Collections.ObjectModel;

namespace ProjGraph.Tests.Unit.ClassDiagram;

/// <summary>
/// Tests for <see cref="SymbolResolver"/> and <see cref="TypeProcessor"/> workspace-discovery
/// behavior: symbols already in the compilation and system types must not trigger workspace
/// searches, and repeated lookups for the same type must be memoized per analysis run.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SymbolResolverTests
{
    private readonly IWorkspaceTypeDiscovery _discovery = Substitute.For<IWorkspaceTypeDiscovery>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();

    private static AnalysisContext CreateContext(Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation)
    {
        return new AnalysisContext
        {
            AnalyzedTypeFullNames = [],
            Types = new Collection<TypeDefinition>(),
            Relationships = new Collection<Relationship>(),
            Compilation = compilation,
            StartDirectory = "/nonexistent"
        };
    }

    [Fact]
    public async Task ResolveRelatedSymbolAsync_SymbolDeclaredInCompilation_ShouldNotSearchWorkspace()
    {
        // A base type declared in the current compilation is already fully resolved; searching
        // the workspace again is wasted I/O and could bind an unrelated same-named file instead.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public class Base { }",
            "namespace MyApp; public class Derived : Base { }");
        var derived = RoslynTestHelper.GetTypeSymbol(compilation, "Derived")!;
        var baseSymbol = derived.BaseType!;
        var sut = new SymbolResolver(_discovery, _fileSystem);
        var context = CreateContext(compilation);

        var resolved = await sut.ResolveRelatedSymbolAsync(baseSymbol, context);

        resolved.Should().NotBeNull();
        resolved!.Name.Should().Be("Base");
        await _discovery.DidNotReceive().FindTypeDefinitionFileAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveRelatedSymbolAsync_MetadataSymbol_ShouldAddExternalTypeWithoutSearching()
    {
        // A non-error symbol that is not in source was resolved from a referenced assembly.
        // There is nothing to discover in the workspace, and a scan could rebind the type to an
        // unrelated source type sharing the same simple name.
        var compilation = RoslynTestHelper.CreateCompilation(
            """
            using Microsoft.Win32.SafeHandles;
            namespace MyApp;
            public class Holder { public SafeFileHandle? Handle { get; set; } }
            """);
        var holder = RoslynTestHelper.GetTypeSymbol(compilation, "Holder")!;
        var handleSymbol = (INamedTypeSymbol)holder.GetMembers().OfType<IPropertySymbol>()
            .First(p => p.Name == "Handle").Type;
        handleSymbol.TypeKind.Should().NotBe(Microsoft.CodeAnalysis.TypeKind.Error,
            "the test requires a resolved metadata symbol");
        var sut = new SymbolResolver(_discovery, _fileSystem);
        var context = CreateContext(compilation);
        _discovery.FindTypeDefinitionFileAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>(null));

        var resolved = await sut.ResolveRelatedSymbolAsync(handleSymbol, context);

        resolved.Should().BeNull();
        context.Types.Should().ContainSingle(t => t.Name == "SafeFileHandle");
        await _discovery.DidNotReceive().FindTypeDefinitionFileAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveRelatedSymbolAsync_SameExternalTypeTwice_ShouldAddSingleNode()
    {
        // The same external type referenced by two source types must yield exactly one external
        // node, not a duplicate per reference.
        var compilation = RoslynTestHelper.CreateCompilation(
            """
            using Microsoft.Win32.SafeHandles;
            namespace MyApp;
            public class A { public SafeFileHandle? Handle { get; set; } }
            """);
        var holder = RoslynTestHelper.GetTypeSymbol(compilation, "A")!;
        var handleSymbol = (INamedTypeSymbol)holder.GetMembers().OfType<IPropertySymbol>()
            .First(p => p.Name == "Handle").Type;
        var sut = new SymbolResolver(_discovery, _fileSystem);
        var context = CreateContext(compilation);
        _discovery.FindTypeDefinitionFileAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>(null));

        await sut.ResolveRelatedSymbolAsync(handleSymbol, context);
        await sut.ResolveRelatedSymbolAsync(handleSymbol, context);

        context.Types.Should().ContainSingle(t => t.Name == "SafeFileHandle");
    }

    [Fact]
    public async Task ResolveRelatedSymbolAsync_RepeatedUnresolvedType_ShouldSearchWorkspaceOnce()
    {
        // Multiple references to the same unresolved external type within one analysis run must
        // trigger a single workspace search, not one full scan per referencing type.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public class Service : Ghost { }");
        var service = RoslynTestHelper.GetTypeSymbol(compilation, "Service")!;
        var ghostSymbol = service.BaseType!;
        var sut = new SymbolResolver(_discovery, _fileSystem);
        var context = CreateContext(compilation);
        _discovery.FindTypeDefinitionFileAsync("Ghost", Arg.Any<string>())
            .Returns(Task.FromResult<string?>(null));

        await sut.ResolveRelatedSymbolAsync(ghostSymbol, context);
        await sut.ResolveRelatedSymbolAsync(ghostSymbol, context);

        await _discovery.Received(1).FindTypeDefinitionFileAsync("Ghost", Arg.Any<string>());
    }

    [Fact]
    public async Task ProcessTypeQueue_StructAndEnum_ShouldNotResolveSystemBaseTypes()
    {
        // Structs implicitly derive from System.ValueType and enums from System.Enum. Neither
        // base may reach the symbol resolver, where it would trigger a full workspace scan.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public struct Money { public decimal Amount; }",
            "namespace MyApp; public enum Color { Red, Green }");
        var resolver = Substitute.For<ISymbolResolver>();
        resolver.ResolveRelatedSymbolAsync(Arg.Any<INamedTypeSymbol>(), Arg.Any<AnalysisContext>())
            .Returns(Task.FromResult<INamedTypeSymbol?>(null));
        var sut = new TypeProcessor(resolver);
        var context = CreateContext(compilation);
        var queue = new Queue<(INamedTypeSymbol Symbol, int Depth)>();
        queue.Enqueue((RoslynTestHelper.GetTypeSymbol(compilation, "Money")!, 0));
        queue.Enqueue((RoslynTestHelper.GetTypeSymbol(compilation, "Color")!, 0));

        await sut.ProcessTypeQueueAsync(queue, context,
            new AnalysisOptions(MaxDepth: 5, IncludeInheritance: true, IncludeDependencies: true));

        await resolver.DidNotReceive()
            .ResolveRelatedSymbolAsync(
                Arg.Is<INamedTypeSymbol>(s => s != null && (s.Name == "ValueType" || s.Name == "Enum")),
                Arg.Any<AnalysisContext>());
    }
}
