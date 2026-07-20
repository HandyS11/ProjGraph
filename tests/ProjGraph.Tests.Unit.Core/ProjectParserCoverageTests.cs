using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.Core;

/// <summary>
/// Error-path and fallback coverage for <see cref="ProjectParser"/>: malformed project and props
/// files, MSBuild property inheritance from <c>Directory.Build.props</c>, and Central Package
/// Management version resolution from <c>Directory.Packages.props</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProjectParserCoverageTests : IDisposable
{
    private readonly TestDirectory _testDirectory = new();
    private readonly ProjectParser _sut = new(new PhysicalFileSystem());

    public void Dispose()
    {
        _testDirectory.Dispose();
    }

    /// <summary>
    /// Writes a project file inside a uniquely named subdirectory so that MSBuild's global
    /// <c>ProjectRootElement</c> cache never serves a sibling test's file for the same path.
    /// </summary>
    /// <param name="relativePath">The path of the project file, relative to the test directory.</param>
    /// <param name="content">The project file XML.</param>
    /// <returns>The full path of the written project file.</returns>
    private string WriteFile(string relativePath, string content)
    {
        return _testDirectory.CreateFile(relativePath, content);
    }

    [Fact]
    public void Parse_MalformedProjectFile_ShouldThrowParsingException()
    {
        var path = WriteFile("malformed/App.csproj", "<Project><PropertyGroup></Project>");

        var act = () => _sut.Parse(path);

        act.Should().Throw<ParsingException>()
            .WithMessage($"*{path}*");
    }

    [Fact]
    public void Parse_MissingProjectFile_ShouldThrowParsingException()
    {
        var path = Path.Combine(_testDirectory.DirectoryPath, "missing", "Gone.csproj");

        var act = () => _sut.Parse(path);

        act.Should().Throw<ParsingException>();
    }

    [Fact]
    public void Parse_TargetFrameworksPlural_ShouldBeUsedWhenSingularIsAbsent()
    {
        var path = WriteFile("plural/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);

        var (project, _, _) = _sut.Parse(path);

        project.Framework.Should().Be("net8.0;net10.0");
    }

    [Fact]
    public void Parse_WhitespaceTargetFramework_ShouldBeTreatedAsUndefined()
    {
        // A whitespace-only property value counts as "not defined": it must not win over the
        // inheritance/unknown fallback chain, otherwise the graph shows a blank framework.
        var path = WriteFile("blank/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>   </TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var (project, _, _) = _sut.Parse(path);

        project.Framework.Should().Be("unknown");
    }

    [Fact]
    public void Parse_TargetFrameworkInDirectoryBuildProps_ShouldBeInherited()
    {
        WriteFile("inherit/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        var path = WriteFile("inherit/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var (project, _, _) = _sut.Parse(path);

        project.Framework.Should().Be("net10.0");
    }

    [Fact]
    public void Parse_MalformedDirectoryBuildProps_ShouldBeIgnoredAndFallBackToUnknown()
    {
        // An unreadable props file must not abort the parse; the walk continues upward and the
        // framework degrades to "unknown" rather than throwing.
        WriteFile("badprops/Directory.Build.props", "<Project><PropertyGroup></Project>");
        var path = WriteFile("badprops/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var (project, _, _) = _sut.Parse(path);

        project.Framework.Should().Be("unknown");
    }

    [Fact]
    public void Parse_NestedDirectoryBuildProps_NearestValueShouldWin()
    {
        // Both levels define TargetFramework. The walk is nearest-first, so the outer value must
        // not overwrite the inner one already recorded.
        WriteFile("nested/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net6.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteFile("nested/inner/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        var path = WriteFile("nested/inner/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var (project, _, _) = _sut.Parse(path);

        project.Framework.Should().Be("net10.0");
    }

    [Fact]
    public void Parse_IsTestProjectInDirectoryBuildProps_ShouldClassifyAsTest()
    {
        // The project name deliberately avoids the word "Test" so the classification can only come
        // from the inherited IsTestProject property.
        WriteFile("inheritedtest/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
            </Project>
            """);
        var path = WriteFile("inheritedtest/Specs.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var (project, _, _) = _sut.Parse(path);

        project.Type.Should().Be(ProjectType.Test);
    }

    [Fact]
    public void Parse_OutputTypeExeInDirectoryBuildProps_ShouldClassifyAsExecutable()
    {
        WriteFile("inheritedexe/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """);
        var path = WriteFile("inheritedexe/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var (project, _, _) = _sut.Parse(path);

        project.Type.Should().Be(ProjectType.Executable);
    }

    [Fact]
    public void Parse_LocalPropertiesShouldOverrideDirectoryBuildProps()
    {
        WriteFile("override/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net6.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        var path = WriteFile("override/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var (project, _, _) = _sut.Parse(path);

        project.Framework.Should().Be("net10.0");
    }

    [Fact]
    public void Parse_CentralPackageManagement_ShouldResolveVersionFromPackagesProps()
    {
        WriteFile("cpm/Directory.Packages.props",
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Serilog" Version="4.1.0" />
              </ItemGroup>
            </Project>
            """);
        var path = WriteFile("cpm/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" />
              </ItemGroup>
            </Project>
            """);

        var (_, _, packages) = _sut.Parse(path);

        packages.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new PackageReference("Serilog", "4.1.0"));
    }

    [Fact]
    public void Parse_MalformedDirectoryPackagesProps_ShouldYieldUnknownVersion()
    {
        WriteFile("badcpm/Directory.Packages.props", "<Project><ItemGroup></Project>");
        var path = WriteFile("badcpm/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" />
              </ItemGroup>
            </Project>
            """);

        var (_, _, packages) = _sut.Parse(path);

        packages.Should().ContainSingle().Which.Version.Should().Be("unknown");
    }

    [Fact]
    public void Parse_NoDirectoryPackagesProps_ShouldYieldUnknownVersion()
    {
        var path = WriteFile("nocpm/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" />
              </ItemGroup>
            </Project>
            """);

        var (_, _, packages) = _sut.Parse(path);

        packages.Should().ContainSingle().Which.Version.Should().Be("unknown");
    }

    [Fact]
    public void Parse_PackageAbsentFromPackagesProps_ShouldYieldUnknownVersion()
    {
        WriteFile("othercpm/Directory.Packages.props",
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);
        var path = WriteFile("othercpm/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" />
              </ItemGroup>
            </Project>
            """);

        var (_, _, packages) = _sut.Parse(path);

        packages.Should().ContainSingle().Which.Version.Should().Be("unknown");
    }

    [Fact]
    public void Parse_ExplicitPackageVersion_ShouldNotConsultPackagesProps()
    {
        WriteFile("explicit/Directory.Packages.props",
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Serilog" Version="4.1.0" />
              </ItemGroup>
            </Project>
            """);
        var path = WriteFile("explicit/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" Version="3.0.0" />
              </ItemGroup>
            </Project>
            """);

        var (_, _, packages) = _sut.Parse(path);

        packages.Should().ContainSingle().Which.Version.Should().Be("3.0.0");
    }

    [Fact]
    public void Parse_ProjectReferences_ShouldBeReturnedVerbatim()
    {
        var path = WriteFile("refs/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Lib\Lib.csproj" />
              </ItemGroup>
            </Project>
            """);

        var (_, references, _) = _sut.Parse(path);

        references.Should().ContainSingle().Which.Should().Be(@"..\Lib\Lib.csproj");
    }

    [Fact]
    public void Parse_SameProjectTwice_ShouldProduceIdenticalDeterministicId()
    {
        var path = WriteFile("stable/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var (first, _, _) = _sut.Parse(path);
        var (second, _, _) = _sut.Parse(path);

        second.Id.Should().Be(first.Id);
        first.Id.Should().NotBe(Guid.Empty);
    }
}
