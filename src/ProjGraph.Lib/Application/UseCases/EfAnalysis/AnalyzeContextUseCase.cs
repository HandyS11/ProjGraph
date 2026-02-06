using ProjGraph.Core.Models;
using ProjGraph.Lib.Application.Interfaces;

namespace ProjGraph.Lib.Application.UseCases.EfAnalysis;

/// <summary>
/// Use case for analyzing a specified DbContext class.
/// </summary>
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
        ValidateCsFilePath(path);
        return await modelAnalyzer.AnalyzeContextAsync(path, contextName);
    }

    /// <summary>
    /// Validates that the provided file path points to a C# source file.
    /// </summary>
    /// <param name="path">The file path to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the file path does not end with a .cs extension.</exception>
    private static void ValidateCsFilePath(string path)
    {
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only .cs files are supported", nameof(path));
        }
    }
}