using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;
using TypeKind = Microsoft.CodeAnalysis.TypeKind;

namespace ProjGraph.Lib.ClassDiagram.Infrastructure;

/// <summary>
/// Provides methods for analyzing relationships between types (inheritance, realization, associations, dependencies).
/// </summary>
internal static class RelationshipAnalyzer
{
    /// <summary>
    /// Adds inheritance relationships for a given symbol to the list of related symbols.
    /// This method identifies base classes (excluding System.Object) and implemented interfaces.
    /// </summary>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> representing the type to analyze for inheritance.</param>
    /// <param name="relatedSymbols">
    /// A list of tuples where each tuple contains a related symbol and its corresponding relationship kind.
    /// </param>
    public static void AddInheritanceRelationships(
        INamedTypeSymbol symbol,
        List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)> relatedSymbols)
    {
        if (symbol.BaseType != null && symbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            relatedSymbols.Add((symbol.BaseType, RelationshipKind.Inheritance, null, null));
        }

        relatedSymbols.AddRange(symbol.Interfaces.Select(iface =>
            (iface, RelationshipKind.Realization, (string?)null, (string?)null)));
    }

    /// <summary>
    /// Adds dependency relationships for a given symbol to the list of related symbols.
    /// This method identifies dependencies based on the properties, fields, method return types,
    /// and method parameter types of the provided symbol.
    /// </summary>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> representing the type to analyze for dependencies.</param>
    /// <param name="relatedSymbols">
    /// A list of tuples where each tuple contains a related symbol and its corresponding relationship kind.
    /// </param>
    public static void AddDependencyRelationships(
        INamedTypeSymbol symbol,
        List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)> relatedSymbols)
    {
        // Skip dependency analysis for enums - they don't have meaningful relationships
        if (symbol.TypeKind == TypeKind.Enum)
        {
            return;
        }

        // Track unique type+label combinations to avoid exact duplicates. Keys are
        // fully-qualified names so same-named types in different namespaces stay distinct.
        var seenCombinations = new HashSet<(string TypeName, string? Label)>();

        // Also track types separately for method dependencies (which don't have labels),
        // again keyed by fully-qualified name.
        var seenMethodTypes = new HashSet<string>();

        // Process properties and fields for association relationships (has-a relationships)
        var propertySymbols = symbol.GetMembers().OfType<IPropertySymbol>().ToList();
        var fieldSymbols = symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsImplicitlyDeclared) // Filter out compiler-generated backing fields
            .ToList();

        foreach (var prop in propertySymbols)
        {
            ProcessMemberType(prop.Type, prop.Name, relatedSymbols, seenCombinations);
        }

        foreach (var field in fieldSymbols)
        {
            ProcessMemberType(field.Type, field.Name, relatedSymbols, seenCombinations);
        }

        // Process method return types and parameters as dependencies
        var methodReturnTypes = symbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .Select(m => m.ReturnType);

        var methodParamTypes = symbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .SelectMany(m => m.Parameters.Select(p => p.Type));

        foreach (var type in methodReturnTypes.Concat(methodParamTypes))
        {
            if (UnwrapArrayElementType(type) is not INamedTypeSymbol { SpecialType: SpecialType.None } namedType)
            {
                continue;
            }

            var extractedTypes = ExtractTypesFromGeneric(namedType);
            relatedSymbols.AddRange(
                extractedTypes
                    .Where(extracted => seenMethodTypes.Add(TypeAnalyzer.GetFullyQualifiedName(extracted)) &&
                                        !TypeFilter.IsSystemType(extracted))
                    .Select(extracted =>
                        ((INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality))(
                            extracted, RelationshipKind.Dependency, null, null)));
        }
    }

    /// <summary>
    /// Processes a member type (property or field) to determine the appropriate relationship kind.
    /// </summary>
    /// <param name="type">The type of the member.</param>
    /// <param name="memberName">The name of the property or field.</param>
    /// <param name="relatedSymbols">List to add discovered relationships to.</param>
    /// <param name="seenCombinations">Set to track already processed type+label combinations.</param>
    private static void ProcessMemberType(
        ITypeSymbol type,
        string memberName,
        List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)> relatedSymbols,
        HashSet<(string TypeName, string? Label)> seenCombinations)
    {
        string cardinality;
        List<INamedTypeSymbol> extractedTypes;
        INamedTypeSymbol? singleInstanceType = null;

        // An array (T[], including jagged arrays) is a collection, exactly like List<T>:
        // unwrap to the innermost element type and treat it as a '*' association.
        if (type is IArrayTypeSymbol)
        {
            if (UnwrapArrayElementType(type) is not INamedTypeSymbol { SpecialType: SpecialType.None } arrayElement)
            {
                return;
            }

            cardinality = "*";
            extractedTypes = ExtractTypesFromGeneric(arrayElement);
        }
        else if (type is INamedTypeSymbol { SpecialType: SpecialType.None } namedType)
        {
            cardinality = IsCollectionType(namedType) ? "*" : "1";
            extractedTypes = ExtractTypesFromGeneric(namedType);

            // The member holds exactly one instance of its own (outer) type; '*' describes a
            // collection's element count, so it applies to the extracted element types only.
            singleInstanceType = namedType.OriginalDefinition;
        }
        else
        {
            return;
        }

        relatedSymbols.AddRange(
            extractedTypes
                .Where(extracted =>
                    seenCombinations.Add((TypeAnalyzer.GetFullyQualifiedName(extracted), memberName)) &&
                    !TypeFilter.IsSystemType(extracted))
                .Select(extracted =>
                    ((INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality))(
                        extracted,
                        RelationshipKind.Association,
                        memberName,
                        SymbolEqualityComparer.Default.Equals(extracted, singleInstanceType) ? "1" : cardinality)));
    }

    /// <summary>
    /// Determines whether a member type represents a collection (rendered with '*' cardinality).
    /// A resolved type is a collection iff it is, or implements, <see cref="System.Collections.IEnumerable"/>.
    /// Unresolved (error) symbols carry no interface information, so the legacy name heuristic
    /// is kept as a best-effort fallback for them.
    /// </summary>
    /// <param name="type">The member type to classify.</param>
    /// <returns>True when the type should be rendered with '*' cardinality.</returns>
    private static bool IsCollectionType(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Error)
        {
            return type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                   || type.AllInterfaces.Any(i => i.SpecialType == SpecialType.System_Collections_IEnumerable);
        }

        return type.IsGenericType &&
               (type.Name.Contains("List", StringComparison.Ordinal) ||
                type.Name.Contains("Collection", StringComparison.Ordinal) ||
                type.Name.Contains("IEnumerable", StringComparison.Ordinal) ||
                type.Name.Contains("Array", StringComparison.Ordinal) ||
                type.Name.Contains("Set", StringComparison.Ordinal));
    }

    /// <summary>
    /// Unwraps an array type (including jagged arrays) to its innermost element type.
    /// Non-array types are returned unchanged.
    /// </summary>
    /// <param name="type">The type to unwrap.</param>
    /// <returns>The innermost element type, or the input type when it is not an array.</returns>
    private static ITypeSymbol UnwrapArrayElementType(ITypeSymbol type)
    {
        while (type is IArrayTypeSymbol arrayType)
        {
            type = arrayType.ElementType;
        }

        return type;
    }

    /// <summary>
    /// Extracts concrete types from a potentially generic type.
    /// For example, List&lt;Address&gt; would return [Address], and Dictionary&lt;string, User&gt; would return [User].
    /// This avoids creating nodes for BCL container types; a user-defined generic container is
    /// kept alongside its arguments (Result&lt;Order&gt; returns [Result&lt;T&gt;, Order]).
    /// </summary>
    /// <param name="type">The type to extract from.</param>
    /// <returns>A list of concrete named type symbols.</returns>
    private static List<INamedTypeSymbol> ExtractTypesFromGeneric(INamedTypeSymbol type)
    {
        var result = new List<INamedTypeSymbol>();

        // If it's a generic type (like List<T>, Dictionary<K,V>), extract the type arguments
        if (type is { IsGenericType: true, TypeArguments.Length: > 0 })
        {
            // A user-defined generic container is itself a participant in the relationship:
            // Result<Order> keeps an edge to Result<T>, not only to Order. BCL containers
            // stay reduced to their arguments — we don't want List<T> nodes in the diagram.
            if (!TypeFilter.IsSystemType(type))
            {
                result.Add(type.OriginalDefinition);
            }

            foreach (var typeArg in type.TypeArguments)
            {
                if (typeArg is not INamedTypeSymbol { SpecialType: SpecialType.None } namedTypeArg)
                {
                    continue;
                }

                // Recursively handle nested generics
                if (namedTypeArg.IsGenericType)
                {
                    result.AddRange(ExtractTypesFromGeneric(namedTypeArg));
                }
                else if (!TypeFilter.IsSystemType(namedTypeArg))
                {
                    // Use OriginalDefinition to strip off nullability markers for reference types
                    result.Add(namedTypeArg.OriginalDefinition);
                }
            }
        }
        else if (!TypeFilter.IsSystemType(type))
        {
            // Not a generic type, return the type itself if it's not a system type
            // Use OriginalDefinition to strip off nullability markers for reference types
            result.Add(type.OriginalDefinition);
        }

        return result;
    }
}
