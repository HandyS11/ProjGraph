using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Shared utility methods used by relationship and property configuration parsers.
/// </summary>
internal static class FluentApiParsingUtilities
{
    /// <summary>
    /// Extracts the generic type from a method name like "Property&lt;T&gt;".
    /// </summary>
    /// <param name="methodName">The method name to parse.</param>
    /// <returns>The extracted type, or empty string if no generic type is found.</returns>
    public static string ExtractGenericType(string methodName)
    {
        if (methodName.Contains('<', StringComparison.Ordinal) && methodName.Contains('>', StringComparison.Ordinal))
        {
            return methodName.Split('<')[1].Split('>')[0];
        }

        return "";
    }

    /// <summary>
    /// Extracts property names from method arguments, handling both lambdas and string literals.
    /// </summary>
    /// <param name="args">The method arguments string to parse.</param>
    public static List<string> ExtractPropertyNamesFromArgs(string args)
    {
        var result = new List<string>();

        // Handle lambda: e => new { e.P1, e.P2 } or e => e.P1
        if (args.Contains("=>", StringComparison.Ordinal))
        {
            var matches = EfAnalysisRegexPatterns.MethodChainRegex().Matches(args);
            result.AddRange(matches.Select(match => match.Groups[1].Value));
        }
        else
        {
            // Handle string list: "P1", "P2"
            var matches = EfAnalysisRegexPatterns.StringLiteralRegex().Matches(args);
            result.AddRange(matches.Select(match => match.Groups[1].Value));

            if (result.Count != 0 || string.IsNullOrWhiteSpace(args))
            {
                return result;
            }

            // Fallback for single unquoted arg
            var identifier = args.Trim('"', ' ');
            if (!string.IsNullOrEmpty(identifier))
            {
                result.Add(identifier);
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts the target entity name from method arguments.
    /// </summary>
    /// <param name="args">The method arguments string to extract from.</param>
    public static string? ExtractTargetName(string args)
    {
        // First try string literals (common in ModelSnapshots)
        var stringMatches = EfAnalysisRegexPatterns.StringLiteralRegex().Matches(args);
        if (stringMatches.Count > 0)
        {
            var name = stringMatches[0].Groups[1].Value;
            if (name.Contains('.', StringComparison.Ordinal))
            {
                name = name.Split('.')[^1];
            }

            return name;
        }

        // Try lambda expression: e => e.NavigationProperty (common in DbContext fluent API)
        if (!args.Contains("=>", StringComparison.Ordinal))
        {
            return null;
        }

        var lambdaMatch = EfAnalysisRegexPatterns.PropertyLambdaRegex().Match(args);
        if (!lambdaMatch.Success)
        {
            return null;
        }

        return lambdaMatch.Groups[2].Value;
    }

    /// <summary>
    /// Gets an existing property or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="entity">The entity containing the property.</param>
    /// <param name="propName">The property name.</param>
    /// <param name="type">The property type.</param>
    /// <returns>The EfProperty object.</returns>
    public static EfProperty GetOrCreateProperty(EfEntity entity, string propName, string type)
    {
        var property = entity.Properties.FirstOrDefault(p => p.Name == propName);
        if (property is null)
        {
            var detectedType = type;
            if (string.IsNullOrEmpty(detectedType))
            {
                detectedType =
                    propName.EndsWith(EfAnalysisConstants.Suffixes.IdSuffix, StringComparison.OrdinalIgnoreCase)
                        ? EfAnalysisConstants.DataTypes.Guid
                        : EfAnalysisConstants.DataTypes.StringTypeName;
            }

            property = new EfProperty
            {
                Name = propName,
                Type = detectedType,
                IsValueType = IsValueTypeString(detectedType)
            };
            entity.Properties.Add(property);
        }
        else if (!string.IsNullOrEmpty(type))
        {
            var updated = EfPropertyFactory.CopyWith(property,
                type,
                isValueType: IsValueTypeString(type));
            var index = entity.Properties.IndexOf(property);
            if (index >= 0)
            {
                entity.Properties[index] = updated;
            }

            property = updated;
        }

        return property;
    }

    /// <summary>
    /// Determines whether a type name represents a value type.
    /// </summary>
    /// <param name="type">The type name to check.</param>
    public static bool IsValueTypeString(string type)
    {
        var typeName = type.TrimEnd('?');
        if (typeName.Contains('.', StringComparison.Ordinal))
        {
            typeName = typeName[(typeName.LastIndexOf('.') + 1)..];
        }

        return EfAnalysisConstants.DataTypes.ValueTypes.Contains(typeName);
    }

    /// <summary>
    /// Determines whether a match is inside a "UsingEntity" block within the given configuration section.
    /// </summary>
    /// <param name="configSection">The configuration section to search within.</param>
    /// <param name="matchIndex">The index of the match in the configuration section.</param>
    /// <returns>
    /// <c>true</c> if the match is inside a "UsingEntity" block; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsInsideUsingEntityBlock(string configSection, int matchIndex)
    {
        var textBeforeMatch = configSection[..matchIndex];
        var lastUsingEntity =
            textBeforeMatch.LastIndexOf(EfAnalysisConstants.EfMethods.UsingEntity, StringComparison.Ordinal);

        if (lastUsingEntity < 0)
        {
            return false;
        }

        var textBetween = configSection[lastUsingEntity..matchIndex];
        var openParens = textBetween.Count(c => c == '(');
        var closeParens = textBetween.Count(c => c == ')');

        return openParens > closeParens;
    }
}
