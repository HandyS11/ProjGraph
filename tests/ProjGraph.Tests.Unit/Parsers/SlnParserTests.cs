using FluentAssertions;
using ProjGraph.Lib.Parsers;

namespace ProjGraph.Tests.Unit.Parsers;

public class SlnParserTests
{
    [Fact]
    public void GetProjectPaths_ShouldExtractPathsFromSln()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var tempSln = Path.Combine(tempDir, "TestSolution.sln");
        var srcDir = Path.Combine(tempDir, "src");
        var testsDir = Path.Combine(tempDir, "tests");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(testsDir);

        // Create dummy project files
        var projAPath = Path.Combine(srcDir, "ProjA", "ProjA.csproj");
        var projBPath = Path.Combine(srcDir, "ProjB", "ProjB.csproj");
        var projTestPath = Path.Combine(testsDir, "ProjA.Tests", "ProjA.Tests.csproj");

        Directory.CreateDirectory(Path.GetDirectoryName(projAPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(projBPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(projTestPath)!);

        File.WriteAllText(projAPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        File.WriteAllText(projBPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        File.WriteAllText(projTestPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        // Create a valid .sln file
        var csharpProjectTypeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
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

        try
        {
            // Act
            var paths = SlnParser.GetProjectPaths(tempSln).ToList();

            // Assert
            paths.Should().HaveCount(3);
            paths.Any(p => p.EndsWith("ProjA.csproj")).Should().BeTrue();
            paths.Any(p => p.EndsWith("ProjB.csproj")).Should().BeTrue();
            paths.Any(p => p.EndsWith("ProjA.Tests.csproj")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void GetProjectPaths_ShouldReturnEmpty_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.sln");

        // Act
        var paths = SlnParser.GetProjectPaths(nonExistentPath).ToList();

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void GetProjectPaths_ShouldFilterNonMSBuildProjects()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var tempSln = Path.Combine(tempDir, "TestSolution.sln");
        var srcDir = Path.Combine(tempDir, "src");
        Directory.CreateDirectory(srcDir);

        // Create dummy project files
        var csProjPath = Path.Combine(srcDir, "CsProj", "CsProj.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(csProjPath)!);
        File.WriteAllText(csProjPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

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

        try
        {
            // Act
            var paths = SlnParser.GetProjectPaths(tempSln).ToList();

            // Assert
            paths.Should().HaveCount(1);
            paths.Single().Should().EndWith("CsProj.csproj");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}