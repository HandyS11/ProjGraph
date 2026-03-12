using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Dependencies.Application.UseCases;

namespace ProjGraph.Tests.Unit.Dependencies;

/// <summary>
/// Tests for <see cref="BuildGraphUseCase"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BuildGraphUseCaseTests
{
    private readonly ISlnParser _slnParser = Substitute.For<ISlnParser>();
    private readonly ISlnxParser _slnxParser = Substitute.For<ISlnxParser>();
    private readonly IProjectParser _projectParser = Substitute.For<IProjectParser>();
    private readonly IProjectDiscoveryService _discoveryService = Substitute.For<IProjectDiscoveryService>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IOutputConsole _console = Substitute.For<IOutputConsole>();
    private readonly ILogger<BuildGraphUseCase> _logger = Substitute.For<ILogger<BuildGraphUseCase>>();
    private readonly BuildGraphUseCase _sut;

    public BuildGraphUseCaseTests()
    {
        _sut = new BuildGraphUseCase(
            _slnParser, _slnxParser, _projectParser,
            _discoveryService, _fileSystem, _console, _logger);
    }

    [Fact]
    public void Execute_FileNotFound_ShouldThrowFileNotFoundException()
    {
        _fileSystem.FileExists("/missing.sln").Returns(false);

        var act = () => _sut.Execute("/missing.sln");

        act.Should().Throw<FileNotFoundException>();
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".xml")]
    [InlineData(".json")]
    [InlineData("")]
    public void Execute_UnsupportedExtension_ShouldThrowArgumentException(string extension)
    {
        const string path = "/test/file";
        var fullPath = path + extension;
        _fileSystem.FileExists(fullPath).Returns(true);

        var act = () => _sut.Execute(fullPath);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unsupported file type*");
    }

    [Fact]
    public void Execute_SlnFile_ShouldParseProjects()
    {
        const string slnPath = "/test/solution.sln";
        const string projPath = "/test/projA.csproj";

        _fileSystem.FileExists(slnPath).Returns(true);
        _slnParser.GetProjectPaths(slnPath).Returns([projPath]);

        _fileSystem.GetFullPath(projPath).Returns(projPath);
        _fileSystem.FileExists(projPath).Returns(true);
        _discoveryService.NormalizePath(projPath).Returns(projPath);

        var project = new Project(Guid.NewGuid(), "ProjA", projPath, "projA.csproj", "net10.0",
            ProjectType.Library);
        _projectParser.Parse(projPath)
            .Returns((project, Enumerable.Empty<string>(), Enumerable.Empty<PackageReference>()));

        var result = _sut.Execute(slnPath);

        result.Projects.Should().HaveCount(1);
        result.Projects[0].Name.Should().Be("ProjA");
        result.Name.Should().Be("solution.sln");
    }

    [Fact]
    public void Execute_SlnxFile_ShouldUseSlnxParser()
    {
        const string slnxPath = "/test/solution.slnx";
        const string projPath = "/test/projA.csproj";

        _fileSystem.FileExists(slnxPath).Returns(true);
        _slnxParser.GetProjectPaths(slnxPath).Returns([projPath]);

        _fileSystem.GetFullPath(projPath).Returns(projPath);
        _fileSystem.FileExists(projPath).Returns(true);
        _discoveryService.NormalizePath(projPath).Returns(projPath);

        var project = new Project(Guid.NewGuid(), "ProjA", projPath, "projA.csproj", "net10.0",
            ProjectType.Library);
        _projectParser.Parse(projPath)
            .Returns((project, Enumerable.Empty<string>(), Enumerable.Empty<PackageReference>()));

        var result = _sut.Execute(slnxPath);

        _slnxParser.Received(1).GetProjectPaths(slnxPath);
        result.Projects.Should().HaveCount(1);
    }

    [Fact]
    public void Execute_CsprojFile_ShouldDiscoverRecursively()
    {
        const string csprojPath = "/test/project.csproj";

        _fileSystem.FileExists(csprojPath).Returns(true);
        _discoveryService.DiscoverProjectsRecursively(csprojPath).Returns([csprojPath]);

        _fileSystem.GetFullPath(csprojPath).Returns(csprojPath);
        _discoveryService.NormalizePath(csprojPath).Returns(csprojPath);

        var project = new Project(Guid.NewGuid(), "Project", csprojPath, "project.csproj", "net10.0",
            ProjectType.Library);
        _projectParser.Parse(csprojPath)
            .Returns((project, Enumerable.Empty<string>(), Enumerable.Empty<PackageReference>()));

        var result = _sut.Execute(csprojPath);

        _discoveryService.Received(1).DiscoverProjectsRecursively(csprojPath);
        result.Projects.Should().HaveCount(1);
    }

    [Fact]
    public void Execute_WithDependencies_ShouldBuildDependencyList()
    {
        const string slnPath = "/test/solution.sln";
        const string pathA = "/test/a.csproj";
        const string pathB = "/test/b.csproj";

        _fileSystem.FileExists(slnPath).Returns(true);
        _slnParser.GetProjectPaths(slnPath).Returns([pathA, pathB]);

        _fileSystem.GetFullPath(pathA).Returns(pathA);
        _fileSystem.GetFullPath(pathB).Returns(pathB);
        _fileSystem.FileExists(pathA).Returns(true);
        _fileSystem.FileExists(pathB).Returns(true);
        _discoveryService.NormalizePath(pathA).Returns(pathA);
        _discoveryService.NormalizePath(pathB).Returns(pathB);
        _discoveryService.ResolveProjectReferencePath(pathA, "../b.csproj").Returns(pathB);

        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var projectA = new Project(idA, "A", pathA, "a.csproj", "net10.0", ProjectType.Library);
        var projectB = new Project(idB, "B", pathB, "b.csproj", "net10.0", ProjectType.Library);
        _projectParser.Parse(pathA).Returns((projectA, (IEnumerable<string>)["../b.csproj"],
            Enumerable.Empty<PackageReference>()));
        _projectParser.Parse(pathB)
            .Returns((projectB, Enumerable.Empty<string>(), Enumerable.Empty<PackageReference>()));

        var result = _sut.Execute(slnPath);

        result.Dependencies.Should().HaveCount(1);
        result.Dependencies[0].SourceId.Should().Be(idA);
        result.Dependencies[0].TargetId.Should().Be(idB);
    }

    [Fact]
    public void Execute_ProjectParseThrowsIOException_ShouldSkipProject()
    {
        const string slnPath = "/test/solution.sln";
        const string pathA = "/test/a.csproj";

        _fileSystem.FileExists(slnPath).Returns(true);
        _slnParser.GetProjectPaths(slnPath).Returns([pathA]);

        _fileSystem.GetFullPath(pathA).Returns(pathA);
        _fileSystem.FileExists(pathA).Returns(true);
        _discoveryService.NormalizePath(pathA).Returns(pathA);
        _projectParser.Parse(pathA).Throws(new IOException("disk error"));

        var result = _sut.Execute(slnPath);

        result.Projects.Should().BeEmpty();
        _console.Received(1).WriteWarning(Arg.Is<string>(s => s.Contains("Skipped")));
    }

    [Fact]
    public void Execute_ProjectFileDoesNotExist_ShouldSkipProject()
    {
        const string slnPath = "/test/solution.sln";
        const string pathA = "/test/missing.csproj";

        _fileSystem.FileExists(slnPath).Returns(true);
        _slnParser.GetProjectPaths(slnPath).Returns([pathA]);

        _fileSystem.GetFullPath(pathA).Returns(pathA);
        _fileSystem.FileExists(pathA).Returns(false);

        var result = _sut.Execute(slnPath);

        result.Projects.Should().BeEmpty();
    }

    [Fact]
    public void Execute_DependencyToUnknownProject_ShouldNotIncludeDependency()
    {
        const string slnPath = "/test/solution.sln";
        const string pathA = "/test/a.csproj";

        _fileSystem.FileExists(slnPath).Returns(true);
        _slnParser.GetProjectPaths(slnPath).Returns([pathA]);

        _fileSystem.GetFullPath(pathA).Returns(pathA);
        _fileSystem.FileExists(pathA).Returns(true);
        _discoveryService.NormalizePath(pathA).Returns(pathA);
        _discoveryService.NormalizePath("/test/unknown.csproj").Returns("/test/unknown.csproj");
        _discoveryService.ResolveProjectReferencePath(pathA, "../unknown.csproj").Returns("/test/unknown.csproj");

        var projectA = new Project(Guid.NewGuid(), "A", pathA, "a.csproj", "net10.0", ProjectType.Library);
        _projectParser.Parse(pathA).Returns((projectA, (IEnumerable<string>)["../unknown.csproj"],
            Enumerable.Empty<PackageReference>()));

        var result = _sut.Execute(slnPath);

        result.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Execute_ShouldSetPathOnResult()
    {
        const string slnPath = "/test/solution.sln";

        _fileSystem.FileExists(slnPath).Returns(true);
        _slnParser.GetProjectPaths(slnPath).Returns([]);

        var result = _sut.Execute(slnPath);

        result.Path.Should().Be(slnPath);
    }

    [Fact]
    public void Execute_WithPackagesRequested_ShouldIncludeDeduplicatedPackages()
    {
        // Arrange
        const string slnPath = "/test/solution.sln";
        const string pathA = "/test/a.csproj";
        const string pathB = "/test/b.csproj";

        _fileSystem.FileExists(slnPath).Returns(true);
        _slnParser.GetProjectPaths(slnPath).Returns([pathA, pathB]);

        _fileSystem.GetFullPath(pathA).Returns(pathA);
        _fileSystem.GetFullPath(pathB).Returns(pathB);
        _fileSystem.FileExists(pathA).Returns(true);
        _fileSystem.FileExists(pathB).Returns(true);
        _discoveryService.NormalizePath(pathA).Returns(pathA);
        _discoveryService.NormalizePath(pathB).Returns(pathB);

        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var projectA = new Project(idA, "A", pathA, "a.csproj", "net10.0", ProjectType.Library);
        var projectB = new Project(idB, "B", pathB, "b.csproj", "net10.0", ProjectType.Library);

        var pkg = new PackageReference("Newtonsoft.Json", "13.0.1");

        _projectParser.Parse(pathA)
            .Returns((projectA, Enumerable.Empty<string>(), (IEnumerable<PackageReference>)[pkg]));
        _projectParser.Parse(pathB)
            .Returns((projectB, Enumerable.Empty<string>(), (IEnumerable<PackageReference>)[pkg]));

        // Act
        var result = _sut.Execute(slnPath, true);

        // Assert
        result.Projects.Should().HaveCount(3); // A, B, and 1 Newtonsoft.Json
        result.Projects.Should().Contain(p => p.Name == "Newtonsoft.Json" && p.Type == ProjectType.Package);
        result.Dependencies.Should().HaveCount(2); // A -> Pkg, B -> Pkg
        result.Dependencies.All(d => d.Type == DependencyType.PackageReference).Should().BeTrue();
    }

    [Fact]
    public void Execute_WithPackagesNotRequested_ShouldExcludePackages()
    {
        // Arrange
        const string slnPath = "/test/solution.sln";
        const string pathA = "/test/a.csproj";

        _fileSystem.FileExists(slnPath).Returns(true);
        _slnParser.GetProjectPaths(slnPath).Returns([pathA]);

        _fileSystem.GetFullPath(pathA).Returns(pathA);
        _fileSystem.FileExists(pathA).Returns(true);
        _discoveryService.NormalizePath(pathA).Returns(pathA);

        var projectA = new Project(Guid.NewGuid(), "A", pathA, "a.csproj", "net10.0", ProjectType.Library);
        var pkg = new PackageReference("Newtonsoft.Json", "13.0.1");

        _projectParser.Parse(pathA)
            .Returns((projectA, Enumerable.Empty<string>(), (IEnumerable<PackageReference>)[pkg]));

        // Act
        var result = _sut.Execute(slnPath);

        // Assert
        result.Projects.Should().HaveCount(1);
        result.Projects.Should().NotContain(p => p.Type == ProjectType.Package);
        result.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Execute_SelfReferencingProject_ShouldCreateDependencyToSelf()
    {
        const string slnPath = "/test/solution.sln";
        const string pathA = "/test/a.csproj";

        _fileSystem.FileExists(slnPath).Returns(true);
        _slnParser.GetProjectPaths(slnPath).Returns([pathA]);

        _fileSystem.GetFullPath(pathA).Returns(pathA);
        _fileSystem.FileExists(pathA).Returns(true);
        _discoveryService.NormalizePath(pathA).Returns(pathA);
        _discoveryService.ResolveProjectReferencePath(pathA, "../a.csproj").Returns(pathA);

        var idA = Guid.NewGuid();
        var projectA = new Project(idA, "A", pathA, "a.csproj", "net10.0", ProjectType.Library);
        _projectParser.Parse(pathA).Returns((projectA, (IEnumerable<string>)["../a.csproj"],
            Enumerable.Empty<PackageReference>()));

        var result = _sut.Execute(slnPath);

        result.Dependencies.Should().HaveCount(1);
        result.Dependencies[0].SourceId.Should().Be(idA);
        result.Dependencies[0].TargetId.Should().Be(idA);
    }
}
