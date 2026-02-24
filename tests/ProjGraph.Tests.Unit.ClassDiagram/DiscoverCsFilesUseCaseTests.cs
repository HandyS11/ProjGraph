using NSubstitute;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Tests.Unit.ClassDiagram;

public class DiscoverCsFilesUseCaseTests
{
    private readonly IFileSystem _fileSystemMock = Substitute.For<IFileSystem>();
    private readonly DiscoverCsFilesUseCase _useCase;

    public DiscoverCsFilesUseCaseTests()
    {
        _useCase = new DiscoverCsFilesUseCase(_fileSystemMock);
    }

    [Fact]
    public void Execute_ShouldReturnAllCsFiles_InDirectoryAndSubdirectories()
    {
        // Arrange
        const string root = "C:/root";
        const string sub = "C:/root/sub";
        _fileSystemMock.DirectoryExists(root).Returns(true);
        _fileSystemMock.GetFullPath(root).Returns(root);
        _fileSystemMock.GetDirectories(root).Returns([sub]);
        _fileSystemMock.GetDirectories(sub).Returns(Array.Empty<string>());
        _fileSystemMock.GetFiles(root, "*.cs").Returns(["C:/root/File1.cs"]);
        _fileSystemMock.GetFiles(sub, "*.cs").Returns(["C:/root/sub/File2.cs"]);

        // Act
        var result = _useCase.Execute(root);

        // Assert
        result.Should().BeEquivalentTo("C:/root/File1.cs", "C:/root/sub/File2.cs");
    }

    [Fact]
    public void Execute_ShouldSkipExcludedDirectories()
    {
        // Arrange
        const string root = "C:/root";
        const string bin = "C:/root/bin";
        _fileSystemMock.DirectoryExists(root).Returns(true);
        _fileSystemMock.GetFullPath(root).Returns(root);
        _fileSystemMock.GetDirectories(root).Returns([bin]); // This 'bin' will be detected by ShouldSkipDirectory
        _fileSystemMock.GetFiles(root, "*.cs").Returns(["C:/root/File1.cs"]);

        // Act
        var result = _useCase.Execute(root);

        // Assert
        result.Should().BeEquivalentTo("C:/root/File1.cs");
        _fileSystemMock.DidNotReceive().GetFiles(bin, Arg.Any<string>());
        _fileSystemMock.DidNotReceive().GetDirectories(bin);
    }

    [Fact]
    public void Execute_ShouldThrowDirectoryNotFoundException_IfDirectoryDoesNotExist()
    {
        // Arrange
        _fileSystemMock.DirectoryExists(Arg.Any<string>()).Returns(false);

        // Act & Assert
        _useCase.Invoking(u => u.Execute("invalid"))
            .Should().Throw<DirectoryNotFoundException>();
    }
}
