using ProjGraph.Core.Models;

namespace ProjGraph.Lib.EntityFramework.Application;

/// <summary>
/// Defines the interface for analyzing Entity Framework contexts and models.
/// </summary>
public interface IEfAnalysisService
{
    /// <summary>
    /// Analyzes the specified Entity Framework context and retrieves its model.
    /// </summary>
    /// <param name="path">The file path to the project or solution containing the context.</param>
    /// <param name="contextName">The name of the context to analyze. If null, the default context is analyzed.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the analyzed Entity Framework model.</returns>
    Task<EfModel> AnalyzeContextAsync(string path, string? contextName = null);

    /// <summary>
    /// Discovers all Entity Framework contexts in the specified path.
    /// </summary>
    /// <param name="path">The file path to search for Entity Framework contexts.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of discovered context names.</returns>
    Task<List<string>> DiscoverContextsAsync(string path);

    /// <summary>
    /// Discovers all Entity Framework model snapshots in the specified path.
    /// </summary>
    /// <param name="path">The file path to search for Entity Framework model snapshots.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of discovered snapshot names.</returns>
    Task<List<string>> DiscoverSnapshotsAsync(string path);

    /// <summary>
    /// Analyzes the specified Entity Framework model snapshot and retrieves its model.
    /// </summary>
    /// <param name="path">The file path to the C# file containing the model snapshot.</param>
    /// <param name="snapshotName">The name of the snapshot to analyze. If null, the first snapshot found in the file is analyzed.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the analyzed Entity Framework model.</returns>
    Task<EfModel> AnalyzeSnapshotAsync(string path, string? snapshotName = null);
}