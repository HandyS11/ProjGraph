using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Lib.EntityFramework.Application.UseCases;

/// <summary>
/// Use case for analyzing an Entity Framework ModelSnapshot.
/// </summary>
/// <param name="modelAnalyzer">The EF model analyzer used for snapshot analysis.</param>
public class AnalyzeSnapshotUseCase(IEfModelAnalyzer modelAnalyzer)
{
    /// <summary>
    /// Executes the analysis of a specified Entity Framework ModelSnapshot.
    /// </summary>
    /// <param name="path">The file path to the ModelSnapshot class.</param>
    /// <param name="snapshotName">The optional name of the ModelSnapshot class to analyze.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the analyzed Entity Framework model.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided file path is not a .cs file.</exception>
    public async Task<EfModel> ExecuteAsync(string path, string? snapshotName = null)
    {
        FilePathGuard.RequireCsFile(path);

        return await modelAnalyzer.AnalyzeSnapshotAsync(path, snapshotName);
    }
}
