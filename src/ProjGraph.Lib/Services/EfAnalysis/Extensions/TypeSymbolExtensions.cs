using Microsoft.CodeAnalysis;
using ProjGraph.Lib.Services.EfAnalysis.Constants;

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
        return type.NullableAnnotation is NullableAnnotation.Annotated ||
               type.Name is EfAnalysisConstants.CommonNames.Nullable;
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
        if (type.SpecialType is not SpecialType.None)
        {
            return true;
        }

        var ns = type.ContainingNamespace;
        while (ns is { IsGlobalNamespace: false })
        {
            if (ns.Name is EfAnalysisConstants.CommonNames.System)
            {
                return true;
            }

            ns = ns.ContainingNamespace;
        }

        var typeName = type.Name;
        return typeName is EfAnalysisConstants.DataTypes.String or EfAnalysisConstants.DataTypes.Guid
            or EfAnalysisConstants.DataTypes.DateTime or EfAnalysisConstants.DataTypes.DateTimeOffset
            or EfAnalysisConstants.DataTypes.TimeSpan or EfAnalysisConstants.DataTypes.Decimal;
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
        return typeName is EfAnalysisConstants.CollectionTypes.ICollection or EfAnalysisConstants.CollectionTypes.IList
                   or EfAnalysisConstants.CollectionTypes.List or EfAnalysisConstants.CollectionTypes.HashSet
                   or EfAnalysisConstants.CollectionTypes.ISet ||
               type.AllInterfaces.Any(i =>
                   i.Name is EfAnalysisConstants.CollectionTypes.ICollection
                       or EfAnalysisConstants.CollectionTypes.IEnumerable);
    }
}