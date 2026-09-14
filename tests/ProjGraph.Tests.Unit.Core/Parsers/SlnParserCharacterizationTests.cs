using ProjGraph.Core.Exceptions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.Core.Parsers;

/// <summary>
/// Pins which solution entries <see cref="SlnParser"/> returns and how their paths are resolved.
/// The expected values were recorded from Microsoft.Build's <c>SolutionFile</c>
/// (<c>KnownToBeMSBuildFormat</c> projects only) and must pass unchanged on the
/// SolutionPersistence implementation.
/// </summary>
[Trait("Category", "Core")]
public sealed class SlnParserCharacterizationTests
{
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Parsers", "Fixtures");

    private readonly SlnParser _parser = new(new PhysicalFileSystem());

    private static string Expected(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(FixtureDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void GetProjectPaths_ShouldReturnOnlyMsBuildFormatProjectsInFileOrder()
    {
        var paths = _parser.GetProjectPaths(Path.Combine(FixtureDirectory, "characterization.sln")).ToList();

        paths.Should().Equal(
            Expected("src/CsSdk/CsSdk.csproj"),
            Expected("src/CsClassic/CsClassic.csproj"),
            Expected("src/CsLowerGuid/CsLowerGuid.csproj"),
            Expected("src/VbClassic/VbClassic.vbproj"),
            Expected("src/VbSdk/VbSdk.vbproj"),
            Expected("src/FsClassic/FsClassic.fsproj"),
            Expected("src/FsSdk/FsSdk.fsproj"),
            Expected("src/Cps/Cps.msbuildproj"),
            Expected("src/Cpp/Cpp.vcxproj"),
            Expected("src/Database/Database.dbproj"),
            Expected("src/JSharp/JSharp.vjsproj"),
            Expected("src/Synergy/Synergy.synproj"),
            Expected("../Outside/Outside.csproj"),
            Expected("src/Nested/Nested.csproj"));
    }

    [Fact]
    public void GetProjectPaths_RelativeSolutionPath_ShouldStillReturnAbsoluteProjectPaths()
    {
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(),
            Path.Combine(FixtureDirectory, "characterization.sln"));

        var paths = _parser.GetProjectPaths(relative).ToList();

        paths.Should().HaveCount(14).And.OnlyContain(p => Path.IsPathFullyQualified(p));
        paths[0].Should().Be(Expected("src/CsSdk/CsSdk.csproj"));
    }

    [Theory]
    [InlineData("This is not a valid solution file {{{")]
    [InlineData("")]
    [InlineData("\nMicrosoft Visual Studio Solution File, Format Version 12.00\nProject(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Broken\"\nEndProject\n")]
    public void GetProjectPaths_MalformedSolution_ShouldThrowParsingException(string content)
    {
        using var temp = new TestDirectory();
        var path = temp.CreateFile("Broken.sln", content);

        var act = () => _parser.GetProjectPaths(path).ToList();

        act.Should().Throw<ParsingException>().WithMessage($"*{path}*");
    }

    [Fact]
    public void GetProjectPaths_HeaderOnlySolution_ShouldReturnEmpty()
    {
        using var temp = new TestDirectory();
        var path = temp.CreateFile("Empty.sln", "\nMicrosoft Visual Studio Solution File, Format Version 12.00\n");

        _parser.GetProjectPaths(path).Should().BeEmpty();
    }
}
