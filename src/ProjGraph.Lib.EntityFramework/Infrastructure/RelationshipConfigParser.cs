using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;
using System.Text.RegularExpressions;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Parses relationship configurations (HasOne/HasMany/WithOne/WithMany) from Fluent API sections.
/// </summary>
internal static class RelationshipConfigParser
{
    /// <summary>
    /// Parses shadow relationships from a given configuration section and adds them to the provided list.
    /// </summary>
    public static void ParseShadowRelationships(
        string configSection,
        string entityName,
        Dictionary<string, EfEntity> entities,
        List<EfRelationship> shadowRelationships)
    {
        var shadowMatches = EfAnalysisRegexPatterns.ShadowRelationshipRegex().Matches(configSection);

        foreach (Match shadowMatch in shadowMatches)
        {
            if (FluentApiParsingUtilities.IsInsideUsingEntityBlock(configSection, shadowMatch.Index))
            {
                continue;
            }

            var hasMethod = shadowMatch.Groups[1].Value;
            var targetEntityName = shadowMatch.Groups[2].Value;
            var withMethod = shadowMatch.Groups[3].Value;

            if (!entities.ContainsKey(targetEntityName))
            {
                continue;
            }

            var rel = CreateShadowRelationship(entityName, targetEntityName, hasMethod, withMethod);
            shadowRelationships.Add(rel);
        }
    }

    /// <summary>
    /// Parses explicit relationships (e.g., HasOne, HasMany) from a given configuration section.
    /// </summary>
    public static void ParseExplicitRelationships(
        string configSection,
        string entityName,
        Dictionary<string, EfEntity> entities,
        List<EfRelationship> relationships,
        Compilation compilation)
    {
        var matches = EfAnalysisRegexPatterns.MethodCallRegex().Matches(configSection);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];

            if (FluentApiParsingUtilities.IsInsideUsingEntityBlock(configSection, match.Index))
            {
                continue;
            }

            var methodName = match.Groups[1].Value;
            var args = match.Groups[2].Value;

            if (!methodName.StartsWith(EfAnalysisConstants.EfMethods.HasOne) &&
                !methodName.StartsWith(EfAnalysisConstants.EfMethods.HasMany))
            {
                continue;
            }

            var relationship = TryCreateRelationship(matches, i, methodName, args, entityName, entities, compilation);
            if (relationship is null)
            {
                continue;
            }

            ApplyForeignKeyConfiguration(matches, i, methodName, entityName, relationship.TargetEntity, entities);
            relationships.Add(relationship);
        }
    }

    /// <summary>
    /// Attempts to create a relationship from method call information.
    /// </summary>
    private static EfRelationship? TryCreateRelationship(
        MatchCollection matches,
        int startIndex,
        string methodName,
        string args,
        string entityName,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        var targetEntityName = FluentApiParsingUtilities.ExtractTargetName(args);

        if (string.IsNullOrEmpty(targetEntityName))
        {
            targetEntityName = FluentApiParsingUtilities.ExtractGenericType(methodName);
        }

        if (!string.IsNullOrEmpty(targetEntityName) && !entities.ContainsKey(targetEntityName))
        {
            var resolvedName =
                ResolveNavigationPropertyToEntityType(entityName, targetEntityName, entities, compilation);
            if (resolvedName is not null)
            {
                targetEntityName = resolvedName;
            }
        }

        if (string.IsNullOrEmpty(targetEntityName))
        {
            return null;
        }

        var method = FindWithMethodInfo(matches, startIndex);
        if (method is null)
        {
            return null;
        }

        var isRequired = IsRelationshipRequired(matches, startIndex);
        return CreateShadowRelationship(entityName, targetEntityName, methodName, method, isRequired);
    }

    /// <summary>
    /// Resolves a navigation property name to its target entity type.
    /// </summary>
    private static string? ResolveNavigationPropertyToEntityType(
        string sourceEntityName,
        string navigationPropertyName,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        var sourceSymbol = compilation.GetSymbolsWithName(sourceEntityName, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault();

        var navProperty = sourceSymbol?.GetMembers().OfType<IPropertySymbol>()
            .FirstOrDefault(p => p.Name.Equals(navigationPropertyName, StringComparison.OrdinalIgnoreCase));

        if (navProperty is null)
        {
            return null;
        }

        if (NavigationPropertyAnalyzer.IsNavigationProperty(navProperty, out var targetType, out _) &&
            targetType is not null && entities.ContainsKey(targetType.Name))
        {
            return targetType.Name;
        }

        return null;
    }

    /// <summary>
    /// Applies foreign key configuration to the appropriate entity.
    /// </summary>
    private static void ApplyForeignKeyConfiguration(
        MatchCollection matches,
        int startIndex,
        string methodName,
        string sourceEntityName,
        string targetEntityName,
        Dictionary<string, EfEntity> entities)
    {
        var (fkEntityNameOverride, fkPropNames) = FindForeignKeyInfo(matches, startIndex);
        if (fkPropNames.Count is 0)
        {
            return;
        }

        var dependentEntityName =
            DetermineDependentEntity(methodName, sourceEntityName, targetEntityName, fkEntityNameOverride);

        if (entities.TryGetValue(dependentEntityName, out var dependentEntity))
        {
            MarkPropertiesAsForeignKeys(dependentEntity, fkPropNames);
        }
    }

    /// <summary>
    /// Determines which entity is the dependent entity (holds the foreign key).
    /// </summary>
    private static string DetermineDependentEntity(
        string methodName,
        string sourceEntityName,
        string targetEntityName,
        string? fkEntityNameOverride)
    {
        if (!string.IsNullOrEmpty(fkEntityNameOverride))
        {
            return fkEntityNameOverride;
        }

        return methodName.StartsWith(EfAnalysisConstants.EfMethods.HasOne) ? sourceEntityName : targetEntityName;
    }

    /// <summary>
    /// Marks the specified properties as foreign keys in the entity.
    /// </summary>
    private static void MarkPropertiesAsForeignKeys(EfEntity entity, List<string> propertyNames)
    {
        foreach (var prop in propertyNames.Select(propName =>
                     FluentApiParsingUtilities.GetOrCreateProperty(entity, propName, "")))
        {
            prop.IsForeignKey = true;
        }
    }

    private static bool IsRelationshipRequired(MatchCollection matches, int startIndex)
    {
        for (var j = startIndex + 1; j < Math.Min(startIndex + 10, matches.Count); j++)
        {
            var nextMethod = matches[j].Groups[1].Value;
            if (nextMethod is not EfAnalysisConstants.EfMethods.IsRequired)
            {
                continue;
            }

            var arg = matches[j].Groups[2].Value.Trim();
            return string.IsNullOrEmpty(arg) || arg.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Finds the corresponding HasForeignKey method call following a HasOne or HasMany call.
    /// </summary>
    private static (string? EntityNameOverride, List<string> PropertyNames) FindForeignKeyInfo(
        MatchCollection matches,
        int startIndex)
    {
        for (var j = startIndex + 1; j < Math.Min(startIndex + 10, matches.Count); j++)
        {
            var nextMethodMatch = matches[j].Groups[1].Value;
            if (!nextMethodMatch.StartsWith(EfAnalysisConstants.EfMethods.HasForeignKey))
            {
                if (nextMethodMatch.Contains(EfAnalysisConstants.EfMethods.Entity) ||
                    nextMethodMatch.StartsWith(EfAnalysisConstants.EfMethods.HasOne) ||
                    nextMethodMatch.StartsWith(EfAnalysisConstants.EfMethods.HasMany) ||
                    nextMethodMatch.StartsWith(EfAnalysisConstants.EfMethods.ToTable))
                {
                    break;
                }

                continue;
            }

            var typeName = FluentApiParsingUtilities.ExtractGenericType(nextMethodMatch);
            var propNames = FluentApiParsingUtilities.ExtractPropertyNamesFromArgs(matches[j].Groups[2].Value);
            return (typeName, propNames);
        }

        return (null, []);
    }

    /// <summary>
    /// Finds the corresponding WithOne or WithMany method call following a HasOne or HasMany call.
    /// </summary>
    private static string? FindWithMethodInfo(MatchCollection matches, int startIndex)
    {
        for (var j = startIndex + 1; j < Math.Min(startIndex + 10, matches.Count); j++)
        {
            var nextMethod = matches[j].Groups[1].Value;
            if (nextMethod.StartsWith(EfAnalysisConstants.EfMethods.WithOne) ||
                nextMethod.StartsWith(EfAnalysisConstants.EfMethods.WithMany))
            {
                return nextMethod;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates an EfRelationship from has/with method combination.
    /// </summary>
    public static EfRelationship CreateShadowRelationship(string sourceEntity, string targetEntity, string hasMethod,
        string withMethod, bool isRequired = false)
    {
        return (hasMethod, withMethod) switch
        {
            (EfAnalysisConstants.EfMethods.HasOne, EfAnalysisConstants.EfMethods.WithMany) => new EfRelationship
            {
                SourceEntity = targetEntity,
                TargetEntity = sourceEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = true
            },
            (EfAnalysisConstants.EfMethods.HasMany, EfAnalysisConstants.EfMethods.WithOne) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = true
            },
            (EfAnalysisConstants.EfMethods.HasOne, EfAnalysisConstants.EfMethods.WithOne) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.OneToOne,
                IsRequired = isRequired
            },
            (EfAnalysisConstants.EfMethods.HasMany, EfAnalysisConstants.EfMethods.WithMany) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.ManyToMany,
                IsRequired = isRequired
            },
            _ => new EfRelationship
            {
                SourceEntity = targetEntity,
                TargetEntity = sourceEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = true
            }
        };
    }
}
