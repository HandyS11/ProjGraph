using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Services.EfAnalysis.Constants;
using ProjGraph.Lib.Services.EfAnalysis.Patterns;
using System.Text.RegularExpressions;

namespace ProjGraph.Lib.Services.EfAnalysis;

/// <summary>
/// A static partial class that provides methods for parsing and applying Fluent API configurations
/// to an Entity Framework model. This class includes methods for processing entity configuration sections,
/// finding the "OnModelCreating" method, and parsing shadow relationships and property configurations.
/// </summary>
/// <remarks>
/// This class is designed to work with Entity Framework models and uses regular expressions to parse
/// Fluent API configurations. It includes methods for ensuring unique relationships and applying
/// property configurations to entities.
/// </remarks>
public static class FluentApiConfigurationParser
{
    /// <summary>
    /// Applies Fluent API constraints to the specified Entity Framework model by parsing the "OnModelCreating" method
    /// of the provided context type and processing each entity configuration section.
    /// </summary>
    /// <param name="contextType">The <see cref="INamedTypeSymbol"/> representing the context type containing the "OnModelCreating" method.</param>
    /// <param name="entities">
    /// A dictionary of all entities, where the key is the entity name and the value is the <see cref="EfEntity"/> object.
    /// </param>
    /// <param name="model">The <see cref="EfModel"/> object to which the parsed Fluent API constraints will be applied.</param>
    /// <param name="compilation">The <see cref="Compilation"/> used to find symbols for shadow entities.</param>
    /// <remarks>
    /// This method retrieves the "OnModelCreating" method from the specified context type using the <see cref="FindOnModelCreatingMethod"/> method.
    /// It then splits the method's content into individual entity configuration sections using the <see cref="EntitySplitRegex"/>.
    /// Each section is processed using the <see cref="ProcessEntityConfigSection"/> method to extract and apply the constraints.
    /// </remarks>
    public static void ApplyFluentApiConstraints(
        INamedTypeSymbol contextType,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        var methodSyntax = FindOnModelCreatingMethod(contextType);
        if (methodSyntax?.Body is null)
        {
            return;
        }

        ApplyConstraintsFromMethod(methodSyntax, entities, model, compilation);
    }

    /// <summary>
    /// Applies Fluent API constraints from a specific method (e.g., OnModelCreating or BuildModel) 
    /// to the specified Entity Framework model.
    /// </summary>
    /// <param name="methodSyntax">The <see cref="MethodDeclarationSyntax"/> representing the method to parse.</param>
    /// <param name="entities">
    /// A dictionary of all entities, where the key is the entity name and the value is the <see cref="EfEntity"/> object.
    /// </param>
    /// <param name="model">The <see cref="EfModel"/> object to which the parsed Fluent API constraints will be applied.</param>
    /// <param name="compilation">The <see cref="Compilation"/> used to find symbols for entities.</param>
    public static void ApplyConstraintsFromMethod(
        MethodDeclarationSyntax methodSyntax,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        if (methodSyntax.Body is null)
        {
            return;
        }

        var methodText = methodSyntax.ToString();
        var entityConfigSections = EfAnalysisRegexPatterns.EntitySplitRegex().Split(methodText);

        // Skip the first part (before the first .Entity)
        for (var i = 1; i < entityConfigSections.Length; i++)
        {
            ProcessEntityConfigSection(entityConfigSections[i], entities, model, compilation);
        }
    }

    /// <summary>
    /// Finds the "OnModelCreating" method declaration within the specified context type.
    /// </summary>
    /// <param name="contextType">The <see cref="INamedTypeSymbol"/> representing the context type to search for the method.</param>
    /// <returns>
    /// A <see cref="MethodDeclarationSyntax"/> object representing the syntax of the "OnModelCreating" method,
    /// or <c>null</c> if the method is not found.
    /// </returns>
    /// <remarks>
    /// This method searches for a member named "OnModelCreating" in the provided context type.
    /// If found, it retrieves the syntax reference of the method and returns its syntax as a <see cref="MethodDeclarationSyntax"/> object.
    /// </remarks>
    private static MethodDeclarationSyntax? FindOnModelCreatingMethod(INamedTypeSymbol contextType)
    {
        var onModelCreating = contextType.GetMembers(EfAnalysisConstants.EfMethods.OnModelCreating)
            .OfType<IMethodSymbol>()
            .FirstOrDefault();

        var syntaxRef = onModelCreating?.DeclaringSyntaxReferences.FirstOrDefault();
        return syntaxRef?.GetSyntax() as MethodDeclarationSyntax;
    }

    /// <summary>
    /// Processes a configuration section for a specific entity and updates the model with the parsed relationships.
    /// </summary>
    /// <param name="sectionContent">The content of the configuration section for the entity.</param>
    /// <param name="entities">
    /// A dictionary of all entities, where the key is the entity name and the value is the <see cref="EfEntity"/> object.
    /// </param>
    /// <param name="model">The <see cref="EfModel"/> object to which the parsed relationships will be added.</param>
    /// <param name="compilation">The <see cref="Compilation"/> used to find symbols for shadow entities.</param>
    /// <remarks>
    /// This method extracts the configuration for a single entity from the provided section content.
    /// It identifies shadow relationships using the <see cref="ParseEntityConfiguration"/> method and ensures
    /// that only unique relationships are added to the model using the <see cref="AddUniqueRelationships"/> method.
    /// </remarks>
    private static void ProcessEntityConfigSection(
        string sectionContent,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        // Add back "Entity" which was removed by the split
        var section = EfAnalysisConstants.EfMethods.Entity + sectionContent;

        // Extract just this entity's configuration (up to the next .Entity)
        var entityConfigEnd = EfAnalysisRegexPatterns.EntitySplitRegex().Match(section, 7).Index;
        if (entityConfigEnd > 0)
        {
            section = section[..entityConfigEnd];
        }

        var shadowRelationships = ParseEntityConfiguration(section, entities, model, compilation);
        AddUniqueRelationships(shadowRelationships, model);
    }

    /// <summary>
    /// Adds unique relationships from the provided list to the model's relationships collection.
    /// </summary>
    /// <param name="relationships">A list of <see cref="EfRelationship"/> objects to be added to the model.</param>
    /// <param name="model">The <see cref="EfModel"/> object to which the relationships will be added.</param>
    /// <remarks>
    /// This method ensures that only unique relationships are added to the model by checking if a relationship
    /// with the same key already exists in the model's relationships collection. Uses the same key generation
    /// logic as RelationshipAnalyzer to ensure consistency.
    /// </remarks>
    private static void AddUniqueRelationships(List<EfRelationship> relationships, EfModel model)
    {
        var existingKeys = model.Relationships.Select(GenerateRelationshipKey).ToHashSet();

        var uniqueRelationships = relationships
            .Where(relationship => existingKeys.Add(GenerateRelationshipKey(relationship)));

        model.Relationships.AddRange(uniqueRelationships);
    }

    /// <summary>
    /// Generates a unique key for a relationship to enable deduplication.
    /// Uses the same logic as RelationshipAnalyzer to ensure consistency.
    /// </summary>
    /// <param name="relationship">The relationship to generate a key for.</param>
    /// <returns>A unique string key representing the relationship.</returns>
    private static string GenerateRelationshipKey(EfRelationship relationship)
    {
        // For symmetric relationships (1:1, M:M), sort entity names to avoid duplicates
        if (relationship.Type is EfRelationshipType.OneToOne or EfRelationshipType.ManyToMany)
        {
            var entitiesSorted = new[] { relationship.SourceEntity, relationship.TargetEntity }
                .OrderBy(e => e)
                .ToArray();
            return
                $"{entitiesSorted[0]}{EfAnalysisConstants.RelationshipKeys.Delimiter}{entitiesSorted[1]}{EfAnalysisConstants.RelationshipKeys.Delimiter}{relationship.Type}";
        }

        // For OneToMany, direction matters
        return
            $"{relationship.SourceEntity}{EfAnalysisConstants.RelationshipKeys.Delimiter}{relationship.TargetEntity}{EfAnalysisConstants.RelationshipKeys.Delimiter}{relationship.Type}";
    }

    /// <summary>
    /// Parses the configuration section of an entity to extract shadow relationships and property configurations.
    /// </summary>
    /// <param name="configSection">The configuration section containing the entity's Fluent API configurations.</param>
    /// <param name="entities">
    /// A dictionary of all entities, where the key is the entity name and the value is the <see cref="EfEntity"/> object.
    /// </param>
    /// <param name="model">The <see cref="EfModel"/> to which discovered shadow entities will be added.</param>
    /// <param name="compilation">The <see cref="Compilation"/> used to find symbols for shadow entities.</param>
    /// <returns>
    /// A list of <see cref="EfRelationship"/> objects representing the shadow relationships parsed from the configuration section.
    /// </returns>
    /// <remarks>
    /// This method first attempts to match the entity name in the configuration section using the <see cref="EntityNameRegex"/>.
    /// If the entity is not found in the dictionary, it attempts to find and analyze it from the compilation.
    /// Then it proceeds to parse shadow relationships and property configurations
    /// for the entity using the <see cref="ParseShadowRelationships"/> and <see cref="ParsePropertyConfigurations"/> methods.
    /// </remarks>
    private static List<EfRelationship> ParseEntityConfiguration(
        string configSection,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        var shadowRelationships = new List<EfRelationship>();

        var entityMatch = EfAnalysisRegexPatterns.EntityNameRegex().Match(configSection);
        if (!entityMatch.Success)
        {
            return shadowRelationships;
        }

        var entityName = entityMatch.Groups[1].Value;
        if (string.IsNullOrEmpty(entityName))
        {
            entityName = entityMatch.Groups[2].Value;
        }

        // Simplify name if it contains namespace
        if (entityName.Contains('.'))
        {
            entityName = entityName.Split('.')[^1];
        }

        if (!entities.TryGetValue(entityName, out var entity))
        {
            var symbol = compilation.GetSymbolsWithName(entityName, SymbolFilter.Type)
                .OfType<INamedTypeSymbol>()
                .FirstOrDefault();

            entity = symbol != null
                ? EntityAnalyzer.AnalyzeEntity(symbol)
                :
                // Create a basic entity if symbol not found (common in ModelSnapshots)
                new EfEntity { Name = entityName };

            entities[entityName] = entity;

            // Only add to model if not already present (prevents duplicates)
            if (model.Entities.All(e => e.Name != entityName))
            {
                model.Entities.Add(entity);
            }
        }

        ParseShadowRelationships(configSection, entityName, entities, shadowRelationships);
        ParseExplicitRelationships(configSection, entityName, entities, shadowRelationships, compilation);
        ParsePropertyConfigurations(configSection, entity);

        // Parse table mapping
        var tableMatch = EfAnalysisRegexPatterns.ToTableRegex().Match(configSection);
        if (tableMatch.Success)
        {
            entity.TableName = tableMatch.Groups[1].Value;
        }

        return shadowRelationships;
    }

    /// <summary>
    /// Parses shadow relationships from a given configuration section and adds them to the provided list of relationships.
    /// </summary>
    /// <param name="configSection">The configuration section containing shadow relationship definitions.</param>
    /// <param name="entityName">The name of the entity for which shadow relationships are being parsed.</param>
    /// <param name="entities">A dictionary of all entities, where the key is the entity name and the value is the <see cref="EfEntity"/> object.</param>
    /// <param name="shadowRelationships">The list of <see cref="EfRelationship"/> objects to which the parsed shadow relationships will be added.</param>
    /// <remarks>
    /// This method uses the <see cref="ShadowRelationshipRegex"/> to find shadow relationship definitions in the configuration section.
    /// It skips any matches that are inside a "UsingEntity" block, as determined by the <see cref="IsInsideUsingEntityBlock"/> method.
    /// For each valid match, it creates a new <see cref="EfRelationship"/> object and adds it to the provided list of shadow relationships.
    /// </remarks>
    private static void ParseShadowRelationships(
        string configSection,
        string entityName,
        Dictionary<string, EfEntity> entities,
        List<EfRelationship> shadowRelationships)
    {
        var shadowMatches = EfAnalysisRegexPatterns.ShadowRelationshipRegex().Matches(configSection);

        foreach (Match shadowMatch in shadowMatches)
        {
            if (IsInsideUsingEntityBlock(configSection, shadowMatch.Index))
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
    /// handles string-based names common in ModelSnapshots.
    /// </summary>
    private static void ParseExplicitRelationships(
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
    /// <returns>The created relationship, or null if creation failed.</returns>
    private static EfRelationship? TryCreateRelationship(
        MatchCollection matches,
        int startIndex,
        string methodName,
        string args,
        string entityName,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        var targetEntityName = ExtractTargetName(args);

        if (string.IsNullOrEmpty(targetEntityName))
        {
            // Try to extract from generic type: HasOne<ActivityType>()
            targetEntityName = ExtractGenericType(methodName);
        }

        // If we got a navigation property name, try to resolve it to an entity type
        if (!string.IsNullOrEmpty(targetEntityName) && !entities.ContainsKey(targetEntityName))
        {
            // Try to find the entity by checking if any entity has a property with a type matching this name
            // This handles cases like HasMany(a => a.Options) where Options is List<ActivityOption>
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
    /// Resolves a navigation property name to its target entity type by examining the source entity's properties.
    /// </summary>
    /// <param name="sourceEntityName">The name of the source entity.</param>
    /// <param name="navigationPropertyName">The name of the navigation property (e.g., "Options").</param>
    /// <param name="entities">Dictionary of all known entities.</param>
    /// <param name="compilation">The compilation to use for semantic analysis.</param>
    /// <returns>The target entity type name, or null if not found.</returns>
    /// <remarks>
    /// This method uses semantic analysis to find the source entity symbol and resolve the navigation property
    /// to its target entity type. For example, if the source entity has a property "Options" of type "List&lt;ActivityOption&gt;",
    /// this will resolve "Options" to "ActivityOption".
    /// </remarks>
    private static string? ResolveNavigationPropertyToEntityType(
        string sourceEntityName,
        string navigationPropertyName,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        // Find the source entity symbol using semantic analysis
        var sourceSymbol = compilation.GetSymbolsWithName(sourceEntityName, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault();

        // Look for a property with a matching name (case-insensitive)
        var navProperty = sourceSymbol?.GetMembers().OfType<IPropertySymbol>()
            .FirstOrDefault(p => p.Name.Equals(navigationPropertyName, StringComparison.OrdinalIgnoreCase));

        if (navProperty is null)
        {
            return null;
        }

        // Check if this is a navigation property and extract the target type
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
        // Override if generic type specified in HasForeignKey<T>
        if (!string.IsNullOrEmpty(fkEntityNameOverride))
        {
            return fkEntityNameOverride;
        }

        // Default: HasOne -> current entity, HasMany -> target entity
        return methodName.StartsWith(EfAnalysisConstants.EfMethods.HasOne) ? sourceEntityName : targetEntityName;
    }

    /// <summary>
    /// Marks the specified properties as foreign keys in the entity.
    /// </summary>
    private static void MarkPropertiesAsForeignKeys(EfEntity entity, List<string> propertyNames)
    {
        foreach (var prop in propertyNames.Select(propName => GetOrCreateProperty(entity, propName, "")))
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
                    // Boundary of the relationship chain
                    break;
                }

                continue;
            }

            var typeName = ExtractGenericType(nextMethodMatch);
            var propNames = ExtractPropertyNamesFromArgs(matches[j].Groups[2].Value);
            return (typeName, propNames);
        }

        return (null, []);
    }

    /// <summary>
    /// Extracts the target entity name from method arguments.
    /// </summary>
    private static string? ExtractTargetName(string args)
    {
        // First try string literals (common in ModelSnapshots)
        var stringMatches = EfAnalysisRegexPatterns.StringLiteralRegex().Matches(args);
        if (stringMatches.Count > 0)
        {
            var name = stringMatches[0].Groups[1].Value;
            if (name.Contains('.'))
            {
                name = name.Split('.')[^1];
            }

            return name;
        }

        // Try lambda expression: e => e.NavigationProperty (common in DbContext fluent API)
        if (!args.Contains("=>"))
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

    private static EfRelationship CreateShadowRelationship(string sourceEntity, string targetEntity, string hasMethod,
        string withMethod, bool isRequired = false)
    {
        return (hasMethod, withMethod) switch
        {
            (EfAnalysisConstants.EfMethods.HasOne, EfAnalysisConstants.EfMethods.WithMany) => new EfRelationship
            {
                SourceEntity = targetEntity,
                TargetEntity = sourceEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = true // OneToMany defaults to required
            },
            (EfAnalysisConstants.EfMethods.HasMany, EfAnalysisConstants.EfMethods.WithOne) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = true // OneToMany defaults to required
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
                IsRequired = true // OneToMany defaults to required
            }
        };
    }

    /// <summary>
    /// Determines whether a match is inside a "UsingEntity" block within the given configuration section.
    /// </summary>
    /// <param name="configSection">The configuration section to search within.</param>
    /// <param name="matchIndex">The index of the match in the configuration section.</param>
    /// <returns>
    /// <c>true</c> if the match is inside a "UsingEntity" block; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method checks if the specified match index is within a "UsingEntity" block by analyzing the text
    /// before the match index. It counts the number of opening and closing parentheses to determine if the
    /// match is enclosed within a "UsingEntity" block.
    /// </remarks>
    private static bool IsInsideUsingEntityBlock(string configSection, int matchIndex)
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

    /// <summary>
    /// Parses property configurations from a given configuration section and applies them to the specified entity.
    /// </summary>
    /// <param name="configSection">The configuration section containing property configuration details.</param>
    /// <param name="entity">The <see cref="EfEntity"/> object representing the entity to which the property configurations will be applied.</param>
    /// <remarks>
    /// This method extracts property configuration details from the provided configuration section.
    /// It identifies the property name using either a lambda expression or a string argument, 
    /// and then parses all subsequent method calls (e.g., IsRequired, HasMaxLength) 
    /// to apply the corresponding configurations to the entity's property.
    /// </remarks>
    private static void ParsePropertyConfigurations(string configSection, EfEntity entity)
    {
        EfProperty? currentProperty = null;

        var matches = EfAnalysisRegexPatterns.MethodCallRegex().Matches(configSection);
        foreach (var groups in matches.Select(match => match.Groups))
        {
            var methodName = groups[1].Value;
            var args = groups[2].Value;

            if (methodName == EfAnalysisConstants.EfMethods.Property ||
                methodName.StartsWith(EfAnalysisConstants.EfMethods.Property + "<"))
            {
                currentProperty = ProcessPropertyDeclaration(entity, methodName, args);
            }
            else if (methodName == EfAnalysisConstants.EfMethods.HasKey)
            {
                ApplyKeyConfiguration(entity, args);
                currentProperty = null;
            }

            else if (currentProperty != null)
            {
                ApplyPropertyConfiguration(currentProperty, methodName, args);
            }
        }
    }

    /// <summary>
    /// Applies primary key configuration to the entity.
    /// </summary>
    private static void ApplyKeyConfiguration(EfEntity entity, string args)
    {
        var propNames = ExtractPropertyNamesFromArgs(args);
        foreach (var prop in propNames.Select(propName => GetOrCreateProperty(entity, propName, "")))
        {
            prop.IsPrimaryKey = true;
        }
    }

    /// <summary>
    /// Extracts property names from method arguments, handling both lambdas and string literals.
    /// </summary>
    private static List<string> ExtractPropertyNamesFromArgs(string args)
    {
        var result = new List<string>();

        // Handle lambda: e => new { e.P1, e.P2 } or e => e.P1
        if (args.Contains("=>"))
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
    /// Processes a Property declaration and returns or creates the corresponding EfProperty.
    /// </summary>
    /// <param name="entity">The entity containing the property.</param>
    /// <param name="methodName">The method name (e.g., "Property" or "Property&lt;T&gt;").</param>
    /// <param name="args">The method arguments.</param>
    /// <returns>The EfProperty object, or null if the property name is invalid.</returns>
    private static EfProperty? ProcessPropertyDeclaration(EfEntity entity, string methodName, string args)
    {
        var propName = ExtractPropertyName(args);
        if (string.IsNullOrEmpty(propName))
        {
            return null;
        }

        var type = ExtractGenericType(methodName);
        return GetOrCreateProperty(entity, propName, type);
    }

    /// <summary>
    /// Extracts the property name from method arguments.
    /// </summary>
    /// <param name="args">The method arguments to parse.</param>
    /// <returns>The extracted property name.</returns>
    private static string ExtractPropertyName(string args)
    {
        var lambdaMatch = EfAnalysisRegexPatterns.PropertyLambdaRegex().Match(args);
        return lambdaMatch.Success ? lambdaMatch.Groups[2].Value : args.Trim('"', ' ');
    }

    /// <summary>
    /// Extracts the generic type from a method name like "Property&lt;T&gt;".
    /// </summary>
    /// <param name="methodName">The method name to parse.</param>
    /// <returns>The extracted type, or empty string if no generic type is found.</returns>
    private static string ExtractGenericType(string methodName)
    {
        if (methodName.Contains('<') && methodName.Contains('>'))
        {
            return methodName.Split('<')[1].Split('>')[0];
        }

        return "";
    }

    /// <summary>
    /// Gets an existing property or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="entity">The entity containing the property.</param>
    /// <param name="propName">The property name.</param>
    /// <param name="type">The property type.</param>
    /// <returns>The EfProperty object.</returns>
    private static EfProperty GetOrCreateProperty(EfEntity entity, string propName, string type)
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
                        : EfAnalysisConstants.DataTypes.String;
            }

            property = new EfProperty { Name = propName, Type = detectedType };
            entity.Properties.Add(property);
        }
        else if (!string.IsNullOrEmpty(type))
        {
            property.Type = type;
        }

        return property;
    }

    /// <summary>
    /// Applies a specific configuration to a given property based on the provided configuration method and argument.
    /// </summary>
    /// <param name="property">The <see cref="EfProperty"/> object representing the property to configure.</param>
    /// <param name="configMethod">The name of the configuration method to apply (e.g., "IsRequired", "HasMaxLength").</param>
    /// <param name="configArg">The argument for the configuration method, if applicable (e.g., max length, precision).</param>
    /// <remarks>
    /// This method applies various property configurations based on the provided method name:
    /// - "IsRequired": Marks the property as required.
    /// - "HasMaxLength": Sets the maximum length of the property if the argument is a valid integer.
    /// - "HasPrecision": Configures the precision and scale of the property using the <see cref="ApplyPrecisionConfiguration"/> method.
    /// - "HasDefaultValue": Sets the default value of the property using the provided argument.
    /// </remarks>
    private static void ApplyPropertyConfiguration(EfProperty property, string configMethod, string configArg)
    {
        var configActions = new Dictionary<string, Action<EfProperty, string>>
        {
            [EfAnalysisConstants.EfMethods.IsRequired] = ApplyIsRequiredConfiguration,
            [EfAnalysisConstants.EfMethods.HasMaxLength] = ApplyMaxLengthConfiguration,
            [EfAnalysisConstants.EfMethods.HasPrecision] = ApplyPrecisionConfiguration,
            [EfAnalysisConstants.EfMethods.HasColumnType] = ApplyColumnTypeConfiguration,
            [EfAnalysisConstants.EfMethods.HasDefaultValue] = ApplyDefaultValueConfiguration,
            [EfAnalysisConstants.EfMethods.HasDefaultValueSql] = ApplyDefaultValueSqlConfiguration
        };

        if (configActions.TryGetValue(configMethod, out var action))
        {
            action(property, configArg);
        }
    }

    /// <summary>
    /// Configures the IsRequired property based on the configuration argument.
    /// </summary>
    /// <param name="property">The <see cref="EfProperty"/> object representing the property to configure.</param>
    /// <param name="configArg">The configuration argument.</param>
    private static void ApplyIsRequiredConfiguration(EfProperty property, string configArg)
    {
        property.IsRequired = string.IsNullOrEmpty(configArg) ||
                              configArg.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Configures the MaxLength property based on the configuration argument.
    /// </summary>
    /// <param name="property">The <see cref="EfProperty"/> object representing the property to configure.</param>
    /// <param name="configArg">The configuration argument containing the max length value.</param>
    private static void ApplyMaxLengthConfiguration(EfProperty property, string configArg)
    {
        if (int.TryParse(configArg, out var maxLen))
        {
            property.MaxLength = maxLen;
        }
    }

    /// <summary>
    /// Configures the DefaultValue property based on the configuration argument.
    /// </summary>
    /// <param name="property">The <see cref="EfProperty"/> object representing the property to configure.</param>
    /// <param name="configArg">The configuration argument containing the default value.</param>
    private static void ApplyDefaultValueConfiguration(EfProperty property, string configArg)
    {
        property.DefaultValue = ParseDefaultValue(configArg);
    }

    /// <summary>
    /// Configures the DefaultValue property from SQL based on the configuration argument.
    /// </summary>
    /// <param name="property">The <see cref="EfProperty"/> object representing the property to configure.</param>
    /// <param name="configArg">The configuration argument containing the SQL default value.</param>
    private static void ApplyDefaultValueSqlConfiguration(EfProperty property, string configArg)
    {
        property.DefaultValue = configArg.Trim('\"', '\'', ' ');
    }

    /// <summary>
    /// Configures the column type for a property, inferring max length from column type definition if needed.
    /// </summary>
    /// <param name="property">The <see cref="EfProperty"/> object representing the property to configure.</param>
    /// <param name="configArg">The column type argument (e.g., "nvarchar(30)").</param>
    /// <remarks>
    /// If the property doesn't have a max length set and the column type contains a length specification
    /// in parentheses (e.g., "nvarchar(30)"), this method will extract and set the max length.
    /// </remarks>
    private static void ApplyColumnTypeConfiguration(EfProperty property, string configArg)
    {
        // If it's something like "nvarchar(30)", we can infer max length if not already set
        if (property.MaxLength is not null)
        {
            return;
        }

        var match = EfAnalysisRegexPatterns.NumberInParensRegex().Match(configArg);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var len))
        {
            property.MaxLength = len;
        }
    }

    /// <summary>
    /// Parses a default value argument and returns a simplified string representation.
    /// </summary>
    /// <param name="configArg">The configuration argument containing the default value.</param>
    /// <returns>
    /// A string representing the default value, with quotes removed and qualified names shortened
    /// to their simple name when appropriate.
    /// </returns>
    /// <remarks>
    /// This method handles quoted strings and attempts to simplify fully qualified names
    /// (e.g., "MyNamespace.MyEnum.Value" becomes "Value") unless the value appears to be numeric.
    /// </remarks>
    private static string ParseDefaultValue(string configArg)
    {
        var trimmedArg = configArg.Trim();
        var isQuoted = (trimmedArg.StartsWith('\"') && trimmedArg.EndsWith('\"')) ||
                       (trimmedArg.StartsWith('\'') && trimmedArg.EndsWith('\''));
        var val = trimmedArg.Trim('\"', '\'');

        if (isQuoted || !val.Contains('.'))
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
    /// Configures the precision and scale of a given property based on the provided configuration argument.
    /// </summary>
    /// <param name="property">The <see cref="EfProperty"/> object representing the property to configure.</param>
    /// <param name="configArg">
    /// A comma-separated string containing precision and optionally scale values. 
    /// The first value represents the precision, and the second value (if present) represents the scale.
    /// </param>
    /// <remarks>
    /// This method parses the <paramref name="configArg"/> string to extract precision and scale values. 
    /// If the precision value is valid, it is assigned to the <see cref="EfProperty.Precision"/> property. 
    /// If a scale value is also provided and valid, it is assigned to the <see cref="EfProperty.Scale"/> property.
    /// </remarks>
    private static void ApplyPrecisionConfiguration(EfProperty property, string configArg)
    {
        var precisionArgs = configArg.Split(',');
        if (precisionArgs.Length < 1 || !int.TryParse(precisionArgs[0].Trim(), out var precision))
        {
            return;
        }

        property.Precision = precision;
        if (precisionArgs.Length >= 2 && int.TryParse(precisionArgs[1].Trim(), out var scale))
        {
            property.Scale = scale;
        }
    }
}