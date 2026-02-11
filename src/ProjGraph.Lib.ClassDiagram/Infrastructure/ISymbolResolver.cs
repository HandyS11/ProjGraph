using Microsoft.CodeAnalysis;
using ProjGraph.Lib.ClassDiagram.Application;

namespace ProjGraph.Lib.ClassDiagram.Infrastructure;

/// <summary>
/// Provides methods for resolving type symbols and loading their definitions from source files.
/// </summary>
public interface ISymbolResolver
{
    /// <summary>
    /// Resolves a related symbol by determining if it's already in the compilation or needs to be loaded from a file.
    /// If the symbol is external (not found in source), it's added as an external type.
    /// </summary>
    /// <param name="relatedSymbol">The <see cref="INamedTypeSymbol"/> to resolve.</param>
    /// <param name="context">The <see cref="AnalysisContext"/> containing the current state of the analysis.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the resolved symbol,
    /// or null if it's an external type that was added to the context.
    /// </returns>
    Task<INamedTypeSymbol?> ResolveRelatedSymbolAsync(INamedTypeSymbol relatedSymbol, AnalysisContext context);
}
