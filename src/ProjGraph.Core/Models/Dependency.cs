namespace ProjGraph.Core.Models;

/// <summary>
/// Represents the type of dependency in a project.
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// Represents a project-to-project reference.
    /// </summary>
    ProjectReference = 0,

    /// <summary>
    /// Represents a package reference.
    /// </summary>
    PackageReference = 1
}

/// <summary>
/// Represents a dependency between two entities in a project.
/// </summary>
/// <param name="SourceId">The unique identifier of the source entity.</param>
/// <param name="TargetId">The unique identifier of the target entity.</param>
/// <param name="Type">The type of dependency (e.g., project or package reference).</param>
public record Dependency(
    Guid SourceId,
    Guid TargetId,
    DependencyType Type
);

