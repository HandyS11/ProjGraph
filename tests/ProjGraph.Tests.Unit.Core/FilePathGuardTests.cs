using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Tests for <see cref="FilePathGuard"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FilePathGuardTests
{
    [Theory]
    [InlineData("file.cs")]
    [InlineData("path/to/file.cs")]
    [InlineData("C:\\Users\\test\\file.cs")]
    [InlineData("FILE.CS")]
    [InlineData("file.Cs")]
    public void RequireCsFile_ValidCsPath_ShouldNotThrow(string path)
    {
        var act = () => FilePathGuard.RequireCsFile(path);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("file.txt")]
    [InlineData("file.csx")]
    [InlineData("file.vb")]
    [InlineData("file")]
    [InlineData("path/to/file.csproj")]
    public void RequireCsFile_NonCsExtension_ShouldThrow(string path)
    {
        var act = () => FilePathGuard.RequireCsFile(path);

        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{path}*");
    }

    [Fact]
    public void RequireCsFile_CustomParamName_ShouldIncludeInException()
    {
        var act = () => FilePathGuard.RequireCsFile("file.txt", "filePath");

        act.Should().Throw<ArgumentException>()
            .And.ParamName.Should().Be("filePath");
    }

    [Fact]
    public void RequireCsFile_DefaultParamName_ShouldBePath()
    {
        var act = () => FilePathGuard.RequireCsFile("file.txt");

        act.Should().Throw<ArgumentException>()
            .And.ParamName.Should().Be("path");
    }

    [Fact]
    public void CSharpExtension_ShouldBeDotCs()
    {
        FilePathGuard.CSharpExtension.Should().Be(".cs");
    }

    [Fact]
    public void CSharpFilesPattern_ShouldBeStarDotCs()
    {
        FilePathGuard.CSharpFilesPattern.Should().Be("*.cs");
    }
}
