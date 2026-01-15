using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Interfaces;

public interface IEfAnalysisService
{
    Task<List<string>> DiscoverContextsAsync(string path);
    Task<EfModel> AnalyzeContextAsync(string path, string? contextName = null);
}