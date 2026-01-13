namespace ProjGraph.Core.Models;

public record SolutionGraph(
    string Name,
    string Path,
    IReadOnlyList<Project> Projects,
    IReadOnlyList<Dependency> Dependencies
);
