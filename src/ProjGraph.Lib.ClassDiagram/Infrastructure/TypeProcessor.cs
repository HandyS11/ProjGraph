using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;

namespace ProjGraph.Lib.ClassDiagram.Infrastructure;

/// <summary>
/// Provides methods for processing type queues and handling related types during analysis.
/// </summary>
/// <param name="symbolResolver">The symbol resolver for resolving related type symbols.</param>
public sealed class TypeProcessor(ISymbolResolver symbolResolver) : ITypeProcessor
{
    /// <summary>
    /// Processes a queue of types to analyze, extracting type definitions and discovering relationships.
    /// This method continues until the queue is empty, respecting the maximum depth constraint.
    /// </summary>
    /// <param name="typesToAnalyze">A queue of tuples containing type symbols and their current depth.</param>
    /// <param name="context">The <see cref="AnalysisContext"/> containing the current state of the analysis.</param>
    /// <param name="options">The <see cref="AnalysisOptions"/> controlling the analysis behavior.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ProcessTypeQueueAsync(
        Queue<(INamedTypeSymbol Symbol, int Depth)> typesToAnalyze,
        AnalysisContext context,
        AnalysisOptions options)
    {
        await ProcessTypeQueueInternalAsync(typesToAnalyze, context, options);
    }

    /// <summary>
    /// Internal implementation of processing the type queue.
    /// </summary>
    /// <param name="typesToAnalyze">The queue of type symbols and their depths to analyze.</param>
    /// <param name="context">The analysis context.</param>
    /// <param name="options">The analysis options controlling the behavior.</param>
    private async Task ProcessTypeQueueInternalAsync(
        Queue<(INamedTypeSymbol Symbol, int Depth)> typesToAnalyze,
        AnalysisContext context,
        AnalysisOptions options)
    {
        while (typesToAnalyze.Count > 0)
        {
            var (symbol, depth) = typesToAnalyze.Dequeue();
            var fullName = TypeAnalyzer.GetFullyQualifiedName(symbol);

            if (!context.AnalyzedTypeFullNames.Add(fullName))
            {
                continue;
            }

            // Skip system types - they shouldn't be added as nodes in the diagram
            if (TypeFilter.IsSystemType(symbol))
            {
                continue;
            }

            var typeDef = TypeAnalyzer.AnalyzeType(symbol, options.IncludeProperties, options.IncludeFunctions);
            context.Types.Add(typeDef);

            if (depth >= options.MaxDepth)
            {
                continue;
            }

            var relatedSymbols = DiscoverRelatedTypes(symbol, options.IncludeInheritance, options.IncludeDependencies);

            await ProcessRelatedTypesAsync(
                relatedSymbols,
                fullName,
                depth,
                typesToAnalyze,
                context);
        }
    }

    /// <summary>
    /// Discovers related types for a given symbol based on the specified analysis options.
    /// This method identifies related types through inheritance and dependency relationships
    /// and returns a list of these relationships.
    /// </summary>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> representing the type to analyze for related types.</param>
    /// <param name="includeInheritance">
    /// A boolean value indicating whether to include inheritance relationships in the analysis.
    /// </param>
    /// <param name="includeDependencies">
    /// A boolean value indicating whether to include dependency relationships in the analysis.
    /// </param>
    /// <returns>
    /// A list of tuples where each tuple contains a related symbol and its corresponding relationship kind.
    /// </returns>
    private static List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>
        DiscoverRelatedTypes(
            INamedTypeSymbol symbol,
            bool includeInheritance,
            bool includeDependencies)
    {
        var relatedSymbols =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        if (includeInheritance)
        {
            RelationshipAnalyzer.AddInheritanceRelationships(symbol, relatedSymbols);
        }

        if (includeDependencies)
        {
            RelationshipAnalyzer.AddDependencyRelationships(symbol, relatedSymbols);
        }

        return relatedSymbols;
    }

    /// <summary>
    /// Processes related types by resolving their symbols and creating relationships.
    /// Also, enqueues unprocessed types for further analysis.
    /// </summary>
    /// <param name="relatedSymbols">List of related symbols with their relationship information.</param>
    /// <param name="fullName">The fully qualified name of the source type.</param>
    /// <param name="depth">The current depth in the analysis hierarchy.</param>
    /// <param name="typesToAnalyze">The queue to add newly discovered types to.</param>
    /// <param name="context">The <see cref="AnalysisContext"/> containing the current state of the analysis.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ProcessRelatedTypesAsync(
        List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)> relatedSymbols,
        string fullName,
        int depth,
        Queue<(INamedTypeSymbol Symbol, int Depth)> typesToAnalyze,
        AnalysisContext context)
    {
        // First, resolve all unique symbols to ensure consistency
        var resolvedSymbolsCache = new Dictionary<ISymbol, INamedTypeSymbol?>(SymbolEqualityComparer.Default);

        foreach (var (relatedSymbol, kind, label, cardinality) in relatedSymbols)
        {
            // Skip system types before resolution: no relationship is created for them anyway,
            // and resolving one triggers a pointless workspace scan (e.g. System.ValueType for
            // every struct, System.Enum for every enum).
            if (TypeFilter.IsSystemType(relatedSymbol))
            {
                continue;
            }

            // Resolve symbol only once per unique type
            if (!resolvedSymbolsCache.TryGetValue(relatedSymbol, out var resolvedSymbol))
            {
                resolvedSymbol = await symbolResolver.ResolveRelatedSymbolAsync(relatedSymbol, context);
                resolvedSymbolsCache[relatedSymbol] = resolvedSymbol;
            }

            // When resolution fails, the symbol is registered as an external node under its
            // OriginalDefinition (e.g. the open generic Ghost<T>), so the edge must target the
            // OriginalDefinition too — not the constructed Ghost<Order> — to hit that node.
            var symbolToUse = resolvedSymbol ?? relatedSymbol.OriginalDefinition;

            // Skip system types - don't create relationships to them
            if (TypeFilter.IsSystemType(symbolToUse))
            {
                continue;
            }

            var relatedFullName = TypeAnalyzer.GetFullyQualifiedName(symbolToUse);

            // Skip self-referencing relationships - they are not meaningful
            if (fullName == relatedFullName)
            {
                continue;
            }

            // Roslyn parks an unresolved base-list item in BaseType, so an interface base only
            // discernible after resolution is initially classified as Inheritance. Once the
            // resolved symbol is known to be an interface, reclassify it as Realization.
            var effectiveKind = kind == RelationshipKind.Inheritance
                                && symbolToUse.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface
                ? RelationshipKind.Realization
                : kind;

            context.Relationships.Add(new Relationship(fullName, relatedFullName, effectiveKind, label, cardinality));

            // Only enqueue if we haven't analyzed this type yet
            if (context.AnalyzedTypeFullNames.Contains(relatedFullName))
            {
                continue;
            }

            if (resolvedSymbol is null)
            {
                continue;
            }

            // Only enqueue once per unique type (check if already in queue would be complex, 
            // but the AnalyzedTypeFullNames check in ProcessTypeQueueAsync handles duplicates)
            typesToAnalyze.Enqueue((resolvedSymbol, depth + 1));
        }
    }
}
