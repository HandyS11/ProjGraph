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
    Task ProcessTypeQueueAsync(
        Queue<(INamedTypeSymbol Symbol, int Depth)> typesToAnalyze,
        AnalysisContext context,
        AnalysisOptions options);
}