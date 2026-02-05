using Microsoft.CodeAnalysis;

namespace ProjGraph.Lib.Application.Interfaces;

/// <summary>
/// Defines a contract for creating Roslyn compilations.
/// </summary>
public interface ICompilationFactory
{
    /// <summary>
    /// Creates a compilation for the specified syntax trees.
    /// </summary>
    /// <param name="syntaxTrees">The syntax trees to compile.</param>
    /// <returns>A Roslyn compilation object.</returns>
    Compilation CreateCompilation(IEnumerable<SyntaxTree> syntaxTrees);
}