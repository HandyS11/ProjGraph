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
    public Task<ClassModel> AnalyzeFileAsync(
        string filePath,
        AnalysisOptions? options = null)
    {
        return analyzeFileUseCase.ExecuteAsync(filePath, options);
    }

    /// <inheritdoc />
    public Task<ClassModel> AnalyzeDirectoryAsync(
        string directoryPath,
        AnalysisOptions? options = null)
    {
        return analyzeDirectoryUseCase.ExecuteAsync(directoryPath, options);
    }
}
