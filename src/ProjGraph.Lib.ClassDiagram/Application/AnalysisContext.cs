using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProjGraph.Core.Models;
using System.Collections.ObjectModel;

namespace ProjGraph.Lib.ClassDiagram.Application;

/// <summary>
/// Represents the context for the analysis process, containing information about analyzed types,
/// their relationships, and the compilation context.
/// </summary>
public sealed class AnalysisContext
{
    /// <summary>
    /// A set of fully qualified names of types that have already been analyzed.
    /// </summary>
    public required HashSet<string> AnalyzedTypeFullNames { get; init; }

    /// <summary>
    /// A list of type definitions discovered during the analysis.
    /// </summary>
    public required Collection<TypeDefinition> Types { get; init; }

    /// <summary>
    /// A list of relationships between the analyzed types.
    /// </summary>
    public required Collection<Relationship> Relationships { get; init; }

    /// <summary>
    /// The Roslyn CSharpCompilation object used for semantic analysis.
    /// </summary>
    public required CSharpCompilation Compilation { get; set; }

    /// <summary>
    /// Adds syntax trees to the compilation, returning the updated compilation.
    /// This is the only way to mutate the compilation after construction.
    /// </summary>
    /// <param name="trees">The syntax trees to add to the compilation.</param>
    /// <returns>The updated <see cref="CSharpCompilation"/>.</returns>
    public CSharpCompilation AddSyntaxTrees(params SyntaxTree[] trees)
    {
        Compilation = Compilation.AddSyntaxTrees(trees);
        return Compilation;
    }

    /// <summary>
    /// The starting directory for the analysis process.
    /// </summary>
    public required string StartDirectory { get; init; }

    /// <summary>
    /// Caches workspace type-definition lookups by type name for the duration of this analysis,
    /// so repeated references to the same unresolved type trigger a single workspace search.
    /// A null value records a completed search that found no file.
    /// </summary>
    public Dictionary<string, string?> TypeFileLookupCache { get; } = [];
}
