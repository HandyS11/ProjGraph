using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Lib.EntityFramework.Application.UseCases;

/// <summary>
/// Use case for analyzing a specified DbContext class.
/// </summary>
/// <param name="modelAnalyzer">The EF model analyzer used for context analysis.</param>
public class AnalyzeContextUseCase(IEfModelAnalyzer modelAnalyzer)
{
    /// <summary>
    /// Executes the analysis of a specified DbContext class.
    /// </summary>
    /// <param name="path">The file path to the DbContext class.</param>
    /// <param name="contextName">The optional name of the DbContext class to analyze.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the analyzed Entity Framework model.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided file path is not a .cs file.</exception>
    public async Task<EfModel> ExecuteAsync(string path, string? contextName = null)
    {
        FilePathGuard.RequireCsFile(path);
        return await modelAnalyzer.AnalyzeContextAsync(path, contextName);
    }
}
