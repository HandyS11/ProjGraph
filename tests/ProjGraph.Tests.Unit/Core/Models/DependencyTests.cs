using ProjGraph.Core.Models;

namespace ProjGraph.Tests.Unit.ProjGraph.Core.Models;

[Trait("Category", "Core")]
public class DependencyTests
{
    [Fact]
    public void Dependency_ShouldCreateWithAllProperties()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        const DependencyType type = DependencyType.ProjectReference;

        // Act
        var dependency = new Dependency(sourceId, targetId, type);

        // Assert
        dependency.SourceId.Should().Be(sourceId);
        dependency.TargetId.Should().Be(targetId);
        dependency.Type.Should().Be(type);
    }

    [Fact]
    public void Dependency_ShouldSupportRecordEquality()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var dependency1 = new Dependency(sourceId, targetId, DependencyType.ProjectReference);
        var dependency2 = new Dependency(sourceId, targetId, DependencyType.ProjectReference);

        // Act & Assert
        dependency1.Should().Be(dependency2);
        (dependency1 == dependency2).Should().BeTrue();
    }

    [Fact]
    public void Dependency_ShouldNotBeEqualWithDifferentSourceIds()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var dependency1 = new Dependency(Guid.NewGuid(), targetId, DependencyType.ProjectReference);
        var dependency2 = new Dependency(Guid.NewGuid(), targetId, DependencyType.ProjectReference);

        // Act & Assert
        dependency1.Should().NotBe(dependency2);
    }

    [Fact]
    public void Dependency_ShouldNotBeEqualWithDifferentTargetIds()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var dependency1 = new Dependency(sourceId, Guid.NewGuid(), DependencyType.ProjectReference);
        var dependency2 = new Dependency(sourceId, Guid.NewGuid(), DependencyType.ProjectReference);

        // Act & Assert
        dependency1.Should().NotBe(dependency2);
    }

    [Fact]
    public void Dependency_ShouldNotBeEqualWithDifferentTypes()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var dependency1 = new Dependency(sourceId, targetId, DependencyType.ProjectReference);
        var dependency2 = new Dependency(sourceId, targetId, DependencyType.PackageReference);

        // Act & Assert
        dependency1.Should().NotBe(dependency2);
    }

    [Fact]
    public void DependencyType_ShouldHaveExpectedValues()
    {
        // Assert
        ((int)DependencyType.ProjectReference).Should().Be(0);
        ((int)DependencyType.PackageReference).Should().Be(1);
    }
}