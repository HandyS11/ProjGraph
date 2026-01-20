using FluentAssertions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Parsers;

namespace ProjGraph.Tests.Unit.Parsers;

public class ProjectParserTests
{
    [Fact]
    public void Parse_ShouldIdentifyProjectReferences()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");

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

        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var (project, references) = ProjectParser.Parse(tempFile);

            // Assert
            project.Name.Should().Be(Path.GetFileNameWithoutExtension(tempFile));
            project.Framework.Should().Be("net10.0");
            project.Type.Should().Be(ProjectType.Executable);
            references.Should().HaveCount(2);
            references.Should().Contain("../LibA/LibA.csproj");
            references.Should().Contain("../LibB/LibB.csproj");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Parse_ShouldHandleLibraryType()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                               </Project>
                               """;

        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var (project, _) = ProjectParser.Parse(tempFile);

            // Assert
            project.Type.Should().Be(ProjectType.Library);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Parse_ShouldIdentifyTestProjectByName()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"MyProject.Tests.{Guid.NewGuid()}.csproj");

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                               </Project>
                               """;

        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var (project, _) = ProjectParser.Parse(tempFile);

            // Assert
            project.Type.Should().Be(ProjectType.Test);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Parse_ShouldIdentifyTestProjectByProperty()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                   <IsTestProject>true</IsTestProject>
                                 </PropertyGroup>
                               </Project>
                               """;

        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var (project, _) = ProjectParser.Parse(tempFile);

            // Assert
            project.Type.Should().Be(ProjectType.Test);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Parse_ShouldHandleMultiTargetFrameworks()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
                                 </PropertyGroup>
                               </Project>
                               """;

        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var (project, _) = ProjectParser.Parse(tempFile);

            // Assert
            project.Framework.Should().Be("net8.0;net9.0;net10.0");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Parse_ShouldHandleProjectWithNoReferences()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                               </Project>
                               """;

        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var (project, references) = ProjectParser.Parse(tempFile);

            // Assert
            project.Should().NotBeNull();
            references.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Parse_ShouldThrowWhenProjectFileDoesNotExist()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");

        // Act & Assert
        var act = () => ProjectParser.Parse(nonExistentFile);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_ShouldThrowForInvalidXml()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 <!-- Missing closing tags
                               """;

        File.WriteAllText(tempFile, content);

        try
        {
            // Act & Assert
            var act = () => ProjectParser.Parse(tempFile);
            act.Should().Throw<Exception>();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Parse_ShouldReturnUnknownFrameworkWhenNotSpecified()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                 </PropertyGroup>
                               </Project>
                               """;

        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var (project, _) = ProjectParser.Parse(tempFile);

            // Assert
            project.Framework.Should().Be("unknown");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Parse_ShouldGenerateUniqueIds()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                               </Project>
                               """;

        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var (project1, _) = ProjectParser.Parse(tempFile);
            var (project2, _) = ProjectParser.Parse(tempFile);

            // Assert
            project1.Id.Should().NotBe(project2.Id);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Parse_ShouldSetCorrectPaths()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csproj");

        const string content = """
                               <Project Sdk="Microsoft.NET.Sdk">
                                 <PropertyGroup>
                                   <TargetFramework>net10.0</TargetFramework>
                                 </PropertyGroup>
                               </Project>
                               """;

        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var (project, _) = ProjectParser.Parse(tempFile);

            // Assert
            project.FullPath.Should().Be(tempFile);
            project.RelativePath.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}