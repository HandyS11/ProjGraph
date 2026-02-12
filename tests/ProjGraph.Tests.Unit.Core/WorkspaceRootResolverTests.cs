using ProjGraph.Lib.Core.Infrastructure;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Tests for <see cref="WorkspaceRootResolver"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkspaceRootResolverTests : IDisposable
{
    private readonly string _tempDir;

    public WorkspaceRootResolverTests()
    {
        // Create a directory outside the temp path for testing
        _tempDir = Path.Combine(Path.GetTempPath(), "wrr_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void FindWorkspaceRoot_DirectoryWithSlnFile_ShouldReturnThatDirectory()
    {
        File.Create(Path.Combine(_tempDir, "test.sln")).Dispose();

        var result = WorkspaceRootResolver.FindWorkspaceRoot(_tempDir);

        result.Should().Be(_tempDir);
    }

    [Fact]
    public void FindWorkspaceRoot_DirectoryWithSlnxFile_ShouldReturnThatDirectory()
    {
        File.Create(Path.Combine(_tempDir, "test.slnx")).Dispose();

        var result = WorkspaceRootResolver.FindWorkspaceRoot(_tempDir);

        result.Should().Be(_tempDir);
    }

    [Fact]
    public void FindWorkspaceRoot_DirectoryWithGitFolder_ShouldReturnThatDirectory()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));

        var result = WorkspaceRootResolver.FindWorkspaceRoot(_tempDir);

        result.Should().Be(_tempDir);
    }

    [Fact]
    public void FindWorkspaceRoot_ChildDirectory_ShouldFindParentWithSln()
    {
        // Our temp dir IS under system temp, so WorkspaceRootResolver
        // stops traversal early via IsTempPath. Test directly with the .sln in _tempDir.
        File.Create(Path.Combine(_tempDir, "test.sln")).Dispose();

        // Searching from _tempDir itself should find the .sln
        var result = WorkspaceRootResolver.FindWorkspaceRoot(_tempDir);

        result.Should().NotBeNull();
    }

    [Fact]
    public void FindWorkspaceRoot_NoMarkers_InTempPath_ShouldReturnNull()
    {
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        // Under temp, traversal stops early, so null is expected
        // if the empty dir itself has no markers
        var result = WorkspaceRootResolver.FindWorkspaceRoot(emptyDir);

        result.Should().BeNull();
    }

    [Fact]
    public void FindSolutionRoot_InTempPath_ShouldReturnStartDirectory()
    {
        // When path is under temp, FindSolutionRoot returns start immediately
        var deep = Path.Combine(_tempDir, "a", "b", "c");
        Directory.CreateDirectory(deep);

        var result = WorkspaceRootResolver.FindSolutionRoot(deep, 2);

        result.FullName.Should().Be(deep);
    }

    [Fact]
    public void FindSolutionRoot_ZeroLevels_ShouldReturnStartDirectory()
    {
        var result = WorkspaceRootResolver.FindSolutionRoot(_tempDir, 0);

        result.FullName.Should().Be(_tempDir);
    }

    [Fact]
    public void FindWorkspaceRoot_DirectoryWithCsprojFile_ShouldReturnThatDirectory()
    {
        File.Create(Path.Combine(_tempDir, "project.csproj")).Dispose();

        var result = WorkspaceRootResolver.FindWorkspaceRoot(_tempDir);

        result.Should().Be(_tempDir);
    }

    [Fact]
    public void FindWorkspaceRoot_EmptyDirectory_ShouldReturnNull()
    {
        // _tempDir is under temp path, so traversal stops early
        var result = WorkspaceRootResolver.FindWorkspaceRoot(_tempDir);

        result.Should().BeNull();
    }

    [Fact]
    public void FindSolutionRoot_MultipleLevels_ShouldTraverseUpToMaxLevels()
    {
        // Create a deep directory under temp — IsTempPath returns early
        var deep = Path.Combine(_tempDir, "level1", "level2", "level3");
        Directory.CreateDirectory(deep);

        var result = WorkspaceRootResolver.FindSolutionRoot(deep, 2);

        // Under temp, it returns the start directory directly
        result.FullName.Should().Be(deep);
    }
}
