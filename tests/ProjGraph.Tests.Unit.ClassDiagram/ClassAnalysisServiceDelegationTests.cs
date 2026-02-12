using NSubstitute;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Tests.Unit.ClassDiagram;

/// <summary>
/// Tests for <see cref="ClassAnalysisService"/> delegation to <see cref="AnalyzeFileUseCase"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ClassAnalysisServiceDelegationTests
{
    private readonly ICompilationFactory _compilationFactory = Substitute.For<ICompilationFactory>();
    private readonly ITypeProcessor _typeProcessor = Substitute.For<ITypeProcessor>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();

    [Fact]
    public async Task AnalyzeFileAsync_ShouldDelegateToUseCase()
    {
        const string filePath = "/test/model.cs";
        _fileSystem.FileExists(filePath).Returns(false);

        var useCase = new AnalyzeFileUseCase(_compilationFactory, _typeProcessor, _fileSystem);
        var sut = new ClassAnalysisService(useCase);

        var act = () => sut.AnalyzeFileAsync(filePath);

        // Since the file doesn't exist, it should throw FileNotFoundException
        // verifying that it delegates to the use case
        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
