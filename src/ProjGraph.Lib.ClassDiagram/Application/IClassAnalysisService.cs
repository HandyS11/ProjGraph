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
    /// <param name="includeProperties">Whether to include properties and fields.</param>
    /// <param name="includeFunctions">Whether to include functions and methods.</param>
    /// <param name="maxDepth">Depth of relationship discovery.</param>
    /// <returns>A ClassModel representing the discovered types and relationships.</returns>
    Task<ClassModel> AnalyzeFileAsync(
        string filePath,
        bool includeInheritance = false,
        bool includeDependencies = false,
        bool includeProperties = true,
        bool includeFunctions = true,
        int maxDepth = 1);
}
