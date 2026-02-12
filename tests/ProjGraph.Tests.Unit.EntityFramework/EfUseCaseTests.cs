using Microsoft.CodeAnalysis;
using NSubstitute;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for EF use cases: <see cref="AnalyzeContextUseCase"/>,
/// <see cref="AnalyzeSnapshotUseCase"/>, <see cref="DiscoverContextsUseCase"/>,
/// and <see cref="DiscoverSnapshotsUseCase"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EfUseCaseTests
{
    private readonly IEfModelAnalyzer _modelAnalyzer = Substitute.For<IEfModelAnalyzer>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();

    [Fact]
    public async Task AnalyzeContextUseCase_InvalidPath_ShouldThrowArgumentException()
    {
        var sut = new AnalyzeContextUseCase(_modelAnalyzer);

        var act = () => sut.ExecuteAsync("/file.txt");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeContextUseCase_ValidPath_ShouldCallModelAnalyzer()
    {
        const string path = "/test/Context.cs";
        var expected = new EfModel();
        _modelAnalyzer.AnalyzeContextAsync(path, null).Returns(expected);
        var sut = new AnalyzeContextUseCase(_modelAnalyzer);

        var result = await sut.ExecuteAsync(path);

        result.Should().BeSameAs(expected);
        await _modelAnalyzer.Received(1).AnalyzeContextAsync(path, null);
    }

    [Fact]
    public async Task AnalyzeContextUseCase_WithContextName_ShouldPassContextName()
    {
        const string path = "/test/Context.cs";
        const string contextName = "AppDbContext";
        var expected = new EfModel();
        _modelAnalyzer.AnalyzeContextAsync(path, contextName).Returns(expected);
        var sut = new AnalyzeContextUseCase(_modelAnalyzer);

        var result = await sut.ExecuteAsync(path, contextName);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task AnalyzeSnapshotUseCase_InvalidPath_ShouldThrowArgumentException()
    {
        var sut = new AnalyzeSnapshotUseCase(_modelAnalyzer);

        var act = () => sut.ExecuteAsync("/file.xml");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeSnapshotUseCase_ValidPath_ShouldCallModelAnalyzer()
    {
        const string path = "/test/Snapshot.cs";
        var expected = new EfModel();
        _modelAnalyzer.AnalyzeSnapshotAsync(path, null).Returns(expected);
        var sut = new AnalyzeSnapshotUseCase(_modelAnalyzer);

        var result = await sut.ExecuteAsync(path);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task DiscoverContextsUseCase_InvalidPath_ShouldThrowArgumentException()
    {
        var sut = new DiscoverContextsUseCase(_modelAnalyzer, _fileSystem);

        var act = () => sut.ExecuteAsync("/file.json");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DiscoverContextsUseCase_ValidPath_ShouldReturnContextNames()
    {
        const string path = "/test/Data.cs";
        const string code = """
                            using Microsoft.EntityFrameworkCore;
                            public class AppDbContext : DbContext { }
                            """;
#pragma warning disable CA1849, S6966
        _fileSystem.ReadAllText(path).Returns(code);
#pragma warning restore CA1849, S6966
        _modelAnalyzer.DiscoverDbContexts(Arg.Any<SyntaxNode>())
            .Returns(["AppDbContext"]);
        var sut = new DiscoverContextsUseCase(_modelAnalyzer, _fileSystem);

        var result = await sut.ExecuteAsync(path);

        result.Should().Contain("AppDbContext");
    }

    [Fact]
    public async Task DiscoverSnapshotsUseCase_InvalidPath_ShouldThrowArgumentException()
    {
        var sut = new DiscoverSnapshotsUseCase(_modelAnalyzer, _fileSystem);

        var act = () => sut.ExecuteAsync("/file.txt");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DiscoverSnapshotsUseCase_ValidPath_ShouldReturnSnapshotNames()
    {
        const string path = "/test/Snapshot.cs";
        const string code = """
                            using Microsoft.EntityFrameworkCore.Infrastructure;
                            public class AppModelSnapshot : ModelSnapshot { }
                            """;
#pragma warning disable CA1849, S6966
        _fileSystem.ReadAllText(path).Returns(code);
#pragma warning restore CA1849, S6966
        _modelAnalyzer.DiscoverModelSnapshots(Arg.Any<SyntaxNode>())
            .Returns(["AppModelSnapshot"]);
        var sut = new DiscoverSnapshotsUseCase(_modelAnalyzer, _fileSystem);

        var result = await sut.ExecuteAsync(path);

        result.Should().Contain("AppModelSnapshot");
    }
}
