using ProjGraph.Core.Models;
using ProjGraph.Lib.ProjectGraph.Application.UseCases;

namespace ProjGraph.Lib.ProjectGraph.Application;

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