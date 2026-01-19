using Microsoft.CodeAnalysis;

namespace ProjGraph.Lib.Services.EfAnalysis.Extensions;

/// <summary>
/// Provides extension methods for working with <see cref="INamespaceSymbol"/> objects.
/// </summary>
public static class NamespaceSymbolExtensions
{
    /// <summary>
    /// Retrieves all named type symbols within the specified namespace, including those in nested namespaces.
    /// </summary>
    /// <param name="ns">The namespace symbol to analyze.</param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> of <see cref="INamedTypeSymbol"/> representing all named types
    /// within the specified namespace and its nested namespaces.
    /// </returns>
    public static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(this INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            foreach (var type in childNs.GetAllNamedTypes())
            {
                yield return type;
            }
        }
    }
}