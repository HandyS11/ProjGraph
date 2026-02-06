using Microsoft.CodeAnalysis;
using ProjGraph.Lib.Application.Services;

namespace ProjGraph.Lib.Application.Interfaces;

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