using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;

namespace ProjGraph.Lib.ClassDiagram.Application;

/// <summary>
/// Service for analyzing C# classes and their relationships.
/// Delegates orchestration to specific use cases.
/// </summary>
/// <param name="analyzeFileUseCase">The use case for analyzing files.</param>
/// <param name="analyzeDirectoryUseCase">The use case for analyzing directories.</param>
public class ClassAnalysisService(
    AnalyzeFileUseCase analyzeFileUseCase,
    AnalyzeDirectoryUseCase analyzeDirectoryUseCase)
    : IClassAnalysisService
{
    /// <inheritdoc />
    public async Task<ClassModel> AnalyzeFileAsync(
        string filePath,
        AnalysisOptions? options = null)
    {
        return await analyzeFileUseCase.ExecuteAsync(filePath, options);
    }

    /// <inheritdoc />
    public async Task<ClassModel> AnalyzeDirectoryAsync(
        string directoryPath,
        AnalysisOptions? options = null)
    {
        return await analyzeDirectoryUseCase.ExecuteAsync(directoryPath, options);
    }
}
