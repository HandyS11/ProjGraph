using ProjGraph.Core.Models;

namespace ProjGraph.Lib.EntityFramework.Application.UseCases;

/// <summary>
/// Use case for analyzing an Entity Framework ModelSnapshot.
/// </summary>
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
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only .cs files are supported", nameof(path));
        }

        return await modelAnalyzer.AnalyzeSnapshotAsync(path, snapshotName);
    }
}




