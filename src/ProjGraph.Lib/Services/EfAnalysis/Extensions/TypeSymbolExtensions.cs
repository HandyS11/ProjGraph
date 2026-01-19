using Microsoft.CodeAnalysis;

namespace ProjGraph.Lib.Services.EfAnalysis.Extensions;

/// <summary>
/// Provides extension methods for working with <see cref="ITypeSymbol"/> and <see cref="INamedTypeSymbol"/> objects.
/// </summary>
public static class TypeSymbolExtensions
{
    /// <summary>
    /// Determines whether the specified <see cref="ITypeSymbol"/> is nullable.
    /// </summary>
    /// <param name="type">The type symbol to check.</param>
    /// <returns>
    /// <c>true</c> if the type is nullable; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsNullable(this ITypeSymbol type)
    {
        return type.NullableAnnotation == NullableAnnotation.Annotated ||
               type.Name == "Nullable";
    }

    /// <summary>
    /// Determines whether the specified <see cref="INamedTypeSymbol"/> represents a system or primitive type.
    /// </summary>
    /// <param name="type">The named type symbol to check.</param>
    /// <returns>
    /// <c>true</c> if the type is a system or primitive type; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsSystemOrPrimitiveType(this INamedTypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None)
        {
            return true;
        }

        var ns = type.ContainingNamespace;
        while (ns is { IsGlobalNamespace: false })
        {
            if (ns.Name == "System")
            {
                return true;
            }

            ns = ns.ContainingNamespace;
        }

        var typeName = type.Name;
        return typeName is "String" or "Guid" or "DateTime" or "DateTimeOffset" or "TimeSpan" or "Decimal";
    }

    /// <summary>
    /// Determines whether the specified <see cref="INamedTypeSymbol"/> represents a collection type.
    /// </summary>
    /// <param name="type">The named type symbol to check.</param>
    /// <returns>
    /// <c>true</c> if the type is a collection type; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsCollectionType(this INamedTypeSymbol type)
    {
        var typeName = type.Name;
        return typeName is "ICollection" or "IList" or "List" or "HashSet" or "ISet" ||
               type.AllInterfaces.Any(i => i.Name is "ICollection" or "IEnumerable");
    }
}