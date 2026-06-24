using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSubstitute;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.ClassDiagram;

/// <summary>
/// Tests for <see cref="AnalyzeFileUseCase"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AnalyzeFileUseCaseTests
{
    private readonly ICompilationFactory _compilationFactory = Substitute.For<ICompilationFactory>();
    private readonly ITypeProcessor _typeProcessor = Substitute.For<ITypeProcessor>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly AnalyzeFileUseCase _sut;

    public AnalyzeFileUseCaseTests()
    {
        _sut = new AnalyzeFileUseCase(_compilationFactory, _typeProcessor, _fileSystem);
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ShouldThrowFileNotFoundException()
    {
        _fileSystem.FileExists("/missing.cs").Returns(false);

        var act = () => _sut.ExecuteAsync("/missing.cs");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_ValidFile_ShouldReturnClassModel()
    {
        const string filePath = "/test/Widget.cs";
        const string code = """
                            namespace Test;
                            public class Widget { }
                            """;

        SetupFileSystem(filePath, code);
        SetupCompilationFactory();

        _typeProcessor.ProcessTypeQueueAsync(
                Arg.Any<Queue<(INamedTypeSymbol Symbol, int Depth)>>(),
                Arg.Any<AnalysisContext>(),
                Arg.Any<AnalysisOptions>())
            .ReturnsForAnyArgs(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(filePath);

        result.Title.Should().Be("Widget.cs");
        await _typeProcessor.Received(1).ProcessTypeQueueAsync(
            Arg.Any<Queue<(INamedTypeSymbol Symbol, int Depth)>>(),
            Arg.Any<AnalysisContext>(),
            Arg.Any<AnalysisOptions>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassOptionsToTypeProcessor()
    {
        const string filePath = "/test/Svc.cs";
        const string code = """
                            namespace Test;
                            public class Svc { }
                            """;

        SetupFileSystem(filePath, code);
        SetupCompilationFactory();

        _typeProcessor.ProcessTypeQueueAsync(
                Arg.Any<Queue<(INamedTypeSymbol Symbol, int Depth)>>(),
                Arg.Any<AnalysisContext>(),
                Arg.Any<AnalysisOptions>())
            .ReturnsForAnyArgs(Task.CompletedTask);

        await _sut.ExecuteAsync(filePath, new AnalysisOptions(3, false, true, true, false));

        await _typeProcessor.Received(1).ProcessTypeQueueAsync(
            Arg.Any<Queue<(INamedTypeSymbol Symbol, int Depth)>>(),
            Arg.Any<AnalysisContext>(),
            Arg.Is<AnalysisOptions>(o =>
                o.MaxDepth == 3 &&
                !o.IncludeInheritance &&
                o.IncludeDependencies &&
                o.IncludeProperties &&
                !o.IncludeFunctions));
    }

    [Fact]
    public async Task ExecuteAsync_NullDirectory_ShouldUseCurrentDirectory()
    {
        const string filePath = "/orphan.cs";
        const string code = """
                            namespace Test;
                            public class Orphan { }
                            """;

        _fileSystem.FileExists(filePath).Returns(true);
        _fileSystem.GetDirectoryName(filePath).Returns((string?)null);
        _fileSystem.ReadAllTextAsync(filePath).Returns(code);
        SetupCompilationFactory();

        _typeProcessor.ProcessTypeQueueAsync(
                Arg.Any<Queue<(INamedTypeSymbol Symbol, int Depth)>>(),
                Arg.Any<AnalysisContext>(),
                Arg.Any<AnalysisOptions>())
            .ReturnsForAnyArgs(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(filePath);

        result.Should().NotBeNull();
    }

    private void SetupFileSystem(string filePath, string code)
    {
        _fileSystem.FileExists(filePath).Returns(true);
        _fileSystem.GetDirectoryName(filePath).Returns("/test");
        _fileSystem.ReadAllTextAsync(filePath).Returns(code);
    }

    private void SetupCompilationFactory()
    {
        _compilationFactory.CreateCompilation(Arg.Any<IEnumerable<SyntaxTree>>())
            .Returns(callInfo =>
            {
                var trees = callInfo.Arg<IEnumerable<SyntaxTree>>();
                return CSharpCompilation.Create("TestAssembly",
                    trees,
                    RoslynTestHelper.GetDefaultReferences(),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            });
    }
}
