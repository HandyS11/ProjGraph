using ProjGraph.Core.Models;
using ProjGraph.Lib.ProjectGraph.Application.UseCases;

namespace ProjGraph.Lib.ProjectGraph.Application;

/// <summary>
/// Service responsible for building a solution graph from a given file path.
/// </summary>
/// <param name="buildGraphUseCase">The use case for building solution graphs.</param>
public class GraphService(BuildGraphUseCase buildGraphUseCase) : IGraphService
{
    /// <inheritdoc />
    public Task<SolutionGraph> BuildGraphAsync(string path, bool includePackages = false,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => buildGraphUseCase.Execute(path, includePackages), cancellationToken);
    }
}
