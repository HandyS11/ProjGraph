using ProjGraph.Core.Models;

namespace ProjGraph.Tests.Unit.Core.Models;

[Trait("Category", "Core")]
public class ProjectTests
{
    [Fact]
    public void Project_ShouldCreateWithAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string name = "TestProject";
        var fullPath = Path.Combine("Projects", "TestProject", "TestProject.csproj");
        var relativePath = Path.Combine("TestProject", "TestProject.csproj");
        const string framework = "net10.0";
        const ProjectType type = ProjectType.Library;

        // Act
        var project = new Project(id, name, fullPath, relativePath, framework, type);

        // Assert
        project.Id.Should().Be(id);
        project.Name.Should().Be(name);
        project.FullPath.Should().Be(fullPath);
        project.RelativePath.Should().Be(relativePath);
        project.Framework.Should().Be(framework);
        project.Type.Should().Be(type);
    }

    [Fact]
    public void Project_ShouldSupportRecordEquality()
    {
        // Arrange
        var id = Guid.NewGuid();
        var project1 = new Project(id, "Test", "path1", "rel1", "net10.0", ProjectType.Library);
        var project2 = new Project(id, "Test", "path1", "rel1", "net10.0", ProjectType.Library);

        // Act & Assert
        project1.Should().Be(project2);
        (project1 == project2).Should().BeTrue();
    }

    [Fact]
    public void Project_ShouldNotBeEqualWithDifferentIds()
    {
        // Arrange
        var project1 = new Project(Guid.NewGuid(), "Test", "path1", "rel1", "net10.0", ProjectType.Library);
        var project2 = new Project(Guid.NewGuid(), "Test", "path1", "rel1", "net10.0", ProjectType.Library);

        // Act & Assert
        project1.Should().NotBe(project2);
    }

    [Fact]
    public void ProjectType_ShouldHaveExpectedValues()
    {
        // Assert
        ((int)ProjectType.Library).Should().Be(0);
        ((int)ProjectType.Executable).Should().Be(1);
        ((int)ProjectType.Test).Should().Be(2);
        ((int)ProjectType.Other).Should().Be(3);
    }
}
