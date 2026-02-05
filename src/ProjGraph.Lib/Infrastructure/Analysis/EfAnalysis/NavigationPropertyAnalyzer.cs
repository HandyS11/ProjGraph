using Microsoft.CodeAnalysis;
using ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis.Extensions;

namespace ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis;

/// <summary>
/// Provides methods for analyzing navigation properties in entity types.
/// </summary>
/// <remarks>
/// The <see cref="NavigationPropertyAnalyzer"/> class contains static methods to analyze and determine
/// the characteristics of navigation properties in entity types, such as whether they are collections,
/// have inverse references, or are valid entity candidates.
/// </remarks>
public static class NavigationPropertyAnalyzer
{
    /// <summary>
    /// Determines whether the specified property is a navigation property and retrieves its target type and collection status.
    /// </summary>
    /// <param name="prop">The <see cref="IPropertySymbol"/> representing the property to evaluate.</param>
    /// <param name="targetType">
    /// When this method returns, contains the target type of the navigation property if it is valid; otherwise, <c>null</c>.
    /// </param>
    /// <param name="isCollection">
    /// When this method returns, indicates whether the navigation property is a collection.
    /// </param>
    /// <returns>
    /// <c>true</c> if the property is a navigation property; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method determines if the given property is a navigation property by checking its type.
    /// If the property is a generic collection type, it extracts the element type and sets the <paramref name="isCollection"/> flag to <c>true</c>.
    /// If the property is not a collection, it checks if the type is a valid entity candidate.
    /// </remarks>
    public static bool IsNavigationProperty(
        IPropertySymbol prop,
        out INamedTypeSymbol? targetType,
        out bool isCollection)
    {
        targetType = null;
        isCollection = false;

        var type = UnwrapNullableType(prop.Type);
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        // Check for generic collection types
        if (TryGetCollectionElementType(namedType, out var elementType))
        {
            targetType = elementType;
            isCollection = true;
            return true;
        }

        // Reference detection (non-collection)
        targetType = namedType;
        return IsEntityCandidate(targetType);
    }

    /// <summary>
    /// Determines whether the specified property has an inverse collection navigation property in the target type.
    /// </summary>
    /// <param name="prop">The <see cref="IPropertySymbol"/> representing the property to check.</param>
    /// <param name="targetType">The <see cref="INamedTypeSymbol"/> representing the target type to search for an inverse collection.</param>
    /// <returns>
    /// <c>true</c> if the target type contains a collection navigation property that references the source type; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method checks the members of the target type to find a property that is a collection navigation property
    /// and references the source type of the provided property.
    /// </remarks>
    public static bool HasInverseCollection(IPropertySymbol prop, INamedTypeSymbol targetType)
    {
        var sourceTypeName = prop.ContainingType.Name;
        return targetType.GetMembers()
            .OfType<IPropertySymbol>()
            .Any(p => !SymbolEqualityComparer.Default.Equals(p, prop) &&
                      IsNavigationProperty(p, out var t, out var isColl) &&
                      isColl &&
                      t?.Name == sourceTypeName);
    }

    /// <summary>
    /// Determines whether the specified property has an inverse reference navigation property in the target type.
    /// </summary>
    /// <param name="prop">The <see cref="IPropertySymbol"/> representing the property to check.</param>
    /// <param name="targetType">The <see cref="INamedTypeSymbol"/> representing the target type to search for an inverse reference.</param>
    /// <returns>
    /// <c>true</c> if the target type contains a non-collection navigation property that references the source type; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method checks the members of the target type to find a property that is a non-collection navigation property
    /// and references the source type of the provided property.
    /// </remarks>
    public static bool HasInverseReference(IPropertySymbol prop, INamedTypeSymbol targetType)
    {
        var sourceTypeName = prop.ContainingType.Name;
        return targetType.GetMembers()
            .OfType<IPropertySymbol>()
            .Any(p => !SymbolEqualityComparer.Default.Equals(p, prop) &&
                      IsNavigationProperty(p, out var t, out var isColl) &&
                      !isColl &&
                      t?.Name == sourceTypeName);
    }

    /// <summary>
    /// Determines whether the specified type is a valid entity candidate.
    /// </summary>
    /// <param name="type">The <see cref="INamedTypeSymbol"/> representing the type to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the specified type is not a system or primitive type; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method checks if the provided type is not a system-defined or primitive type,
    /// which helps in identifying whether the type can be considered as a potential entity.
    /// </remarks>
    private static bool IsEntityCandidate(INamedTypeSymbol type)
    {
        return !type.IsSystemOrPrimitiveType();
    }

    /// <summary>
    /// Unwraps a nullable type to retrieve its underlying type if it is nullable.
    /// </summary>
    /// <param name="type">The <see cref="ITypeSymbol"/> representing the type to check.</param>
    /// <returns>
    /// The underlying type if the provided type is a nullable type; otherwise, returns the original type.
    /// </returns>
    /// <remarks>
    /// This method checks if the provided type is a nullable type (e.g., Nullable&lt;T&gt;).
    /// If it is, the method returns the underlying type (T). Otherwise, it returns the original type.
    /// </remarks>
    private static ITypeSymbol UnwrapNullableType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { Name: "Nullable", TypeArguments.Length: 1 } nullableType)
        {
            return nullableType.TypeArguments[0];
        }

        return type;
    }

    /// <summary>
    /// Attempts to determine if the specified type is a generic collection type and retrieves its element type if it is.
    /// </summary>
    /// <param name="namedType">The <see cref="INamedTypeSymbol"/> representing the type to check.</param>
    /// <param name="elementType">
    /// When this method returns, contains the element type of the collection if <paramref name="namedType"/> is a collection type;
    /// otherwise, <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if <paramref name="namedType"/> is a generic collection type with a single type argument; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method checks if the provided type is a generic collection type with exactly one type argument.
    /// If the type is a collection, the method extracts and returns its element type.
    /// </remarks>
    private static bool TryGetCollectionElementType(
        INamedTypeSymbol namedType,
        out INamedTypeSymbol? elementType)
    {
        elementType = null;

        if (namedType.TypeArguments.Length != 1)
        {
            return false;
        }

        if (!namedType.IsCollectionType())
        {
            return false;
        }

        // ReSharper disable once InvertIf
        if (namedType.TypeArguments[0] is INamedTypeSymbol targetTypeSymbol)
        {
            elementType = targetTypeSymbol;
            return true;
        }

        return false;
    }
}

