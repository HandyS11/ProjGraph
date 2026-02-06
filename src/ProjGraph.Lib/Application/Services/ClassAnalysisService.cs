using ProjGraph.Core.Models;
using ProjGraph.Lib.Application.Interfaces;
using ProjGraph.Lib.Application.UseCases.ClassAnalysis;

namespace ProjGraph.Lib.Application.Services;

/// <summary>
/// Service for analyzing C# classes and their relationships.
/// Delegates orchestration to specific use cases.
/// </summary>
public class ClassAnalysisService(AnalyzeFileUseCase analyzeFileUseCase) : IClassAnalysisService
{
    /// <inheritdoc />
    public async Task<ClassModel> AnalyzeFileAsync(
        string filePath,
        bool includeInheritance = true,
        bool includeDependencies = false,
        int maxDepth = 1)
    {
        return await analyzeFileUseCase.ExecuteAsync(filePath, includeInheritance, includeDependencies, maxDepth);
    }
}
