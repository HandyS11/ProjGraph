using ProjGraph.Core.Models;

namespace ProjGraph.Lib.ClassDiagram.Application;

/// <summary>
/// Service for analyzing C# source files to build a class diagram model.
/// </summary>
public interface IClassAnalysisService
{
    /// <summary>
    /// Analyzes a specific C# file and optionally discovers its relationships in the workspace.
    /// </summary>
    /// <param name="filePath">Target .cs file path.</param>
    /// <param name="includeInheritance">Whether to discover base classes/interfaces.</param>
    /// <param name="includeDependencies">Whether to discover types used in members.</param>
    /// <param name="maxDepth">Depth of relationship discovery.</param>
    /// <returns>A ClassModel representing the discovered types and relationships.</returns>
    Task<ClassModel> AnalyzeFileAsync(
        string filePath,
        bool includeInheritance = true,
        bool includeDependencies = false,
        int maxDepth = 1);
}
