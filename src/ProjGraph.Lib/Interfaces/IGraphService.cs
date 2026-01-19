using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Interfaces;

public interface IGraphService
{
    SolutionGraph BuildGraph(string path);
}