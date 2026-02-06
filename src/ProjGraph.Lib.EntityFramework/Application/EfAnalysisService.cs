using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Application.UseCases;

namespace ProjGraph.Lib.EntityFramework.Application;

/// <summary>
/// Service responsible for analyzing Entity Framework Core models architectures.
/// Delegates orchestration to specific use cases.
/// </summary>
public class EfAnalysisService(
    AnalyzeContextUseCase analyzeContextUseCase,
    DiscoverContextsUseCase discoverContextsUseCase,
    AnalyzeSnapshotUseCase analyzeSnapshotUseCase,
    DiscoverSnapshotsUseCase discoverSnapshotsUseCase) : IEfAnalysisService
{
    /// <inheritdoc />
    public async Task<List<string>> DiscoverContextsAsync(string path)
    {
        return await discoverContextsUseCase.ExecuteAsync(path);
    }

    /// <inheritdoc />
    public async Task<EfModel> AnalyzeContextAsync(string path, string? contextName = null)
    {
        return await analyzeContextUseCase.ExecuteAsync(path, contextName);
    }

    /// <inheritdoc />
    public async Task<List<string>> DiscoverSnapshotsAsync(string path)
    {
        return await discoverSnapshotsUseCase.ExecuteAsync(path);
    }

    /// <inheritdoc />
    public async Task<EfModel> AnalyzeSnapshotAsync(string path, string? snapshotName = null)
    {
        return await analyzeSnapshotUseCase.ExecuteAsync(path, snapshotName);
    }
}





