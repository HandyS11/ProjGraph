using Microsoft.CodeAnalysis.CSharp;
using ProjGraph.Core.Models;

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
    public required List<TypeDefinition> Types { get; init; }

    /// <summary>
    /// A list of relationships between the analyzed types.
    /// </summary>
    public required List<Relationship> Relationships { get; init; }

    /// <summary>
    /// The Roslyn CSharpCompilation object used for semantic analysis.
    /// </summary>
    public required CSharpCompilation Compilation { get; set; }

    /// <summary>
    /// The starting directory for the analysis process.
    /// </summary>
    public required string StartDirectory { get; init; }
}





