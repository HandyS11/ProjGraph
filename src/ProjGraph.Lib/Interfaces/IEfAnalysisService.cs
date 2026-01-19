using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Interfaces;

/// <summary>
/// Defines the interface for analyzing Entity Framework contexts and models.
/// </summary>
public interface IEfAnalysisService
{
    /// <summary>
    /// Discovers all Entity Framework contexts in the specified path.
    /// </summary>
    /// <param name="path">The file path to search for Entity Framework contexts.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of discovered context names.</returns>
    Task<List<string>> DiscoverContextsAsync(string path);

    /// <summary>
    /// Analyzes the specified Entity Framework context and retrieves its model.
    /// </summary>
    /// <param name="path">The file path to the project or solution containing the context.</param>
    /// <param name="contextName">The name of the context to analyze. If null, the default context is analyzed.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the analyzed Entity Framework model.</returns>
    Task<EfModel> AnalyzeContextAsync(string path, string? contextName = null);
}