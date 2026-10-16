using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Application.UseCases;

namespace ProjGraph.Lib.EntityFramework.Application;

/// <summary>
/// Service responsible for analyzing Entity Framework Core models architectures.
/// Delegates orchestration to specific use cases.
/// </summary>
/// <param name="analyzeContextUseCase">The use case for analyzing DbContext classes.</param>
/// <param name="discoverContextsUseCase">The use case for discovering DbContext classes.</param>
/// <param name="analyzeSnapshotUseCase">The use case for analyzing ModelSnapshot classes.</param>
/// <param name="discoverSnapshotsUseCase">The use case for discovering ModelSnapshot classes.</param>
public class EfAnalysisService(
    AnalyzeContextUseCase analyzeContextUseCase,
    DiscoverContextsUseCase discoverContextsUseCase,
    AnalyzeSnapshotUseCase analyzeSnapshotUseCase,
    DiscoverSnapshotsUseCase discoverSnapshotsUseCase) : IEfAnalysisService
{
    /// <inheritdoc />
    public Task<List<string>> DiscoverContextsAsync(string path)
    {
        return discoverContextsUseCase.ExecuteAsync(path);
    }

    /// <inheritdoc />
    public Task<EfModel> AnalyzeContextAsync(string path, string? contextName = null)
    {
        return analyzeContextUseCase.ExecuteAsync(path, contextName);
    }

    /// <inheritdoc />
    public Task<List<string>> DiscoverSnapshotsAsync(string path)
    {
        return discoverSnapshotsUseCase.ExecuteAsync(path);
    }

    /// <inheritdoc />
    public Task<EfModel> AnalyzeSnapshotAsync(string path, string? snapshotName = null)
    {
        return analyzeSnapshotUseCase.ExecuteAsync(path, snapshotName);
    }
}
