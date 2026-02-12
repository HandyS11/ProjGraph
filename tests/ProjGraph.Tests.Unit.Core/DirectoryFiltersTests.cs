using ProjGraph.Lib.Core.Infrastructure;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Tests for <see cref="DirectoryFilters"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DirectoryFiltersTests
{
    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData(".git")]
    [InlineData("node_modules")]
    public void ShouldSkipDirectory_ExcludedDirectoryName_ShouldReturnTrue(string dirName)
    {
        DirectoryFilters.ShouldSkipDirectory(dirName).Should().BeTrue();
    }

    [Theory]
    [InlineData("BIN")]
    [InlineData("Obj")]
    [InlineData(".GIT")]
    [InlineData("Node_Modules")]
    public void ShouldSkipDirectory_CaseInsensitive_ShouldReturnTrue(string dirName)
    {
        DirectoryFilters.ShouldSkipDirectory(dirName).Should().BeTrue();
    }

    [Theory]
    [InlineData("src")]
    [InlineData("tests")]
    [InlineData("lib")]
    [InlineData("packages")]
    [InlineData("Controllers")]
    public void ShouldSkipDirectory_NonExcludedDirectory_ShouldReturnFalse(string dirName)
    {
        DirectoryFilters.ShouldSkipDirectory(dirName).Should().BeFalse();
    }

    [Theory]
    [InlineData("/path/to/bin")]
    [InlineData("/project/obj")]
    [InlineData("/repo/.git")]
    public void ShouldSkipDirectory_FullPath_ShouldExtractDirectoryName(string fullPath)
    {
        DirectoryFilters.ShouldSkipDirectory(fullPath).Should().BeTrue();
    }

    [Theory]
    [InlineData("/path/to/src")]
    [InlineData("/project/Controllers")]
    public void ShouldSkipDirectory_FullPathNonExcluded_ShouldReturnFalse(string fullPath)
    {
        DirectoryFilters.ShouldSkipDirectory(fullPath).Should().BeFalse();
    }
}
