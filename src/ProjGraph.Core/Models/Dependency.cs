namespace ProjGraph.Core.Models;

public enum DependencyType
{
    ProjectReference,
    PackageReference
}

public record Dependency(
    Guid SourceId,
    Guid TargetId,
    DependencyType Type,
    string? Version = null
);
