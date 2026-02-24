using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.Core.Parsers;

[Trait("Category", "Core")]
public class ProjectParserTests
{
    private readonly ProjectParser _parser = new(new PhysicalFileSystem());

    [Fact]
    public void Parse_ShouldIdentifyProjectReferences()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                   <OutputType>Exe</OutputType>
                                 </PropertyGroup>
                                 <ItemGroup>
                                   <ProjectReference Include="../LibA/LibA.csproj" />
                                   <ProjectReference Include="../LibB/LibB.csproj" />
                                 </ItemGroup>
                               </Project>
                               """;

        var tempFile = temp.CreateFile("app.csproj", content);

        // Act
        var (project, references, packages) = _parser.Parse(tempFile);
        var referencesList = references.ToList();
        var packagesList = packages.ToList();

        // Assert
        project.Name.Should().Be("app");
        project.Framework.Should().Be("net10.0");
        project.Type.Should().Be(ProjectType.Executable);
        referencesList.Should().HaveCount(2);
        referencesList.Should().Contain("../LibA/LibA.csproj");
        referencesList.Should().Contain("../LibB/LibB.csproj");
        packagesList.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ShouldIdentifyPackageReferences()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                                 <ItemGroup>
                                   <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                                   <PackageReference Include="Spectre.Console" Version="0.45.0" />
                                 </ItemGroup>
                               </Project>
                               """;

        var tempFile = temp.CreateFile("packages.csproj", content);

        // Act
        var (_, references, packages) = _parser.Parse(tempFile);
        var packagesList = packages.ToList();

        // Assert
        packagesList.Should().HaveCount(2);
        packagesList.Should().Contain(new PackageReference("Newtonsoft.Json", "13.0.1"));
        packagesList.Should().Contain(new PackageReference("Spectre.Console", "0.45.0"));
        references.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ShouldHandleLibraryType()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                               </Project>
                               """;

        var tempFile = temp.CreateFile("lib.csproj", content);

        // Act
        var (project, _, _) = _parser.Parse(tempFile);

        // Assert
        project.Type.Should().Be(ProjectType.Library);
    }

    [Fact]
    public void Parse_ShouldIdentifyTestProjectByName()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                               </Project>
                               """;

        var tempFile = temp.CreateFile("MyProject.Tests.csproj", content);

        // Act
        var (project, _, _) = _parser.Parse(tempFile);

        // Assert
        project.Type.Should().Be(ProjectType.Test);
    }

    [Fact]
    public void Parse_ShouldIdentifyTestProjectByProperty()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                   <IsTestProject>true</IsTestProject>
                                 </PropertyGroup>
                               </Project>
                               """;

        var tempFile = temp.CreateFile("test-prop.csproj", content);

        // Act
        var (project, _, _) = _parser.Parse(tempFile);

        // Assert
        project.Type.Should().Be(ProjectType.Test);
    }

    [Fact]
    public void Parse_ShouldHandleMultiTargetFrameworks()
    {
        // Arrange
        using var temp = new TestDirectory();
        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
                                 </PropertyGroup>
                               </Project>
                               """;

        var tempFile = temp.CreateFile("multi.csproj", content);

        // Act
        var (project, _, _) = _parser.Parse(tempFile);

        // Assert
        project.Framework.Should().Be("net8.0;net9.0;net10.0");
    }

    [Fact]
    public void Parse_ShouldHandleProjectWithNoReferences()
    {
        // Arrange
        using var temp = new TestDirectory();

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                               </Project>
                               """;

        var tempFile = temp.CreateFile("no-refs.csproj", content);

        // Act
        var (project, references, _) = _parser.Parse(tempFile);
        var referencesList = references.ToList();

        // Assert
        project.Should().NotBeNull();
        referencesList.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ShouldThrowWhenProjectFileDoesNotExist()
    {
        // Arrange
        using var temp = new TestDirectory();
        var nonExistentFile = Path.Combine(temp.DirectoryPath, "non-existent.csproj");

        // Act & Assert
        var act = () => _parser.Parse(nonExistentFile);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_ShouldThrowForInvalidXml()
    {
        // Arrange
        using var temp = new TestDirectory();

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 <!-- Missing closing tags
                               """;

        var tempFile = temp.CreateFile("invalid.csproj", content);

        // Act & Assert
        var act = () => _parser.Parse(tempFile);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_ShouldReturnUnknownFrameworkWhenNotSpecified()
    {
        // Arrange
        using var temp = new TestDirectory();

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                 </PropertyGroup>
                               </Project>
                               """;

        var tempFile = temp.CreateFile("no-framework.csproj", content);

        // Act
        var (project, _, _) = _parser.Parse(tempFile);

        // Assert
        project.Framework.Should().Be("unknown");
    }

    [Fact]
    public void Parse_ShouldGenerateDeterministicIds()
    {
        // Arrange
        using var temp = new TestDirectory();

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                               </Project>
                               """;

        var tempFile = temp.CreateFile("ids.csproj", content);

        // Act
        var (project1, _, _) = _parser.Parse(tempFile);
        var (project2, _, _) = _parser.Parse(tempFile);

        // Assert - same path should produce same ID (deterministic)
        project1.Id.Should().Be(project2.Id);
    }

    [Fact]
    public void Parse_ShouldGenerateUniqueIds_ForDifferentFiles()
    {
        // Arrange
        using var temp = new TestDirectory();

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                               </Project>
                               """;

        var file1 = temp.CreateFile("first.csproj", content);
        var file2 = temp.CreateFile("second.csproj", content);

        // Act
        var (project1, _, _) = _parser.Parse(file1);
        var (project2, _, _) = _parser.Parse(file2);

        // Assert - different paths should produce different IDs
        project1.Id.Should().NotBe(project2.Id);
    }

    [Fact]
    public void Parse_ShouldSetCorrectPaths()
    {
        // Arrange
        using var temp = new TestDirectory();

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                               </Project>
                               """;

        var tempFile = temp.CreateFile("paths.csproj", content);

        // Act
        var (project, _, _) = _parser.Parse(tempFile);

        // Assert
        project.FullPath.Should().Be(tempFile);
        project.RelativePath.Should().NotBeNullOrWhiteSpace();
    }
}
