using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.Core.Parsers;

[Trait("Category", "Core")]
public class SlnParserTests
{
    private readonly SlnParser _parser = new(new PhysicalFileSystem());

    [Fact]
    public void GetProjectPaths_ShouldExtractPathsFromSln()
    {
        // Arrange
        using var temp = new TestDirectory();

        var tempSln = Path.Combine(temp.DirectoryPath, "TestSolution.sln");

        // Create dummy project files
        temp.CreateFile("src/ProjA/ProjA.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        temp.CreateFile("src/ProjB/ProjB.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        temp.CreateFile("tests/ProjA.Tests/ProjA.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        // Create a valid .sln file
        const string csharpProjectTypeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        var projAGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
        var projBGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
        var projTestGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

        var content = $"""

                       Microsoft Visual Studio Solution File, Format Version 12.00
                       # Visual Studio Version 17
                       VisualStudioVersion = 17.0.31903.59
                       MinimumVisualStudioVersion = 10.0.40219.1
                       Project("{csharpProjectTypeGuid}") = "ProjA", "src\ProjA\ProjA.csproj", "{projAGuid}"
                       EndProject
                       Project("{csharpProjectTypeGuid}") = "ProjB", "src\ProjB\ProjB.csproj", "{projBGuid}"
                       EndProject
                       Project("{csharpProjectTypeGuid}") = "ProjA.Tests", "tests\ProjA.Tests\ProjA.Tests.csproj", "{projTestGuid}"
                       EndProject
                       Global
                       	GlobalSection(SolutionConfigurationPlatforms) = preSolution
                       		Debug|Any CPU = Debug|Any CPU
                       		Release|Any CPU = Release|Any CPU
                       	EndGlobalSection
                       	GlobalSection(ProjectConfigurationPlatforms) = postSolution
                       		{projAGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                       		{projAGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU
                       		{projBGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                       		{projBGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU
                       		{projTestGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                       		{projTestGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU
                       	EndGlobalSection
                       EndGlobal
                       """;

        File.WriteAllText(tempSln, content);

        // Act
        var paths = _parser.GetProjectPaths(tempSln).ToList();

        // Assert
        paths.Should().HaveCount(3);
        paths.Any(p => p.EndsWith("ProjA.csproj", StringComparison.Ordinal)).Should().BeTrue();
        paths.Any(p => p.EndsWith("ProjB.csproj", StringComparison.Ordinal)).Should().BeTrue();
        paths.Any(p => p.EndsWith("ProjA.Tests.csproj", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Fact]
    public void GetProjectPaths_ShouldReturnEmpty_WhenFileDoesNotExist()
    {
        // Arrange
        using var temp = new TestDirectory();
        var nonExistentPath = temp.GetTempFilePath(".sln");

        // Act
        var paths = _parser.GetProjectPaths(nonExistentPath).ToList();

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void GetProjectPaths_ShouldFilterNonMSBuildProjects()
    {
        // Arrange
        using var temp = new TestDirectory();

        var tempSln = Path.Combine(temp.DirectoryPath, "TestSolution.sln");

        // Create dummy project files
        temp.CreateFile("src/CsProj/CsProj.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        const string csharpProjectTypeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        var csProjGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
        const string solutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
        var folderGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

        // Create a .sln file with a solution folder (non-MSBuild project type)
        var content = $"""

                       Microsoft Visual Studio Solution File, Format Version 12.00
                       Project("{csharpProjectTypeGuid}") = "CsProj", "src\CsProj\CsProj.csproj", "{csProjGuid}"
                       EndProject
                       Project("{solutionFolderGuid}") = "Solution Items", "Solution Items", "{folderGuid}"
                       EndProject
                       Global
                       	GlobalSection(SolutionConfigurationPlatforms) = preSolution
                       		Debug|Any CPU = Debug|Any CPU
                       	EndGlobalSection
                       EndGlobal
                       """;

        File.WriteAllText(tempSln, content);

        // Act
        var paths = _parser.GetProjectPaths(tempSln).ToList();

        // Assert
        paths.Should().HaveCount(1);
        paths.Single().Should().EndWith("CsProj.csproj");
    }
}
