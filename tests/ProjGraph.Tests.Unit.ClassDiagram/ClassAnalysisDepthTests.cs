using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.ClassDiagram;

[Trait("Category", "ClassDiagram")]
public sealed class ClassAnalysisDepthTests : IDisposable
{
    private readonly TestDirectory _temp = new();
    private readonly string _tempRoot;
    private readonly ClassAnalysisService _service;

    public ClassAnalysisDepthTests()
    {
        _tempRoot = _temp.DirectoryPath;
        var workspaceTypeDiscovery = new WorkspaceTypeDiscovery();
        var symbolResolver = new SymbolResolver(workspaceTypeDiscovery);
        var compilationFactory = new CompilationFactory();
        var fileSystem = new PhysicalFileSystem();
        var typeProcessor = new TypeProcessor(symbolResolver);
        var analyzeFileUseCase = new AnalyzeFileUseCase(compilationFactory, typeProcessor, fileSystem);
        var discoverCsFilesUseCase = new DiscoverCsFilesUseCase(fileSystem);
        var analyzeDirectoryUseCase =
            new AnalyzeDirectoryUseCase(discoverCsFilesUseCase, compilationFactory, typeProcessor, fileSystem);
        _service = new ClassAnalysisService(analyzeFileUseCase, analyzeDirectoryUseCase);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _temp.Dispose();
        }
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithDepthLimit_DoesNotExceedDepth()
    {
        // Root -> A -> B -> C
        var fileA = Path.Combine(_tempRoot, "A.cs");
        var fileB = Path.Combine(_tempRoot, "B.cs");
        var fileC = Path.Combine(_tempRoot, "C.cs");

        await File.WriteAllTextAsync(fileA, "public class A : B {}");
        await File.WriteAllTextAsync(fileB, "public class B : C {}");
        await File.WriteAllTextAsync(fileC, "public class C {}");

        // Depth 1: Should find A and B, but not C
        var result = await _service.AnalyzeFileAsync(fileA, new AnalysisOptions(IncludeInheritance: true));

        result.Types.Should().Contain(t => t.Name == "A");
        result.Types.Should().Contain(t => t.Name == "B");
        result.Types.Should().NotContain(t => t.Name == "C");
    }

    [Fact]
    public async Task AnalyzeFileAsync_DepthZero_ReturnsOnlyRootType()
    {
        var fileA = Path.Combine(_tempRoot, "A.cs");
        var fileB = Path.Combine(_tempRoot, "B.cs");

        await File.WriteAllTextAsync(fileA, "public class A : B {}");
        await File.WriteAllTextAsync(fileB, "public class B {}");

        var result = await _service.AnalyzeFileAsync(fileA, new AnalysisOptions(0, true));

        result.Types.Should().Contain(t => t.Name == "A");
        result.Types.Should().NotContain(t => t.Name == "B");
    }

    [Fact]
    public async Task AnalyzeFileAsync_DepthTwo_TraversesFullChain()
    {
        var fileA = Path.Combine(_tempRoot, "A.cs");
        var fileB = Path.Combine(_tempRoot, "B.cs");
        var fileC = Path.Combine(_tempRoot, "C.cs");

        await File.WriteAllTextAsync(fileA, "public class A : B {}");
        await File.WriteAllTextAsync(fileB, "public class B : C {}");
        await File.WriteAllTextAsync(fileC, "public class C {}");

        var result = await _service.AnalyzeFileAsync(fileA, new AnalysisOptions(2, true));

        result.Types.Should().Contain(t => t.Name == "A");
        result.Types.Should().Contain(t => t.Name == "B");
        result.Types.Should().Contain(t => t.Name == "C");
    }
}
