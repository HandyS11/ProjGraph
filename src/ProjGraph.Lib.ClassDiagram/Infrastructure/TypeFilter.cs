using Microsoft.CodeAnalysis;
using System.Collections.Frozen;

namespace ProjGraph.Lib.ClassDiagram.Infrastructure;

/// <summary>
/// Provides methods for filtering out system and well-known types during analysis.
/// </summary>
internal static class TypeFilter
{
    /// <summary>
    /// A pre-computed, immutable set of well-known system type names used for fast lookup.
    /// </summary>
    private static readonly FrozenSet<string> WellKnownTypes = new HashSet<string>
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
    }.ToFrozenSet();

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
        var containingNamespace = type.ContainingNamespace;
        var ns = containingNamespace?.ToDisplayString();

        // Check if it's in a system namespace (dot-bounded so 'Systems.Combat' is not treated
        // as being under 'System').
        if (ns != null && (IsInNamespace(ns, "System") || IsInNamespace(ns, "Microsoft.Extensions")))
        {
            return true;
        }

        // Special types (int, string, etc.) are always system types.
        if (type.SpecialType != SpecialType.None)
        {
            return true;
        }

        // Fall back to the well-known-name list ONLY for types we could not attribute to a real
        // namespace — unresolved/error symbols or types in the global namespace. A user-defined
        // type living in a real namespace (e.g. MyApp.Task) must NOT be filtered just because its
        // simple name collides with a BCL type.
        var isUnattributed = type.TypeKind == TypeKind.Error
                             || (containingNamespace?.IsGlobalNamespace ?? true);

        return isUnattributed && IsWellKnownSystemType(type.Name);
    }

    /// <summary>
    /// Determines whether a namespace display string is, or is nested under, the given root
    /// namespace, using a dot boundary to avoid false prefix matches (e.g. 'Systems' vs 'System').
    /// </summary>
    /// <param name="ns">The namespace display string to test.</param>
    /// <param name="root">The root namespace to match against.</param>
    /// <returns><c>true</c> if <paramref name="ns"/> equals or is nested under <paramref name="root"/>.</returns>
    private static bool IsInNamespace(string ns, string root)
    {
        return ns.Equals(root, StringComparison.Ordinal) ||
               ns.StartsWith(root + ".", StringComparison.Ordinal);
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
        return WellKnownTypes.Contains(typeName);
    }
}
