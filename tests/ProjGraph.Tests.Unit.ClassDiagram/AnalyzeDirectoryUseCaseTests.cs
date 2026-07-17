using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSubstitute;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.ClassDiagram;

public class AnalyzeDirectoryUseCaseTests
{
    private readonly IDiscoverCsFilesUseCase _discoverMock;
    private readonly ICompilationFactory _compilationFactoryMock;
    private readonly ITypeProcessor _typeProcessorMock;
    private readonly IFileSystem _fileSystemMock;
    private readonly AnalyzeDirectoryUseCase _useCase;

    public AnalyzeDirectoryUseCaseTests()
    {
        _discoverMock = Substitute.For<IDiscoverCsFilesUseCase>();
        _compilationFactoryMock = Substitute.For<ICompilationFactory>();
        _typeProcessorMock = Substitute.For<ITypeProcessor>();
        _fileSystemMock = Substitute.For<IFileSystem>();

        _useCase = new AnalyzeDirectoryUseCase(
            _discoverMock,
            _compilationFactoryMock,
            _typeProcessorMock,
            _fileSystemMock);
    }

    [Fact]
    public async Task ExecuteAsync_TrailingDirectorySeparator_ShouldKeepDirectoryNameAsTitle()
    {
        // "projgraph class ./src/" — GetFullPath preserves the trailing separator, and
        // Path.GetFileName of ".../src/" is "", silently dropping the diagram title.
        const string input = "/proj/src/";
        _fileSystemMock.DirectoryExists(input).Returns(true);
        _fileSystemMock.GetFullPath(input).Returns("/proj/src/");
        _discoverMock.Execute("/proj/src/").Returns([]);

        var result = await _useCase.ExecuteAsync(input);

        result.Title.Should().Be("src");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowDirectoryNotFound_WhenDirectoryDoesNotExist()
    {
        // Arrange
        const string path = "InvalidDir";
        _fileSystemMock.DirectoryExists(path).Returns(false);

        // Act & Assert
        await _useCase.Awaiting(u => u.ExecuteAsync(path))
            .Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyModel_WhenNoCsFilesFound()
    {
        // Arrange
        const string path = "C:/empty";
        _fileSystemMock.DirectoryExists(path).Returns(true);
        _fileSystemMock.GetFullPath(path).Returns(path);
        _discoverMock.Execute(path).Returns([]);

        // Act
        var result = await _useCase.ExecuteAsync(path);

        // Assert
        result.Types.Should().BeEmpty();
        result.Relationships.Should().BeEmpty();
        result.Title.Should().Be("empty");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessAllDiscoveredFiles()
    {
        // Arrange
        const string path = "C:/Project";
        var files = new[]
        {
            "C:/Project/A.cs", "C:/Project/B.cs"
        };
        _fileSystemMock.DirectoryExists(path).Returns(true);
        _fileSystemMock.GetFullPath(path).Returns(path);
        _discoverMock.Execute(path).Returns(files);
        _fileSystemMock.ReadAllTextAsync(Arg.Any<string>()).Returns("class C{}");

        _compilationFactoryMock.CreateCompilation(Arg.Any<IEnumerable<SyntaxTree>>())
            .Returns(callInfo =>
            {
                var trees = callInfo.Arg<IEnumerable<SyntaxTree>>();
                return CSharpCompilation.Create("TestAssembly",
                    trees,
                    RoslynTestHelper.GetDefaultReferences(),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            });

        // Act
        await _useCase.ExecuteAsync(path);

        // Assert
        await _fileSystemMock.Received(1).ReadAllTextAsync("C:/Project/A.cs");
        await _fileSystemMock.Received(1).ReadAllTextAsync("C:/Project/B.cs");
        _compilationFactoryMock.Received(1)
            .CreateCompilation(Arg.Is<IEnumerable<SyntaxTree>>(trees => trees != null && trees.Count() == 2));
        await _typeProcessorMock.Received(1).ProcessTypeQueueAsync(
            Arg.Any<Queue<(INamedTypeSymbol Symbol, int Depth)>>(),
            Arg.Any<AnalysisContext>(),
            Arg.Any<AnalysisOptions>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleMultipleNamespacesAndPartialClasses()
    {
        // Arrange
        const string path = "C:/Project";
        var files = new[]
        {
            "C:/Project/F1.cs", "C:/Project/F2.cs"
        };
        _fileSystemMock.DirectoryExists(path).Returns(true);
        _fileSystemMock.GetFullPath(path).Returns(path);
        _discoverMock.Execute(path).Returns(files);

        // F1.cs: namespace N1 { partial class C {} }
        // F2.cs: namespace N2 { class C {} }
        _fileSystemMock.ReadAllTextAsync("C:/Project/F1.cs").Returns("namespace N1 { public partial class C {} }");
        _fileSystemMock.ReadAllTextAsync("C:/Project/F2.cs").Returns("namespace N2 { public class C {} }");

        _compilationFactoryMock.CreateCompilation(Arg.Any<IEnumerable<SyntaxTree>>())
            .Returns(callInfo =>
            {
                var trees = callInfo.Arg<IEnumerable<SyntaxTree>>();
                return CSharpCompilation.Create("TestAssembly",
                    trees,
                    RoslynTestHelper.GetDefaultReferences(),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            });

        // Act
        await _useCase.ExecuteAsync(path);

        // Assert
        // We verify that the queue contains 2 symbols: N1.C and N2.C
        await _typeProcessorMock.Received(1).ProcessTypeQueueAsync(
            Arg.Is<Queue<(INamedTypeSymbol Symbol, int Depth)>>(q => q != null && q.Count == 2),
            Arg.Any<AnalysisContext>(),
            Arg.Any<AnalysisOptions>());
    }
}
