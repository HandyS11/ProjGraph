using ProjGraph.Core.Models;
using ProjGraph.Lib.Dependencies.Application.UseCases;

namespace ProjGraph.Lib.Dependencies.Application;

/// <summary>
/// Service that orchestrates graph building and stats computation for a .NET solution or project.
/// </summary>
/// <param name="graphService">Service used to build the dependency graph from a file path.</param>
public sealed class StatsService(IGraphService graphService) : IStatsService
{
    /// <inheritdoc />
    public async Task<SolutionStats> ComputeStatsAsync(
        string path,
        int topN = 5,
        CancellationToken cancellationToken = default)
    {
        var graph = await graphService.BuildGraphAsync(path, false, cancellationToken);
        return ComputeStatsUseCase.Execute(graph, topN);
    }
}
