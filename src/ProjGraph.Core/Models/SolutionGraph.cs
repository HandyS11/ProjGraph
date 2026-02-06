namespace ProjGraph.Core.Models;

/// <summary>
/// Represents the structure of a solution or project graph, including its name, path, projects, and dependencies.
/// </summary>
/// <param name="Name">The name of the solution or project.</param>
/// <param name="Path">The file path to the solution or project.</param>
/// <param name="Projects">A read-only list of projects contained in the solution or project.</param>
/// <param name="Dependencies">A read-only list of dependencies between the projects in the solution or project.</param>
public record SolutionGraph(
    string Name,
    string Path,
    IReadOnlyList<Project> Projects,
    IReadOnlyList<Dependency> Dependencies
);