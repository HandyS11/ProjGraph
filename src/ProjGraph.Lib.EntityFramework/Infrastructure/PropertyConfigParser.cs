using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Parses property configurations (HasKey, Property, IsRequired, HasMaxLength, etc.) from Fluent API sections.
/// </summary>
internal static class PropertyConfigParser
{
    /// <summary>
    /// Parses property configurations from a given configuration section and applies them to the specified entity.
    /// </summary>
    /// <param name="configSection">The configuration section text to parse.</param>
    /// <param name="entity">The entity to apply configurations to.</param>
    /// <param name="compilation">The Roslyn compilation for symbol resolution.</param>
    public static void ParsePropertyConfigurations(string configSection, EfEntity entity, Compilation compilation)
    {
        EfProperty? currentProperty = null;

        var matches = EfAnalysisRegexPatterns.MethodCallRegex().Matches(configSection);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            if (FluentApiParsingUtilities.IsInsideUsingEntityBlock(configSection, match.Index))
            {
                continue;
            }

            var groups = match.Groups;
            var methodName = groups[1].Value;
            var args = groups[2].Value;

            if (methodName == EfAnalysisConstants.EfMethods.Property ||
                methodName.StartsWith(EfAnalysisConstants.EfMethods.Property + "<", StringComparison.Ordinal))
            {
                currentProperty = ProcessPropertyDeclaration(entity, methodName, args);
            }
            else if (methodName == EfAnalysisConstants.EfMethods.HasKey ||
                     methodName == EfAnalysisConstants.EfMethods.ToTable ||
                     methodName.StartsWith(EfAnalysisConstants.EfMethods.HasOne, StringComparison.Ordinal) ||
                     methodName.StartsWith(EfAnalysisConstants.EfMethods.HasMany, StringComparison.Ordinal))
            {
                if (methodName == EfAnalysisConstants.EfMethods.HasKey)
                {
                    ApplyKeyConfiguration(entity, args);
                }

                currentProperty = null;
            }
            else if (currentProperty != null)
            {
                var updated = ApplyPropertyConfiguration(currentProperty, methodName, args, compilation);
                if (!ReferenceEquals(updated, currentProperty))
                {
                    var index = entity.Properties.IndexOf(currentProperty);
                    if (index >= 0)
                    {
                        entity.Properties[index] = updated;
                    }

                    currentProperty = updated;
                }
            }
        }
    }

    /// <summary>
    /// Applies primary key configuration to the entity.
    /// </summary>
    /// <param name="entity">The entity to configure.</param>
    /// <param name="args">The HasKey method arguments.</param>
    private static void ApplyKeyConfiguration(EfEntity entity, string args)
    {
        foreach (var propName in FluentApiParsingUtilities.ExtractPropertyNamesFromArgs(args))
        {
            var prop = FluentApiParsingUtilities.GetOrCreateProperty(entity, propName, "");
            var updated = EfPropertyFactory.CopyWith(prop, isPrimaryKey: true);
            var index = entity.Properties.IndexOf(prop);
            if (index >= 0)
            {
                entity.Properties[index] = updated;
            }
        }
    }

    /// <summary>
    /// Processes a Property declaration and returns or creates the corresponding EfProperty.
    /// </summary>
    /// <param name="entity">The entity containing the property.</param>
    /// <param name="methodName">The Property method name (may contain generic type).</param>
    /// <param name="args">The method arguments.</param>
    private static EfProperty? ProcessPropertyDeclaration(EfEntity entity, string methodName, string args)
    {
        var propName = ExtractPropertyName(args);
        if (string.IsNullOrEmpty(propName))
        {
            return null;
        }

        var type = FluentApiParsingUtilities.ExtractGenericType(methodName);
        return FluentApiParsingUtilities.GetOrCreateProperty(entity, propName, type);
    }

    /// <summary>
    /// Extracts the property name from method arguments.
    /// </summary>
    /// <param name="args">The method arguments string.</param>
    private static string ExtractPropertyName(string args)
    {
        var lambdaMatch = EfAnalysisRegexPatterns.PropertyLambdaRegex().Match(args);
        return lambdaMatch.Success ? lambdaMatch.Groups[2].Value : args.Trim('"', ' ');
    }

    /// <summary>
    /// Applies a specific configuration to a given property based on the provided configuration method.
    /// </summary>
    /// <param name="property">The property to configure.</param>
    /// <param name="configMethod">The configuration method name.</param>
    /// <param name="configArg">The configuration argument value.</param>
    /// <param name="compilation">The Roslyn compilation for symbol resolution.</param>
    private static EfProperty ApplyPropertyConfiguration(EfProperty property, string configMethod, string configArg,
        Compilation compilation)
    {
        return configMethod switch
        {
            EfAnalysisConstants.EfMethods.IsRequired => ApplyIsRequiredConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasMaxLength => ApplyMaxLengthConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasPrecision => ApplyPrecisionConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasColumnType => ApplyColumnTypeConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasDefaultValue => DefaultValueResolver.CreateWithDefaultValue(property,
                configArg, compilation),
            EfAnalysisConstants.EfMethods.HasDefaultValueSql => DefaultValueResolver.CreateWithDefaultValueSql(
                property, configArg),
            _ => property
        };
    }

    private static EfProperty ApplyIsRequiredConfiguration(EfProperty property, string configArg)
    {
        var isRequired = string.IsNullOrEmpty(configArg) ||
                         configArg.Equals("true", StringComparison.OrdinalIgnoreCase);
        return EfPropertyFactory.CopyWith(property,
            isRequired: isRequired,
            isExplicitlyRequired: isRequired || property.IsExplicitlyRequired);
    }

    private static EfProperty ApplyMaxLengthConfiguration(EfProperty property, string configArg)
    {
        if (int.TryParse(configArg, out var maxLen))
        {
            return EfPropertyFactory.CopyWith(property, maxLength: maxLen);
        }

        return property;
    }

    /// <summary>
    /// Configures the column type for a property, inferring max length from column type definition if needed.
    /// </summary>
    /// <param name="property">The property to configure.</param>
    /// <param name="configArg">The column type argument.</param>
    private static EfProperty ApplyColumnTypeConfiguration(EfProperty property, string configArg)
    {
        if (property.MaxLength is not null)
        {
            return property;
        }

        var match = EfAnalysisRegexPatterns.NumberInParensRegex().Match(configArg);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var len))
        {
            return EfPropertyFactory.CopyWith(property, maxLength: len);
        }

        return property;
    }

    /// <summary>
    /// Creates a new property with the specified precision and scale.
    /// </summary>
    /// <param name="property">The source property.</param>
    /// <param name="configArg">The precision/scale argument string.</param>
    private static EfProperty ApplyPrecisionConfiguration(EfProperty property, string configArg)
    {
        var precisionArgs = configArg.Split(',');
        if (precisionArgs.Length < 1 || !int.TryParse(precisionArgs[0].Trim(), out var precision))
        {
            return property;
        }

        int? scale = null;
        if (precisionArgs.Length >= 2 && int.TryParse(precisionArgs[1].Trim(), out var s))
        {
            scale = s;
        }

        return EfPropertyFactory.CopyWith(property, precision: precision, scale: scale ?? property.Scale);
    }
}
