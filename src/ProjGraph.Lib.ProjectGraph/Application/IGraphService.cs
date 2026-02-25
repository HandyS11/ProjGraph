using ProjGraph.Core.Models;

namespace ProjGraph.Lib.ProjectGraph.Application;

/// <summary>
/// Defines the interface for building a solution or project graph.
/// </summary>
public interface IGraphService
{
    /// <summary>
    /// Builds a solution or project graph from the specified path.
    /// </summary>
    /// <param name="path">The file path to the solution or project.</param>
    /// <param name="includePackages">Whether to include NuGet package dependencies in the graph.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task containing a <see cref="SolutionGraph"/> representing the structure of the solution or project.</returns>
    Task<SolutionGraph> BuildGraphAsync(string path, bool includePackages = false,
        CancellationToken cancellationToken = default);
}
