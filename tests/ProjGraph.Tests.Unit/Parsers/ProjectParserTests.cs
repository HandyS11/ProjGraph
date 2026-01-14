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
                                   <ProjectReference Include="..\LibA\LibA.csproj" />
                                   <ProjectReference Include="..\LibB\LibB.csproj" />
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
            references.Should().Contain(@"..\LibA\LibA.csproj");
            references.Should().Contain(@"..\LibB\LibB.csproj");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
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
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
