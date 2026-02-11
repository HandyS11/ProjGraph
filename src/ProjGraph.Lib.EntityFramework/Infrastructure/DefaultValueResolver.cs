using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Resolves default values for EF properties, including constant/enum resolution via Roslyn.
/// </summary>
internal static class DefaultValueResolver
{
    /// <summary>
    /// Configures the DefaultValue property based on the configuration argument.
    /// </summary>
    public static void ApplyDefaultValueConfiguration(EfProperty property, string configArg, Compilation compilation)
    {
        property.DefaultValue = ParseDefaultValue(configArg, compilation);
    }

    /// <summary>
    /// Configures the DefaultValue property from SQL based on the configuration argument.
    /// </summary>
    public static void ApplyDefaultValueSqlConfiguration(EfProperty property, string configArg)
    {
        property.DefaultValue = configArg.Trim('\"', '\'', ' ');
    }

    /// <summary>
    /// Parses the default value from a configuration argument, resolving constant or enum values if possible.
    /// </summary>
    private static string ParseDefaultValue(string configArg, Compilation compilation)
    {
        var trimmedArg = configArg.Trim();
        var isQuoted = (trimmedArg.StartsWith('\"') && trimmedArg.EndsWith('\"')) ||
                       (trimmedArg.StartsWith('\'') && trimmedArg.EndsWith('\''));
        var val = trimmedArg.Trim('\"', '\'');

        if (isQuoted)
        {
            return val;
        }

        // Try to resolve as a constant/enum value using Roslyn
        var resolvedValue = ResolveConstantValue(val, compilation);
        if (resolvedValue != null)
        {
            return resolvedValue;
        }

        if (!val.Contains('.'))
        {
            return val;
        }

        var lastPart = val.Split('.')[^1];
        // Only shorten if it doesn't look like a numeric value (e.g., 0.7f or 0.7)
        if (lastPart.Length > 0 && !char.IsDigit(lastPart[0]))
        {
            val = lastPart;
        }

        return val;
    }

    /// <summary>
    /// Attempts to resolve a constant or enum value from an expression string using the provided compilation.
    /// </summary>
    private static string? ResolveConstantValue(string expression, Compilation compilation)
    {
        var cleaned = expression.Trim();
        // Remove casts like (string) or (int?)
        if (cleaned.StartsWith('(') && cleaned.Contains(')') && cleaned.LastIndexOf(')') < cleaned.Length - 1)
        {
            var afterCast = cleaned[(cleaned.LastIndexOf(')') + 1)..].Trim();
            if (!string.IsNullOrEmpty(afterCast) && !afterCast.Contains(' '))
            {
                cleaned = afterCast;
            }
        }

        if (string.IsNullOrEmpty(cleaned) || cleaned.Contains('(') || cleaned.Contains(' '))
        {
            return null;
        }

        var parts = cleaned.Split('.');

        // Case 1: Simple identifier (e.g., "MyConst")
        if (parts.Length == 1)
        {
            var name = parts[0];
            return compilation.GetSymbolsWithName(name, SymbolFilter.Member)
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue)?.ConstantValue?.ToString();
        }

        // Case 2: Qualified name (e.g., "MyClass.MyConst" or "Namespace.MyClass.MyConst")
        for (var i = parts.Length - 1; i > 0; i--)
        {
            var typeName = string.Join(".", parts[..i]);
            var memberName = parts[i];

            var typeSymbol = compilation.GetTypeByMetadataName(typeName) ?? compilation
                .GetSymbolsWithName(parts[i - 1], SymbolFilter.Type)
                .OfType<INamedTypeSymbol>()
                .FirstOrDefault();

            var member = typeSymbol?.GetMembers(memberName).FirstOrDefault();
            if (member is IFieldSymbol { HasConstantValue: true } field)
            {
                return field.ConstantValue?.ToString();
            }
        }

        return null;
    }
}
