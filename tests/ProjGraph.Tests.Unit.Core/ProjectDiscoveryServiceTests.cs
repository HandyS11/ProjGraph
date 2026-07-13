using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Tests for <see cref="ProjectDiscoveryService"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProjectDiscoveryServiceTests
{
    private readonly IProjectParser _projectParser = Substitute.For<IProjectParser>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IOutputConsole _console = Substitute.For<IOutputConsole>();
    private readonly ILogger<ProjectDiscoveryService> _logger = Substitute.For<ILogger<ProjectDiscoveryService>>();
    private readonly ProjectDiscoveryService _sut;

    public ProjectDiscoveryServiceTests()
    {
        _sut = new ProjectDiscoveryService(_projectParser, _fileSystem, _console, _logger);
    }

    [Fact]
    public void NormalizePath_ShouldReplaceBackslashes()
    {
        var result = _sut.NormalizePath(@"C:\Users\test\project.csproj");

        result.Should().Be("C:/Users/test/project.csproj");
    }

    [Fact]
    public void NormalizePath_ForwardSlashes_ShouldRemainUnchanged()
    {
        const string path = "/home/user/project.csproj";

        var result = _sut.NormalizePath(path);

        result.Should().Be(path);
    }

    [Fact]
    public void ResolveProjectReferencePath_ShouldCombineAndResolve()
    {
        const string projectPath = "/src/app/app.csproj";
        const string refPath = "../lib/lib.csproj";

        _fileSystem.GetDirectoryName(projectPath).Returns("/src/app");
        _fileSystem.Combine("/src/app", Arg.Any<string>()).Returns("/src/app/../lib/lib.csproj");
        _fileSystem.GetFullPath("/src/app/../lib/lib.csproj").Returns("/src/lib/lib.csproj");

        var result = _sut.ResolveProjectReferencePath(projectPath, refPath);

        result.Should().Be("/src/lib/lib.csproj");
    }

    [Fact]
    public void DiscoverProjectsRecursively_SingleProject_ShouldReturnIt()
    {
        const string rootPath = "/src/root.csproj";

        _fileSystem.GetFullPath(rootPath).Returns(rootPath);
        _fileSystem.FileExists(rootPath).Returns(true);

        var project = new Project(Guid.NewGuid(), "Root", rootPath, "root.csproj", "net10.0",
            ProjectType.Library);
        _projectParser.Parse(rootPath)
            .Returns((project, Enumerable.Empty<string>(), Enumerable.Empty<PackageReference>()));

        var result = _sut.DiscoverProjectsRecursively(rootPath).ToList();

        result.Should().HaveCount(1);
        result.Should().Contain(rootPath);
    }

    [Fact]
    public void DiscoverProjectsRecursively_WithReferences_ShouldDiscoverAll()
    {
        const string pathA = "/src/a.csproj";
        const string pathB = "/src/b.csproj";

        _fileSystem.GetFullPath(pathA).Returns(pathA);
        _fileSystem.GetFullPath(pathB).Returns(pathB);
        _fileSystem.FileExists(pathA).Returns(true);
        _fileSystem.FileExists(pathB).Returns(true);
        _fileSystem.GetDirectoryName(pathA).Returns("/src");
        _fileSystem.Combine("/src", Arg.Any<string>()).Returns("/src/b.csproj");
        _fileSystem.GetFullPath("/src/b.csproj").Returns(pathB);

        var projA = new Project(Guid.NewGuid(), "A", pathA, "a.csproj", "net10.0", ProjectType.Library);
        var projB = new Project(Guid.NewGuid(), "B", pathB, "b.csproj", "net10.0", ProjectType.Library);
        _projectParser.Parse(pathA)
            .Returns((projA, (IEnumerable<string>)["b.csproj"], Enumerable.Empty<PackageReference>()));
        _projectParser.Parse(pathB).Returns((projB, Enumerable.Empty<string>(), Enumerable.Empty<PackageReference>()));

        var result = _sut.DiscoverProjectsRecursively(pathA).ToList();

        result.Should().HaveCount(2);
        result.Should().Contain(pathA);
        result.Should().Contain(pathB);
    }

    [Fact]
    public void DiscoverProjectsRecursively_ParseError_ShouldSkipAndWarn()
    {
        const string rootPath = "/src/broken.csproj";

        _fileSystem.GetFullPath(rootPath).Returns(rootPath);
        _fileSystem.FileExists(rootPath).Returns(true);
        _projectParser.Parse(rootPath).Throws(new IOException("parse error"));

        var result = _sut.DiscoverProjectsRecursively(rootPath).ToList();

        result.Should().HaveCount(1); // Root is still in discoveredFullPaths
        _console.Received(1).WriteWarning(Arg.Is<string>(s => s != null && s.Contains("Failed to parse")));
    }

    [Fact]
    public void DiscoverProjectsRecursively_ParsingException_ShouldSkipAndWarn()
    {
        const string rootPath = "/src/malformed.csproj";

        _fileSystem.GetFullPath(rootPath).Returns(rootPath);
        _fileSystem.FileExists(rootPath).Returns(true);
        _projectParser.Parse(rootPath).Throws(new ParsingException("malformed project"));

        var result = _sut.DiscoverProjectsRecursively(rootPath).ToList();

        result.Should().HaveCount(1);
        _console.Received(1).WriteWarning(Arg.Is<string>(s => s != null && s.Contains("Failed to parse")));
    }

    [Fact]
    public void DiscoverProjectsRecursively_MissingFile_ShouldSkip()
    {
        const string rootPath = "/src/missing.csproj";

        _fileSystem.GetFullPath(rootPath).Returns(rootPath);
        _fileSystem.FileExists(rootPath).Returns(false);

        var result = _sut.DiscoverProjectsRecursively(rootPath).ToList();

        result.Should().HaveCount(1); // Root added to discoveredFullPaths initially
    }

    [Fact]
    public void DiscoverProjectsRecursively_InvalidOperationException_ShouldSkipAndWarn()
    {
        const string rootPath = "/src/invalid.csproj";

        _fileSystem.GetFullPath(rootPath).Returns(rootPath);
        _fileSystem.FileExists(rootPath).Returns(true);
        _projectParser.Parse(rootPath).Throws(new InvalidOperationException("invalid xml"));

        var result = _sut.DiscoverProjectsRecursively(rootPath).ToList();

        result.Should().HaveCount(1);
        _console.Received(1).WriteWarning(Arg.Is<string>(s => s != null && s.Contains("Failed to parse")));
    }

    [Fact]
    public void DiscoverProjectsRecursively_DuplicateReferences_ShouldDeduplicateByNormalizedPath()
    {
        const string pathA = "/src/a.csproj";
        const string pathB = "/src/b.csproj";

        _fileSystem.GetFullPath(pathA).Returns(pathA);
        _fileSystem.GetFullPath(pathB).Returns(pathB);
        _fileSystem.FileExists(pathA).Returns(true);
        _fileSystem.FileExists(pathB).Returns(true);
        _fileSystem.GetDirectoryName(pathA).Returns("/src");
        _fileSystem.Combine("/src", Arg.Any<string>()).Returns("/src/b.csproj");
        _fileSystem.GetFullPath("/src/b.csproj").Returns(pathB);

        var projA = new Project(Guid.NewGuid(), "A", pathA, "a.csproj", "net10.0", ProjectType.Library);
        var projB = new Project(Guid.NewGuid(), "B", pathB, "b.csproj", "net10.0", ProjectType.Library);

        // A refs B twice — second reference should be deduplicated
        _projectParser.Parse(pathA).Returns((projA, (IEnumerable<string>)["b.csproj", "b.csproj"],
            Enumerable.Empty<PackageReference>()));
        _projectParser.Parse(pathB).Returns((projB, Enumerable.Empty<string>(), Enumerable.Empty<PackageReference>()));

        var result = _sut.DiscoverProjectsRecursively(pathA).ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public void ResolveProjectReferencePath_NullDirectoryName_ShouldUseEmptyString()
    {
        const string projectPath = "project.csproj";
        const string refPath = "lib.csproj";

        _fileSystem.GetDirectoryName(projectPath).Returns((string?)null);
        _fileSystem.Combine("", Arg.Any<string>()).Returns("lib.csproj");
        _fileSystem.GetFullPath("lib.csproj").Returns("/resolved/lib.csproj");

        var result = _sut.ResolveProjectReferencePath(projectPath, refPath);

        result.Should().Be("/resolved/lib.csproj");
    }
}
