using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Interfaces;

/// <summary>
/// Defines the interface for building a solution or project graph.
/// </summary>
public interface IGraphService
{
    /// <summary>
    /// Builds a solution or project graph from the specified path.
    /// </summary>
    /// <param name="path">The file path to the solution or project.</param>
    /// <returns>A <see cref="SolutionGraph"/> representing the structure of the solution or project.</returns>
    SolutionGraph BuildGraph(string path);
}