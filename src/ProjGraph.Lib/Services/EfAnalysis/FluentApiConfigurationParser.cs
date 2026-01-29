using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Services.EfAnalysis.Extensions;
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
public static partial class FluentApiConfigurationParser
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

        var methodText = methodSyntax.ToString();
        var entityConfigSections = EntitySplitRegex().Split(methodText);

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
        var onModelCreating = contextType.GetMembers("OnModelCreating")
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
        var section = ".Entity<" + sectionContent;

        // Extract just this entity's configuration (up to the next .Entity<)
        var entityConfigEnd = section.IndexOf(".Entity<", 10, StringComparison.Ordinal);
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
    /// with the same source entity, target entity, and type already exists in the model's relationships collection.
    /// </remarks>
    private static void AddUniqueRelationships(List<EfRelationship> relationships, EfModel model)
    {
        foreach (var relationship in from relationship in relationships
                 let alreadyExists = model.Relationships.Any(r =>
                     r.SourceEntity == relationship.SourceEntity &&
                     r.TargetEntity == relationship.TargetEntity &&
                     r.Type == relationship.Type)
                 where !alreadyExists
                 select relationship)
        {
            model.Relationships.Add(relationship);
        }
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

        var entityMatch = EntityNameRegex().Match(configSection);
        if (!entityMatch.Success)
        {
            return shadowRelationships;
        }

        var entityName = entityMatch.Groups[1].Value;
        if (!entities.TryGetValue(entityName, out var entity))
        {
            var symbol = compilation.GlobalNamespace.GetAllNamedTypes()
                .FirstOrDefault(t => t.Name == entityName);

            if (symbol == null)
            {
                return shadowRelationships;
            }

            entity = EntityAnalyzer.AnalyzeEntity(symbol);
            entities[entityName] = entity;
            model.Entities.Add(entity);
        }

        ParseShadowRelationships(configSection, entityName, entities, shadowRelationships);
        ParsePropertyConfigurations(configSection, entity);

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
        var shadowMatches = ShadowRelationshipRegex().Matches(configSection);

        foreach (Match shadowMatch in shadowMatches)
        {
            if (IsInsideUsingEntityBlock(configSection, shadowMatch.Index))
            {
                continue;
            }

            var hasMethod = shadowMatch.Groups[1].Value;
            var targetEntityName = shadowMatch.Groups[2].Value;
            var withMethod = shadowMatch.Groups[3].Value;

            if (entities.ContainsKey(targetEntityName))
            {
                var rel = CreateShadowRelationship(entityName, targetEntityName, hasMethod, withMethod);
                shadowRelationships.Add(rel);
            }
        }
    }

    private static EfRelationship CreateShadowRelationship(string sourceEntity, string targetEntity, string hasMethod,
        string withMethod)
    {
        return (hasMethod, withMethod) switch
        {
            ("HasOne", "WithMany") => new EfRelationship
            {
                SourceEntity = targetEntity,
                TargetEntity = sourceEntity,
                Type = EfRelationshipType.OneToMany,
                Label = "",
                IsRequired = false
            },
            ("HasMany", "WithOne") => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.OneToMany,
                Label = "",
                IsRequired = false
            },
            ("HasOne", "WithOne") => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.OneToOne,
                Label = "",
                IsRequired = false
            },
            ("HasMany", "WithMany") => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.ManyToMany,
                Label = "",
                IsRequired = false
            },
            _ => new EfRelationship
            {
                SourceEntity = targetEntity,
                TargetEntity = sourceEntity,
                Type = EfRelationshipType.OneToMany,
                Label = "",
                IsRequired = false
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
        var lastUsingEntity = textBeforeMatch.LastIndexOf("UsingEntity", StringComparison.Ordinal);

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
    /// It identifies the property name using a lambda expression and then parses all subsequent method calls
    /// (e.g., IsRequired, HasMaxLength) to apply the corresponding configurations to the entity's property.
    /// </remarks>
    private static void ParsePropertyConfigurations(string configSection, EfEntity entity)
    {
        var propertyParts = configSection.Split(".Property", StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in propertyParts)
        {
            var match = PropertyLambdaRegex().Match(part);
            if (!match.Success)
            {
                continue;
            }

            var propName = match.Groups[2].Value;
            var property = entity.Properties.FirstOrDefault(p => p.Name == propName);
            if (property == null)
            {
                continue;
            }

            var methods = MethodCallRegex().Matches(part)
                .Select(method => method.Groups);

            foreach (var groups in methods)
            {
                var configMethod = groups[1].Value;
                var configArg = groups[2].Value;
                ApplyPropertyConfiguration(property, configMethod, configArg);
            }
        }
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
        switch (configMethod)
        {
            case "IsRequired":
                property.IsRequired = true;
                break;

            case "HasMaxLength":
                if (int.TryParse(configArg, out var maxLen))
                {
                    property.MaxLength = maxLen;
                }

                break;

            case "HasPrecision":
                ApplyPrecisionConfiguration(property, configArg);
                break;

            case "HasDefaultValue":
                property.DefaultValue = configArg.Trim('"', '\'');
                break;
        }
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

    /// <summary>
    /// A regex pattern to match entity type names in the format "Entity&lt;TypeName&gt;".
    /// </summary>
    /// <returns>A compiled <see cref="Regex"/> instance for matching entity type names.</returns>
    [GeneratedRegex(@"Entity<(\w+)>")]
    private static partial Regex EntityNameRegex();

    /// <summary>
    /// A regex pattern to split a string by occurrences of ".Entity&lt;".
    /// </summary>
    /// <returns>A compiled <see cref="Regex"/> instance for splitting strings by ".Entity&lt;".</returns>
    [GeneratedRegex(@"\.Entity<")]
    private static partial Regex EntitySplitRegex();

    /// <summary>
    /// A regex pattern to match shadow relationships in the format "(HasOne|HasMany)&lt;TypeName&gt;().(WithOne|WithMany)()".
    /// </summary>
    /// <returns>A compiled <see cref="Regex"/> instance for matching shadow relationships.</returns>
    [GeneratedRegex(@"(HasOne|HasMany)<(\w+)>\(\s*\)\s*\.(WithOne|WithMany)\(\s*\)")]
    private static partial Regex ShadowRelationshipRegex();

    /// <summary>
    /// A regex pattern to match property lambda expressions in the format "(e => e.PropertyName)".
    /// </summary>
    /// <returns>A compiled <see cref="Regex"/> instance for matching property lambda expressions.</returns>
    [GeneratedRegex(@"^\s*\(\s*(\w+)\s*=>\s*\1\.(\w+)\s*\)")]
    private static partial Regex PropertyLambdaRegex();

    /// <summary>
    /// A regex pattern to match fluent method calls in the format ".MethodName(arguments)".
    /// </summary>
    /// <returns>A compiled <see cref="Regex"/> instance for matching fluent method calls.</returns>
    [GeneratedRegex(@"\.(\w+)\(([^)]*)\)")]
    private static partial Regex MethodCallRegex();
}