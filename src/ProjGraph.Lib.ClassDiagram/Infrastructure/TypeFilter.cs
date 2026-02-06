using Microsoft.CodeAnalysis;

namespace ProjGraph.Lib.ClassDiagram.Infrastructure;

/// <summary>
/// Provides methods for filtering out system and well-known types during analysis.
/// </summary>
internal static class TypeFilter
{
    /// <summary>
    /// Determines whether a given type is a system type.
    /// A type is considered a system type if it belongs to well-known system namespaces
    /// such as System, Microsoft.Extensions, or if it is a well-known system type
    /// (e.g., collections, strings, value types).
    /// </summary>
    /// <param name="type">The <see cref="INamedTypeSymbol"/> representing the type to check.</param>
    /// <returns>
    /// <c>true</c> if the type is a system type; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method performs the following checks:
    /// 1. Checks if the type's containing namespace starts with well-known system namespace prefixes.
    /// 2. Validates whether the type is a well-known system type using <see cref="IsWellKnownSystemType"/>.
    /// 3. Checks if the type has a special type designation (e.g., int, string) provided by the compiler.
    /// </remarks>
    public static bool IsSystemType(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString();

        // Check if it's in a system namespace
        if (ns != null && (ns.StartsWith("System") || ns.StartsWith("Microsoft.Extensions")))
        {
            return true;
        }

        // Check if it's a well-known system type
        if (IsWellKnownSystemType(type.Name))
        {
            return true;
        }

        // Check if it's a special type (int, string, etc.)
        return type.SpecialType != SpecialType.None;
    }

    /// <summary>
    /// Determines whether a given type name corresponds to a well-known system type.
    /// Well-known system types include commonly used generic collection types and other built-in utility types.
    /// </summary>
    /// <param name="typeName">The name of the type to check.</param>
    /// <returns>
    /// <c>true</c> if the type name corresponds to a well-known system type; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method checks if the given type name is present in a predefined set of well-known system type names,
    /// which includes common generic collection types such as List, Dictionary, IEnumerable, etc.
    /// </remarks>
    private static bool IsWellKnownSystemType(string typeName)
    {
        var wellKnownTypes = new HashSet<string>
        {
            // Generic collections
            "List",
            "Dictionary",
            "IEnumerable",
            "ICollection",
            "IList",
            "IDictionary",
            "HashSet",
            "Queue",
            "Stack",
            "LinkedList",
            "SortedSet",
            "SortedList",
            "SortedDictionary",
            // Async types
            "Task",
            "ValueTask",
            // Nullable and lazy
            "Nullable",
            "Lazy",
            // Common system value types
            "DateTime",
            "DateTimeOffset",
            "TimeSpan",
            "Guid",
            "Uri",
            "Decimal",
            "Byte",
            "SByte",
            "Int16",
            "UInt16",
            "Int32",
            "UInt32",
            "Int64",
            "UInt64",
            "Single",
            "Double",
            "Char",
            "Boolean",
            "Object",
            "String",
            "Array",
            "Delegate",
            "MulticastDelegate",
            "Enum",
            "ValueType",
            "Exception"
        };

        return wellKnownTypes.Contains(typeName);
    }
}