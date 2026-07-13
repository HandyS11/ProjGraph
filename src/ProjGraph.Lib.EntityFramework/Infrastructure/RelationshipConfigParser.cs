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
    /// <param name="configSection">The configuration section text to parse.</param>
    /// <param name="entityName">The name of the source entity.</param>
    /// <param name="entities">The dictionary of entities in the model.</param>
    /// <param name="shadowRelationships">The list to add discovered shadow relationships to.</param>
    public static void ParseShadowRelationships(
        string configSection,
        string entityName,
        Dictionary<string, EfEntity> entities,
        List<EfRelationship> shadowRelationships)
    {
        foreach (Match shadowMatch in EfAnalysisRegexPatterns.ShadowRelationshipRegex().Matches(configSection))
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

            // Shadow relationships carry no explicit .IsRequired() configuration, so the convention
            // default for the relationship kind is applied (required for one-to-many).
            var rel = CreateShadowRelationship(entityName, targetEntityName, hasMethod, withMethod);
            shadowRelationships.Add(rel);
        }
    }

    /// <summary>
    /// Parses explicit relationships (e.g., HasOne, HasMany) from a given configuration section.
    /// </summary>
    /// <param name="configSection">The configuration section text to parse.</param>
    /// <param name="entityName">The name of the source entity.</param>
    /// <param name="entities">The dictionary of entities in the model.</param>
    /// <param name="relationships">The list to add discovered relationships to.</param>
    /// <param name="compilation">The Roslyn compilation for symbol resolution.</param>
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

            if (!methodName.StartsWith(EfAnalysisConstants.EfMethods.HasOne, StringComparison.Ordinal) &&
                !methodName.StartsWith(EfAnalysisConstants.EfMethods.HasMany, StringComparison.Ordinal))
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
    /// <param name="matches">The collection of regex matches.</param>
    /// <param name="startIndex">The index of the current match.</param>
    /// <param name="methodName">The method name (HasOne/HasMany).</param>
    /// <param name="args">The method arguments.</param>
    /// <param name="entityName">The source entity name.</param>
    /// <param name="entities">The dictionary of entities.</param>
    /// <param name="compilation">The Roslyn compilation.</param>
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

        var explicitRequired = FindExplicitRequired(matches, startIndex);
        return CreateShadowRelationship(entityName, targetEntityName, methodName, method, explicitRequired);
    }

    /// <summary>
    /// Resolves a navigation property name to its target entity type.
    /// </summary>
    /// <param name="sourceEntityName">The source entity name.</param>
    /// <param name="navigationPropertyName">The navigation property name to resolve.</param>
    /// <param name="entities">The dictionary of entities.</param>
    /// <param name="compilation">The Roslyn compilation.</param>
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
    /// <param name="matches">The collection of regex matches.</param>
    /// <param name="startIndex">The index of the current match.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="sourceEntityName">The source entity name.</param>
    /// <param name="targetEntityName">The target entity name.</param>
    /// <param name="entities">The dictionary of entities.</param>
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
    /// <param name="methodName">The method name.</param>
    /// <param name="sourceEntityName">The source entity name.</param>
    /// <param name="targetEntityName">The target entity name.</param>
    /// <param name="fkEntityNameOverride">Optional entity name override from HasForeignKey generic type.</param>
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

        return methodName.StartsWith(EfAnalysisConstants.EfMethods.HasOne, StringComparison.Ordinal)
            ? sourceEntityName
            : targetEntityName;
    }

    /// <summary>
    /// Marks the specified properties as foreign keys in the entity.
    /// </summary>
    /// <param name="entity">The entity containing the properties.</param>
    /// <param name="propertyNames">The names of properties to mark as foreign keys.</param>
    private static void MarkPropertiesAsForeignKeys(EfEntity entity, List<string> propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            var prop = FluentApiParsingUtilities.GetOrCreateProperty(entity, propName, "");
            var updated = EfPropertyFactory.CopyWith(prop, new EfPropertyOverrides
            {
                IsForeignKey = true
            });
            var index = entity.Properties.IndexOf(prop);
            if (index >= 0)
            {
                entity.Properties[index] = updated;
            }
        }
    }

    /// <summary>
    /// Detects an explicit <c>.IsRequired(...)</c> configuration in the chain following a relationship.
    /// </summary>
    /// <param name="matches">The collection of regex matches.</param>
    /// <param name="startIndex">The index of the relationship's Has method.</param>
    /// <returns>
    /// <see langword="true"/> or <see langword="false"/> when an explicit <c>.IsRequired(...)</c> call is
    /// found; <see langword="null"/> when none is present, so the caller can apply the convention default.
    /// </returns>
    private static bool? FindExplicitRequired(MatchCollection matches, int startIndex)
    {
        for (var j = startIndex + 1; j < Math.Min(startIndex + 10, matches.Count); j++)
        {
            var nextMethod = matches[j].Groups[1].Value;
            if (nextMethod is EfAnalysisConstants.EfMethods.IsRequired)
            {
                var arg = matches[j].Groups[2].Value.Trim();
                return string.IsNullOrEmpty(arg) || arg.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            if (IsChainBoundary(nextMethod))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether a method call starts a new fluent chain, meaning a forward scan for
    /// chain members of the current relationship must stop to avoid associating configuration
    /// from an unrelated statement.
    /// </summary>
    /// <param name="methodName">The method name from the match.</param>
    private static bool IsChainBoundary(string methodName)
    {
        return methodName.Contains(EfAnalysisConstants.EfMethods.Entity, StringComparison.Ordinal) ||
               methodName.StartsWith(EfAnalysisConstants.EfMethods.HasOne, StringComparison.Ordinal) ||
               methodName.StartsWith(EfAnalysisConstants.EfMethods.HasMany, StringComparison.Ordinal) ||
               methodName.StartsWith(EfAnalysisConstants.EfMethods.HasKey, StringComparison.Ordinal) ||
               methodName.StartsWith(EfAnalysisConstants.EfMethods.Property, StringComparison.Ordinal) ||
               methodName.StartsWith(EfAnalysisConstants.EfMethods.ToTable, StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds the corresponding HasForeignKey method call following a HasOne or HasMany call.
    /// </summary>
    /// <param name="matches">The collection of regex matches.</param>
    /// <param name="startIndex">The starting index to search from.</param>
    private static (string? EntityNameOverride, List<string> PropertyNames) FindForeignKeyInfo(
        MatchCollection matches,
        int startIndex)
    {
        for (var j = startIndex + 1; j < Math.Min(startIndex + 10, matches.Count); j++)
        {
            var nextMethodMatch = matches[j].Groups[1].Value;
            if (!nextMethodMatch.StartsWith(EfAnalysisConstants.EfMethods.HasForeignKey, StringComparison.Ordinal))
            {
                if (IsChainBoundary(nextMethodMatch))
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
    /// <param name="matches">The collection of regex matches.</param>
    /// <param name="startIndex">The starting index to search from.</param>
    private static string? FindWithMethodInfo(MatchCollection matches, int startIndex)
    {
        for (var j = startIndex + 1; j < Math.Min(startIndex + 10, matches.Count); j++)
        {
            var nextMethod = matches[j].Groups[1].Value;
            if (nextMethod.StartsWith(EfAnalysisConstants.EfMethods.WithOne, StringComparison.Ordinal) ||
                nextMethod.StartsWith(EfAnalysisConstants.EfMethods.WithMany, StringComparison.Ordinal))
            {
                return nextMethod;
            }

            if (IsChainBoundary(nextMethod))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates an EfRelationship from has/with method combination.
    /// </summary>
    /// <param name="sourceEntity">The source entity name.</param>
    /// <param name="targetEntity">The target entity name.</param>
    /// <param name="hasMethod">The Has method name (HasOne/HasMany).</param>
    /// <param name="withMethod">The With method name (WithOne/WithMany).</param>
    /// <param name="explicitRequired">
    /// The explicit <c>.IsRequired(...)</c> value when configured, or <see langword="null"/> to apply the
    /// EF convention default for the relationship kind (required for one-to-many, optional otherwise).
    /// </param>
    public static EfRelationship CreateShadowRelationship(string sourceEntity, string targetEntity, string hasMethod,
        string withMethod, bool? explicitRequired = null)
    {
        return (hasMethod, withMethod) switch
        {
            (EfAnalysisConstants.EfMethods.HasOne, EfAnalysisConstants.EfMethods.WithMany) => new EfRelationship
            {
                SourceEntity = targetEntity,
                TargetEntity = sourceEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = explicitRequired ?? true
            },
            (EfAnalysisConstants.EfMethods.HasMany, EfAnalysisConstants.EfMethods.WithOne) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = explicitRequired ?? true
            },
            (EfAnalysisConstants.EfMethods.HasOne, EfAnalysisConstants.EfMethods.WithOne) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.OneToOne,
                IsRequired = explicitRequired ?? false
            },
            (EfAnalysisConstants.EfMethods.HasMany, EfAnalysisConstants.EfMethods.WithMany) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.ManyToMany,
                IsRequired = explicitRequired ?? false
            },
            _ => new EfRelationship
            {
                SourceEntity = targetEntity,
                TargetEntity = sourceEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = explicitRequired ?? true
            }
        };
    }
}
