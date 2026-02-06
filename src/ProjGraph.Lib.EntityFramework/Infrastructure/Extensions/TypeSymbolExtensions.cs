using Microsoft.CodeAnalysis;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

namespace ProjGraph.Lib.EntityFramework.Infrastructure.Extensions;

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
        switch (type.NullableAnnotation)
        {
            case NullableAnnotation.Annotated:
                return true;
            case NullableAnnotation.NotAnnotated:
                return false;
        }

        if (type.Name is EfAnalysisConstants.CommonNames.Nullable)
        {
            return true;
        }

        // Without NRT enabled, reference types are nullable
        if (type.IsReferenceType)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified <see cref="ITypeSymbol"/> represents a value type for EF purposes.
    /// </summary>
    /// <param name="type">The type symbol to check.</param>
    /// <returns>
    /// <c>true</c> if the type is a value type; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsEfValueType(this ITypeSymbol type)
    {
        if (type.IsValueType)
        {
            return true;
        }

        // Fallback for unresolved types or types where semantic info is incomplete
        var typeString = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat).TrimEnd('?');
        if (typeString.Contains('.'))
        {
            typeString = typeString[(typeString.LastIndexOf('.') + 1)..];
        }

        if (EfAnalysisConstants.DataTypes.ValueTypes.Contains(typeString))
        {
            return true;
        }

        return false;
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
        return EfAnalysisConstants.DataTypes.AllPrimitiveTypes.Contains(typeName);
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
        return EfAnalysisConstants.CollectionTypes.SupportedCollections.Contains(typeName) ||
               type.AllInterfaces.Any(i => EfAnalysisConstants.CollectionTypes.SupportedCollections.Contains(i.Name));
    }
}