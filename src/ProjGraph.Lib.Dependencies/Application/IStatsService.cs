using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Dependencies.Application;

/// <summary>
/// Service responsible for computing architectural statistics for a .NET solution or project.
/// </summary>
public interface IStatsService
{
    /// <summary>
    /// Analyses the solution or project at the specified path and returns key architectural metrics.
    /// </summary>
    /// <param name="path">Absolute path to a <c>.sln</c>, <c>.slnx</c>, or <c>.csproj</c> file.</param>
    /// <param name="topN">Number of top most-referenced projects to include in the hotspot list. Defaults to 5.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="SolutionStats"/> snapshot containing all computed metrics.</returns>
    Task<SolutionStats> ComputeStatsAsync(string path, int topN = 5, CancellationToken cancellationToken = default);
}
