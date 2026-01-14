namespace ProjGraph.Core.Models;

public enum DependencyType
{
    ProjectReference = 0,
    PackageReference = 1
}

public record Dependency(
    Guid SourceId,
    Guid TargetId,
    DependencyType Type
);
