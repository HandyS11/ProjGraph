using Microsoft.CodeAnalysis;

namespace ProjGraph.Lib.ClassDiagram.Application;

/// <summary>
/// Defines a contract for processing type symbols during analysis.
/// </summary>
public interface ITypeProcessor
{
    /// <summary>
    /// Processes a queue of types to analyze.
    /// </summary>
    /// <param name="typesToAnalyze">The queue of type symbols and their depths to analyze.</param>
    /// <param name="context">The analysis context.</param>
    /// <param name="options">The analysis options.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ProcessTypeQueueAsync(
        Queue<(INamedTypeSymbol Symbol, int Depth)> typesToAnalyze,
        AnalysisContext context,
        AnalysisOptions options);
}
