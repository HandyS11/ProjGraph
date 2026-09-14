using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.Core.Parsers;

/// <summary>
/// Pins how <see cref="ProjectParser"/> reads project and props XML. The expected values were
/// recorded from the Microsoft.Build-backed implementation, so they document existing behaviour
/// (including quirks such as untrimmed values and first-in-document-order wins) rather than MSBuild
/// evaluation semantics. They must pass unchanged on the System.Xml.Linq implementation.
/// </summary>
[Trait("Category", "Core")]
public sealed class ProjectParserCharacterizationTests : IDisposable
{
    private readonly TestDirectory _temp = new();
    private readonly ProjectParser _parser = new(new PhysicalFileSystem());

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void Parse_LegacyMsBuildNamespace_ShouldReadPropertiesAndItems()
    {
        var path = _temp.CreateFile("legacy/Legacy.csproj",
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <TargetFramework>net48</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\A\A.csproj" />
                <PackageReference Include="P1" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var (project, references, packages) = _parser.Parse(path);

        project.Framework.Should().Be("net48");
        project.Type.Should().Be(ProjectType.Executable);
        references.Should().Equal(@"..\A\A.csproj");
        packages.Should().Equal(new PackageReference("P1", "1.0.0"));
    }

    [Fact]
    public void Parse_VersionMetadata_ShouldReadAttributeOrChildElementCaseSensitively()
    {
        var path = _temp.CreateFile("version/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Attribute" Version="1.0.0" />
                <PackageReference Include="Child">
                  <Version>2.0.0</Version>
                </PackageReference>
                <PackageReference Include="LowerAttribute" version="3.0.0" />
                <PackageReference Include="LowerChild">
                  <version>4.0.0</version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        var (_, _, packages) = _parser.Parse(path);

        packages.Should().Equal(
            new PackageReference("Attribute", "1.0.0"),
            new PackageReference("Child", "2.0.0"),
            new PackageReference("LowerAttribute", "unknown"),
            new PackageReference("LowerChild", "unknown"));
    }

    [Fact]
    public void Parse_UpdateAndRemoveItems_ShouldBeReturnedWithAnEmptyName()
    {
        var path = _temp.CreateFile("update/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Update="Updated" Version="1.0.0" />
                <PackageReference Remove="Removed" />
                <PackageReference Include="Included" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """);

        var (_, _, packages) = _parser.Parse(path);

        packages.Should().Equal(
            new PackageReference("", "1.0.0"),
            new PackageReference("", "unknown"),
            new PackageReference("Included", "2.0.0"));
    }

    [Fact]
    public void Parse_NameCasing_ShouldMatchPropertiesCaseInsensitivelyAndItemTypesCaseSensitively()
    {
        var path = _temp.CreateFile("casing/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <targetFRAMEWORK>net10.0</targetFRAMEWORK>
                <outputtype>exe</outputtype>
                <istestproject>TRUE</istestproject>
              </PropertyGroup>
              <ItemGroup>
                <projectreference Include="Lower.csproj" />
                <packagereference Include="Lower" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var (project, references, packages) = _parser.Parse(path);

        project.Framework.Should().Be("net10.0");
        project.Type.Should().Be(ProjectType.Test);
        references.Should().BeEmpty();
        packages.Should().BeEmpty();
    }

    [Fact]
    public void Parse_PropertyValueText_ShouldBeUntrimmedWithCommentsRemovedAndEntitiesDecoded()
    {
        var path = _temp.CreateFile("text/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>  net10.0  </TargetFramework>
                <OutputType>
                </OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="A&amp;B" Version="1.0.0" />
                <PackageReference Include="Commented">
                  <Version><!-- pinned -->2.0.0</Version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        var (project, _, packages) = _parser.Parse(path);

        project.Framework.Should().Be("  net10.0  ");
        project.Type.Should().Be(ProjectType.Library);
        packages.Should().Equal(
            new PackageReference("A&B", "1.0.0"),
            new PackageReference("Commented", "2.0.0"));
    }

    [Fact]
    public void Parse_PropertiesAndItemsInsideTarget_ShouldCountInDocumentOrder()
    {
        var path = _temp.CreateFile("target/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <Target Name="Custom">
                <PropertyGroup>
                  <TargetFramework>net6.0</TargetFramework>
                </PropertyGroup>
                <ItemGroup>
                  <ProjectReference Include="InTarget.csproj" />
                  <PackageReference>
                    <Version>1.0.0</Version>
                  </PackageReference>
                </ItemGroup>
              </Target>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var (project, references, packages) = _parser.Parse(path);

        project.Framework.Should().Be("net6.0");
        references.Should().Equal("InTarget.csproj");
        packages.Should().Equal(new PackageReference("", "1.0.0"));
    }

    [Fact]
    public void Parse_ChooseWhenOtherwise_ShouldReadEveryBranch()
    {
        var path = _temp.CreateFile("choose/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <Choose>
                <When Condition="'$(Configuration)' == 'Debug'">
                  <PropertyGroup>
                    <TargetFramework>net7.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="When.csproj" />
                  </ItemGroup>
                </When>
                <Otherwise>
                  <ItemGroup>
                    <ProjectReference Include="Otherwise.csproj" />
                  </ItemGroup>
                </Otherwise>
              </Choose>
            </Project>
            """);

        var (project, references, _) = _parser.Parse(path);

        project.Framework.Should().Be("net7.0");
        references.Should().Equal("When.csproj", "Otherwise.csproj");
    }

    [Fact]
    public void Parse_SamePropertyInSeveralPropertyGroups_FirstInDocumentOrderShouldWinRegardlessOfCondition()
    {
        var path = _temp.CreateFile("multi/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup Condition="'$(Configuration)' == 'Never'">
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <PropertyGroup>
                <TargetFramework Condition="false">net9.0</TargetFramework>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var (project, _, _) = _parser.Parse(path);

        project.Framework.Should().Be("net8.0");
    }

    [Fact]
    public void Parse_DirectoryBuildPropsAtTwoLevels_ShouldMergeDifferentPropertiesNearestFirst()
    {
        _temp.CreateFile("repo/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net6.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """);
        _temp.CreateFile("repo/src/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        var path = _temp.CreateFile("repo/src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var (project, _, _) = _parser.Parse(path);

        project.Framework.Should().Be("net10.0");
        project.Type.Should().Be(ProjectType.Executable);
    }

    [Fact]
    public void Parse_DirectoryPackagesProps_ShouldMatchNamesCaseInsensitivelyAndKeepWalkingUp()
    {
        _temp.CreateFile("cpm/Directory.Packages.props",
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="outer.package">
                  <Version>2.0.0</Version>
                </PackageVersion>
              </ItemGroup>
            </Project>
            """);
        _temp.CreateFile("cpm/src/Directory.Packages.props",
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="serilog" Version="4.1.0" />
              </ItemGroup>
            </Project>
            """);
        var path = _temp.CreateFile("cpm/src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" />
                <PackageReference Include="Outer.Package" />
              </ItemGroup>
            </Project>
            """);

        var (_, _, packages) = _parser.Parse(path);

        packages.Should().Equal(
            new PackageReference("Serilog", "4.1.0"),
            new PackageReference("Outer.Package", "2.0.0"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<Project><PropertyGroup></Project>")]
    [InlineData("<NotAProject><PropertyGroup /></NotAProject>")]
    [InlineData("<Project xmlns=\"http://example.com/other\" />")]
    [InlineData("<Project><ItemGroup><PackageReference /></ItemGroup></Project>")]
    [InlineData("<Project><ItemGroup><ProjectReference Include=\"\" /></ItemGroup></Project>")]
    [InlineData("<Project><Choose><When Condition=\"true\"><ItemGroup><PackageReference /></ItemGroup></When></Choose></Project>")]
    public void Parse_InvalidProjectXml_ShouldThrowParsingException(string content)
    {
        var path = _temp.CreateFile($"invalid-{Guid.NewGuid():N}/App.csproj", content);

        var act = () => _parser.Parse(path);

        act.Should().Throw<ParsingException>().WithMessage($"*{path}*");
    }

    [Fact]
    public void Parse_InvalidDirectoryBuildProps_ShouldBeSkippedWhileTheWalkContinues()
    {
        _temp.CreateFile("skip/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        _temp.CreateFile("skip/src/Directory.Build.props", "<NotAProject />");
        var path = _temp.CreateFile("skip/src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var (project, _, _) = _parser.Parse(path);

        project.Framework.Should().Be("net10.0");
    }
}
