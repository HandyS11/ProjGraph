using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;

namespace ProjGraph.Lib.ClassDiagram.Application;

/// <summary>
/// Service for analyzing C# classes and their relationships.
/// Delegates orchestration to specific use cases.
/// </summary>
/// <param name="analyzeFileUseCase">The use case for analyzing files.</param>
public class ClassAnalysisService(AnalyzeFileUseCase analyzeFileUseCase) : IClassAnalysisService
{
    /// <inheritdoc />
    public async Task<ClassModel> AnalyzeFileAsync(
        string filePath,
        bool includeInheritance = false,
        bool includeDependencies = false,
        bool includeProperties = true,
        bool includeFunctions = true,
        int maxDepth = 1)
    {
        return await analyzeFileUseCase.ExecuteAsync(
            filePath,
            includeInheritance,
            includeDependencies,
            includeProperties,
            includeFunctions,
            maxDepth);
    }
}
