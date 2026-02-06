using FluentAssertions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Infrastructure.Parsers;
using ProjGraph.Tests.Unit.Helpers;

namespace ProjGraph.Tests.Unit.Parsers;

public class ProjectParserTests
{
    private readonly ProjectParser _parser = new();
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
        var (project, references) = _parser.Parse(tempFile);

        // Assert
        project.Name.Should().Be("app");
        project.Framework.Should().Be("net10.0");
        project.Type.Should().Be(ProjectType.Executable);
        references.Should().HaveCount(2);
        references.Should().Contain("../LibA/LibA.csproj");
        references.Should().Contain("../LibB/LibB.csproj");
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
        var (project, _) = _parser.Parse(tempFile);

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
        var (project, _) = _parser.Parse(tempFile);

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
        var (project, _) = _parser.Parse(tempFile);

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
        var (project, _) = _parser.Parse(tempFile);

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
        var (project, references) = _parser.Parse(tempFile);

        // Assert
        project.Should().NotBeNull();
        references.Should().BeEmpty();
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
        var (project, _) = _parser.Parse(tempFile);

        // Assert
        project.Framework.Should().Be("unknown");
    }

    [Fact]
    public void Parse_ShouldGenerateUniqueIds()
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
        var (project1, _) = _parser.Parse(tempFile);
        var (project2, _) = _parser.Parse(tempFile);

        // Assert
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
        var (project, _) = _parser.Parse(tempFile);

        // Assert
        project.FullPath.Should().Be(tempFile);
        project.RelativePath.Should().NotBeNullOrWhiteSpace();
    }
}

