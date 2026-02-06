using ProjGraph.Core.Models;
using ProjGraph.Lib.Application.Interfaces;
using ProjGraph.Lib.Application.UseCases.SolutionGraph;

namespace ProjGraph.Lib.Application.Services;

/// <summary>
/// Service responsible for building a solution graph from a given file path.
/// </summary>
public class GraphService(BuildGraphUseCase buildGraphUseCase) : IGraphService
{
    /// <inheritdoc />
    public SolutionGraph BuildGraph(string path)
    {
        return buildGraphUseCase.Execute(path);
    }
}
