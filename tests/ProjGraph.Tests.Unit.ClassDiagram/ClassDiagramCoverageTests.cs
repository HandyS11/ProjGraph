using Microsoft.CodeAnalysis;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Lib.ClassDiagram.Rendering;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Tests.Shared.Helpers;
using TypeKind = ProjGraph.Core.Models.TypeKind;

namespace ProjGraph.Tests.Unit.ClassDiagram;

/// <summary>
/// Error-path and fallback coverage across the class-diagram pipeline: unreadable directories and
/// files during discovery, symbols that cannot be resolved to a declaration, system types that must
/// never become diagram nodes, and renderer defaults for out-of-range enum values.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ClassDiagramCoverageTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IWorkspaceTypeDiscovery _discovery = Substitute.For<IWorkspaceTypeDiscovery>();

    private static AnalysisContext CreateContext(Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation)
    {
        return new AnalysisContext
        {
            AnalyzedTypeFullNames = [],
            Types = [],
            Relationships = [],
            Compilation = compilation,
            StartDirectory = "/nonexistent"
        };
    }

    // ---------------------------------------------------------------------
    // DiscoverCsFilesUseCase — unreadable directories
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(DirectoryReadFailures))]
    public void DiscoverCsFiles_UnreadableSubdirectory_ShouldBeSkippedWithoutFailingTheScan(Exception failure)
    {
        // A single permission-denied or transient IO failure deep in a tree must not abort the
        // whole discovery pass; the readable files found so far still have to be returned.
        const string root = "/root";
        const string denied = "/root/denied";
        _fileSystem.DirectoryExists(root).Returns(true);
        _fileSystem.GetFullPath(root).Returns(root);
        _fileSystem.GetFiles(root, "*.cs").Returns(["/root/Ok.cs"]);
        _fileSystem.GetDirectories(root).Returns([denied]);
        _fileSystem.GetFiles(denied, "*.cs").Throws(failure);
        var sut = new DiscoverCsFilesUseCase(_fileSystem);

        var result = sut.Execute(root);

        result.Should().BeEquivalentTo("/root/Ok.cs");
    }

    public static TheoryData<Exception> DirectoryReadFailures => new(
        new UnauthorizedAccessException("denied"),
        new IOException("device not ready"));

    [Fact]
    public void DiscoverCsFiles_UnreadableSubdirectory_ShouldNotStopSiblingDirectories()
    {
        // The failing directory must not shadow its siblings that are still enumerable.
        const string root = "/root";
        const string denied = "/root/denied";
        const string ok = "/root/ok";
        _fileSystem.DirectoryExists(root).Returns(true);
        _fileSystem.GetFullPath(root).Returns(root);
        _fileSystem.GetFiles(root, "*.cs").Returns([]);
        _fileSystem.GetDirectories(root).Returns([denied, ok]);
        _fileSystem.GetFiles(denied, "*.cs").Throws(new UnauthorizedAccessException("denied"));
        _fileSystem.GetFiles(ok, "*.cs").Returns(["/root/ok/Sibling.cs"]);
        _fileSystem.GetDirectories(ok).Returns([]);
        var sut = new DiscoverCsFilesUseCase(_fileSystem);

        var result = sut.Execute(root);

        result.Should().BeEquivalentTo("/root/ok/Sibling.cs");
    }

    // ---------------------------------------------------------------------
    // SymbolResolver — declaration lookup fallbacks
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ResolveRelatedSymbol_DiscoveredFileAlreadyInCompilation_ShouldNotReReadItFromDisk()
    {
        // When workspace discovery points at a file whose tree is already in the compilation, the
        // resolver must reuse that tree instead of re-reading and re-parsing the same source.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public class Service : Ghost { }");
        var ghost = RoslynTestHelper.GetTypeSymbol(compilation, "Service")!.BaseType!;
        ghost.TypeKind.Should().Be(Microsoft.CodeAnalysis.TypeKind.Error);
        _discovery.FindTypeDefinitionFileAsync("Ghost", Arg.Any<string>())
            .Returns(Task.FromResult<string?>("Test0.cs"));
        var sut = new SymbolResolver(_discovery, _fileSystem);
        var context = CreateContext(compilation);

        var resolved = await sut.ResolveRelatedSymbolAsync(ghost, context);

        await _fileSystem.DidNotReceive().ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        // The reused tree does not actually declare Ghost, so the original symbol is handed back.
        resolved.Should().NotBeNull();
        resolved.Name.Should().Be("Ghost");
    }

    [Fact]
    public async Task ResolveRelatedSymbol_DiscoveredFileWithoutMatchingDeclaration_ShouldReturnOriginalSymbol()
    {
        // Discovery can point at a false positive (the name appears in the file but no type with
        // that name is declared). The resolver must degrade to the original symbol, not crash.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public class Service : Ghost { }");
        var ghost = RoslynTestHelper.GetTypeSymbol(compilation, "Service")!.BaseType!;
        _discovery.FindTypeDefinitionFileAsync("Ghost", Arg.Any<string>())
            .Returns(Task.FromResult<string?>("/workspace/Decoy.cs"));
        _fileSystem.ReadAllTextAsync("/workspace/Decoy.cs", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("namespace MyApp; public class Unrelated { }"));
        var sut = new SymbolResolver(_discovery, _fileSystem);
        var context = CreateContext(compilation);

        var resolved = await sut.ResolveRelatedSymbolAsync(ghost, context);

        resolved.Should().NotBeNull();
        resolved.Name.Should().Be("Ghost");
    }

    [Fact]
    public async Task ResolveRelatedSymbol_DiscoveredFileDeclaringTheType_ShouldResolveToThatDeclaration()
    {
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public class Service : Ghost { }");
        var ghost = RoslynTestHelper.GetTypeSymbol(compilation, "Service")!.BaseType!;
        _discovery.FindTypeDefinitionFileAsync("Ghost", Arg.Any<string>())
            .Returns(Task.FromResult<string?>("/workspace/Ghost.cs"));
        _fileSystem.ReadAllTextAsync("/workspace/Ghost.cs", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("namespace MyApp; public class Ghost { }"));
        var sut = new SymbolResolver(_discovery, _fileSystem);
        var context = CreateContext(compilation);

        var resolved = await sut.ResolveRelatedSymbolAsync(ghost, context);

        resolved.Should().NotBeNull();
        resolved.Name.Should().Be("Ghost");
        resolved.TypeKind.Should().Be(Microsoft.CodeAnalysis.TypeKind.Class);
    }

    [Fact]
    public async Task ResolveRelatedSymbol_UnresolvedWellKnownSystemType_ShouldNotBecomeAnExternalNode()
    {
        // 'Task' used without its using directive binds to an error symbol. It is still a BCL type
        // and must not be drawn as an external class node in the diagram.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public class Service { public Task Work { get; set; } }");
        var service = RoslynTestHelper.GetTypeSymbol(compilation, "Service")!;
        var taskSymbol = (INamedTypeSymbol)service.GetMembers().OfType<IPropertySymbol>()
            .First(p => p.Name == "Work").Type;
        taskSymbol.TypeKind.Should().Be(Microsoft.CodeAnalysis.TypeKind.Error);
        _discovery.FindTypeDefinitionFileAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<string?>(null));
        var sut = new SymbolResolver(_discovery, _fileSystem);
        var context = CreateContext(compilation);

        var resolved = await sut.ResolveRelatedSymbolAsync(taskSymbol, context);

        resolved.Should().BeNull();
        context.Types.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------
    // TypeProcessor — system-type and unresolved-symbol handling
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ProcessTypeQueue_SystemTypeEnqueued_ShouldNotProduceANode()
    {
        // System types reaching the queue (e.g. via a base-type walk) must be dropped rather than
        // rendered as classes in the user's diagram.
        var compilation = RoslynTestHelper.CreateCompilation("namespace MyApp; public class Service { }");
        var resolver = Substitute.For<ISymbolResolver>();
        var sut = new TypeProcessor(resolver);
        var context = CreateContext(compilation);
        var queue = new Queue<(INamedTypeSymbol Symbol, int Depth)>();
        queue.Enqueue((compilation.GetSpecialType(SpecialType.System_String), 0));

        await sut.ProcessTypeQueueAsync(queue, context, new AnalysisOptions(MaxDepth: 2));

        context.Types.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessTypeQueue_MemberTypedAsSystemType_ShouldNotCreateARelationship()
    {
        // Every class has string/int members; drawing an edge to each BCL type would swamp the
        // diagram.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public class Service { public string Name { get; set; } = string.Empty; }");
        var resolver = Substitute.For<ISymbolResolver>();
        resolver.ResolveRelatedSymbolAsync(Arg.Any<INamedTypeSymbol>(), Arg.Any<AnalysisContext>())
            .Returns(Task.FromResult<INamedTypeSymbol?>(null));
        var sut = new TypeProcessor(resolver);
        var context = CreateContext(compilation);
        var queue = new Queue<(INamedTypeSymbol Symbol, int Depth)>();
        queue.Enqueue((RoslynTestHelper.GetTypeSymbol(compilation, "Service")!, 0));

        await sut.ProcessTypeQueueAsync(queue, context,
            new AnalysisOptions(MaxDepth: 2, IncludeDependencies: true));

        context.Types.Should().ContainSingle(t => t.Name == "Service");
        context.Relationships.Should().NotContain(r => r.To.Contains("String", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessTypeQueue_UnresolvedRelatedType_ShouldRecordEdgeButNotRecurseIntoIt()
    {
        // An unresolved type has no declaration to walk into. The edge is still meaningful, but
        // enqueuing the error symbol would re-analyze a type that cannot yield members.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public class Service : Ghost { }");
        var resolver = Substitute.For<ISymbolResolver>();
        resolver.ResolveRelatedSymbolAsync(Arg.Any<INamedTypeSymbol>(), Arg.Any<AnalysisContext>())
            .Returns(Task.FromResult<INamedTypeSymbol?>(null));
        var sut = new TypeProcessor(resolver);
        var context = CreateContext(compilation);
        var queue = new Queue<(INamedTypeSymbol Symbol, int Depth)>();
        queue.Enqueue((RoslynTestHelper.GetTypeSymbol(compilation, "Service")!, 0));

        await sut.ProcessTypeQueueAsync(queue, context,
            new AnalysisOptions(MaxDepth: 3, IncludeInheritance: true));

        context.Relationships.Should().ContainSingle(r => r.To.EndsWith("Ghost", StringComparison.Ordinal));
        context.Types.Should().ContainSingle(t => t.Name == "Service");
    }

    // ---------------------------------------------------------------------
    // TypeFilter — namespace attribution rules
    // ---------------------------------------------------------------------

    [Fact]
    public void IsSystemType_ErrorSymbolNamedLikeABclType_ShouldReturnTrue()
    {
        // An unresolved symbol has no namespace to attribute it to, so the well-known-name list is
        // the only signal available.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public class Service { public Dictionary Lookup { get; set; } }");
        var service = RoslynTestHelper.GetTypeSymbol(compilation, "Service")!;
        var symbol = (INamedTypeSymbol)service.GetMembers().OfType<IPropertySymbol>()
            .First(p => p.Name == "Lookup").Type;

        TypeFilter.IsSystemType(symbol).Should().BeTrue();
    }

    [Fact]
    public void IsSystemType_ErrorSymbolWithUserDefinedName_ShouldReturnFalse()
    {
        // Unresolved user types are exactly what workspace discovery exists to find; filtering
        // them as "system" would silently drop them from the diagram.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public class Service : Ghost { }");
        var ghost = RoslynTestHelper.GetTypeSymbol(compilation, "Service")!.BaseType!;

        TypeFilter.IsSystemType(ghost).Should().BeFalse();
    }

    [Fact]
    public void IsSystemType_GlobalNamespaceTypeNamedLikeABclType_ShouldReturnTrue()
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Exception { }");
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Exception")!;

        TypeFilter.IsSystemType(symbol).Should().BeTrue();
    }

    [Fact]
    public void IsSystemType_GlobalNamespaceUserType_ShouldReturnFalse()
    {
        var compilation = RoslynTestHelper.CreateCompilation("public class Widget { }");
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Widget")!;

        TypeFilter.IsSystemType(symbol).Should().BeFalse();
    }

    [Fact]
    public void IsSystemType_MicrosoftExtensionsNamespace_ShouldReturnTrue()
    {
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace Microsoft.Extensions.Hosting; public class HostBuilder { }");
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Microsoft.Extensions.Hosting.HostBuilder")!;

        TypeFilter.IsSystemType(symbol).Should().BeTrue();
    }

    [Fact]
    public void IsSystemType_MicrosoftNamespaceOutsideExtensions_ShouldReturnFalse()
    {
        // Only 'Microsoft.Extensions' is treated as framework noise; other Microsoft namespaces
        // (and user namespaces under 'Microsoft') stay in the diagram.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace Microsoft.Playground; public class Sample { }");
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Microsoft.Playground.Sample")!;

        TypeFilter.IsSystemType(symbol).Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // WorkspaceTypeDiscovery — unreadable files and excluded directories
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(DirectoryReadFailures))]
    public async Task FindTypeDefinitionFile_UnreadableFile_ShouldBeSkippedAndScanContinue(Exception failure)
    {
        // One locked or permission-denied file must not abort the workspace scan, otherwise a
        // single bad file makes every type in the workspace unresolvable.
        // The start directory must really exist because the workspace-root walk uses the physical file
        // system; the scan itself runs entirely against the mocked file system below.
        using var dir = new TestDirectory();
        var root = dir.DirectoryPath;
        _fileSystem.EnumerateFiles(root, "*.cs", Arg.Any<EnumerationOptions>())
            .Returns(["/workspace/Locked.cs", "/workspace/Ghost.cs"]);
        _fileSystem.EnumerateDirectories(root, "*", Arg.Any<EnumerationOptions>()).Returns([]);
        _fileSystem.ReadAllTextAsync("/workspace/Locked.cs", Arg.Any<CancellationToken>()).Throws(failure);
        _fileSystem.ReadAllTextAsync("/workspace/Ghost.cs", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("namespace MyApp; public class Ghost { }"));
        var sut = new WorkspaceTypeDiscovery(_fileSystem);

        var result = await sut.FindTypeDefinitionFileAsync("Ghost", root);

        result.Should().Be("/workspace/Ghost.cs");
    }

    [Fact]
    public async Task FindTypeDefinitionFile_ExcludedSubdirectory_ShouldNotBeScanned()
    {
        // Build-output directories contain generated copies of source types; scanning them would
        // both waste time and risk resolving a type to its obj/ copy.
        // Real start directory for the physical workspace-root walk; the scan uses the mock below.
        using var dir = new TestDirectory();
        var root = dir.DirectoryPath;
        _fileSystem.EnumerateFiles(root, "*.cs", Arg.Any<EnumerationOptions>()).Returns([]);
        _fileSystem.EnumerateDirectories(root, "*", Arg.Any<EnumerationOptions>())
            .Returns(["/workspace/obj", "/workspace/src"]);
        _fileSystem.EnumerateFiles("/workspace/src", "*.cs", Arg.Any<EnumerationOptions>())
            .Returns(["/workspace/src/Ghost.cs"]);
        _fileSystem.EnumerateDirectories("/workspace/src", "*", Arg.Any<EnumerationOptions>()).Returns([]);
        _fileSystem.ReadAllTextAsync("/workspace/src/Ghost.cs", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("namespace MyApp; public class Ghost { }"));
        var sut = new WorkspaceTypeDiscovery(_fileSystem);

        var result = await sut.FindTypeDefinitionFileAsync("Ghost", root);

        result.Should().Be("/workspace/src/Ghost.cs");
        _fileSystem.DidNotReceive().EnumerateFiles("/workspace/obj", Arg.Any<string>(),
            Arg.Any<EnumerationOptions>());
    }

    [Fact]
    public async Task FindTypeDefinitionFile_NameAppearsButTypeIsNotDeclared_ShouldReturnNull()
    {
        // The fast substring pre-filter can match a comment or a usage; Roslyn verification must
        // reject the file so the type is treated as external instead of bound to the wrong file.
        // Real start directory for the physical workspace-root walk; the scan uses the mock below.
        using var dir = new TestDirectory();
        var root = dir.DirectoryPath;
        _fileSystem.EnumerateFiles(root, "*.cs", Arg.Any<EnumerationOptions>())
            .Returns(["/workspace/Decoy.cs"]);
        _fileSystem.EnumerateDirectories(root, "*", Arg.Any<EnumerationOptions>()).Returns([]);
        _fileSystem.ReadAllTextAsync("/workspace/Decoy.cs", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("namespace MyApp; /* class Ghost lives elsewhere */ public class Other { }"));
        var sut = new WorkspaceTypeDiscovery(_fileSystem);

        var result = await sut.FindTypeDefinitionFileAsync("Ghost", root);

        result.Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // MermaidClassDiagramRenderer — identity and enum fallbacks
    // ---------------------------------------------------------------------

    [Fact]
    public void MermaidRenderer_Format_ShouldBeMermaid()
    {
        new MermaidClassDiagramRenderer().Format.Should().Be("mermaid");
    }

    [Fact]
    public void MermaidRenderer_DuplicateTypeFullName_ShouldEmitOneNodeNotACollisionSuffix()
    {
        // The same type can be added twice (e.g. as both a source type and a discovered relative).
        // It must map to a single node id rather than getting a spurious '_2' suffix.
        var model = new ClassModel(
            "dup",
            [
                new TypeDefinition("User", "Models", "Models.User", TypeKind.Class, []),
                new TypeDefinition("User", "Models", "Models.User", TypeKind.Class, [])
            ],
            []);

        var result = new MermaidClassDiagramRenderer().Render(model);

        result.Should().NotContain("Models_User_2");
    }

    [Fact]
    public void MermaidRenderer_InternalMember_ShouldUseTildeVisibilityMarker()
    {
        var model = new ClassModel(
            "vis",
            [
                new TypeDefinition("Cfg", "App", "App.Cfg", TypeKind.Class,
                [
                    new MemberDefinition("Secret", "string", Visibility.Internal, MemberKind.Field)
                ])
            ],
            []);

        var result = new MermaidClassDiagramRenderer().Render(model);

        result.Should().Contain("~string Secret");
    }

    [Fact]
    public void MermaidRenderer_OutOfRangeVisibility_ShouldFallBackToPublicMarker()
    {
        // Defensive default: an unmapped visibility must still render valid Mermaid rather than
        // emitting a stray or empty marker that breaks the diagram.
        var model = new ClassModel(
            "vis",
            [
                new TypeDefinition("Cfg", "App", "App.Cfg", TypeKind.Class,
                [
                    new MemberDefinition("Value", "int", (Visibility)99, MemberKind.Property)
                ])
            ],
            []);

        var result = new MermaidClassDiagramRenderer().Render(model);

        result.Should().Contain("+int Value");
    }

    [Fact]
    public void MermaidRenderer_OutOfRangeRelationshipKind_ShouldFallBackToAssociationArrow()
    {
        var model = new ClassModel(
            "rel",
            [
                new TypeDefinition("A", "App", "App.A", TypeKind.Class, []),
                new TypeDefinition("B", "App", "App.B", TypeKind.Class, [])
            ],
            [
                new Relationship("App.A", "App.B", (RelationshipKind)99)
            ]);

        var result = new MermaidClassDiagramRenderer().Render(model);

        result.Should().Contain("App_A --> App_B");
    }

    [Fact]
    public void MermaidRenderer_RelationshipToTypeOutsideTheModel_ShouldStillEmitASanitizedEndpoint()
    {
        // Relationship endpoints are not guaranteed to be present in Types — a filtered node, say — so
        // the renderer must fall back to plain sanitization instead of dropping the edge.
        var model = new ClassModel(
            "rel",
            [
                new TypeDefinition("A", "App", "App.A", TypeKind.Class, [])
            ],
            [
                new Relationship("App.A", "External.Thing", RelationshipKind.Dependency)
            ]);

        var result = new MermaidClassDiagramRenderer().Render(model);

        result.Should().Contain("App_A ..> External_Thing");
    }
}
