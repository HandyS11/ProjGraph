using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using ProjGraph.Lib.EntityFramework.Infrastructure.Extensions;
using ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Orchestrates Fluent API configuration parsing by delegating to specialized parsers for
/// relationships (<see cref="RelationshipConfigParser"/>), properties (<see cref="PropertyConfigParser"/>),
/// and default values (<see cref="DefaultValueResolver"/>).
/// </summary>
public static class FluentApiConfigurationParser
{
    /// <summary>
    /// Applies Fluent API constraints to the specified Entity Framework model by parsing the "OnModelCreating" method
    /// of the provided context type and processing each entity configuration section.
    /// </summary>
    /// <param name="contextType">The named type symbol of the DbContext class.</param>
    /// <param name="entities">The dictionary of entities in the model.</param>
    /// <param name="model">The EF model to apply constraints to.</param>
    /// <param name="compilation">The Roslyn compilation for symbol resolution.</param>
    public static void ApplyFluentApiConstraints(
        INamedTypeSymbol contextType,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        var methodSyntax = FindOnModelCreatingMethod(contextType);
        if (methodSyntax is null || (methodSyntax.Body is null && methodSyntax.ExpressionBody is null))
        {
            return;
        }

        // Context path: every concern now flows through the Roslyn syntax walkers. FluentEntityWalker
        // materializes fluent-only entities and applies ToTable; FluentPropertyWalker derives property
        // config + primary keys; FluentRelationshipWalker derives relationships + foreign keys. The regex
        // ApplyConstraintsFromMethod path is used only by the snapshot path now (retired in Slice 6).
        FluentEntityWalker.Apply(methodSyntax, entities, model, compilation);
        FluentPropertyWalker.Apply(methodSyntax, entities, compilation);
        FluentRelationshipWalker.Apply(methodSyntax, entities, model, compilation);

        // Fold IEntityTypeConfiguration<T> classes referenced via ApplyConfiguration /
        // ApplyConfigurationsFromAssembly by walking each Configure(EntityTypeBuilder<T>) body with T as
        // the ambient entity (Slice 4).
        EntityConfigurationWalker.Apply(methodSyntax, entities, model, compilation);
    }

    /// <summary>
    /// Applies Fluent API constraints from a specific method (e.g., OnModelCreating or BuildModel)
    /// to the specified Entity Framework model. Used by the snapshot path; the context path uses the
    /// Roslyn syntax walkers instead.
    /// </summary>
    /// <param name="methodSyntax">The method declaration syntax to parse.</param>
    /// <param name="entities">The dictionary of entities in the model.</param>
    /// <param name="model">The EF model to apply constraints to.</param>
    /// <param name="compilation">The Roslyn compilation for symbol resolution.</param>
    public static void ApplyConstraintsFromMethod(
        MethodDeclarationSyntax methodSyntax,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        // Accept both block-bodied ({ ... }) and expression-bodied (=> ...) methods; ToString()
        // includes the expression body text in either case.
        if (methodSyntax.Body is null && methodSyntax.ExpressionBody is null)
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

    private static MethodDeclarationSyntax? FindOnModelCreatingMethod(INamedTypeSymbol contextType)
    {
        var onModelCreating = contextType.GetMembers(EfAnalysisConstants.EfMethods.OnModelCreating)
            .OfType<IMethodSymbol>()
            .FirstOrDefault();

        var syntaxRef = onModelCreating?.DeclaringSyntaxReferences.FirstOrDefault();
        return syntaxRef?.GetSyntax() as MethodDeclarationSyntax;
    }

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

    private static void AddUniqueRelationships(List<EfRelationship> relationships, EfModel model)
    {
        var existingKeys = model.Relationships.Select(r => r.GenerateKey()).ToHashSet();

        var uniqueRelationships = relationships
            .Where(relationship => existingKeys.Add(relationship.GenerateKey()));

        foreach (var relationship in uniqueRelationships)
        {
            model.Relationships.Add(relationship);
        }
    }

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
        if (entityName.Contains('.', StringComparison.Ordinal))
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

        RelationshipConfigParser.ParseShadowRelationships(configSection, entityName, entities, shadowRelationships);
        RelationshipConfigParser.ParseExplicitRelationships(configSection, entityName, entities, shadowRelationships,
            compilation);
        PropertyConfigParser.ParsePropertyConfigurations(configSection, entity, compilation);

        // Parse table mapping
        var tableMatch = EfAnalysisRegexPatterns.ToTableRegex().Match(configSection);
        if (!tableMatch.Success)
        {
            return shadowRelationships;
        }

        var updatedEntity = new EfEntity
        {
            Name = entity.Name,
            Properties = entity.Properties,
            IsJoinEntity = entity.IsJoinEntity,
            TableName = tableMatch.Groups[1].Value
        };

        // Replace in model entities
        var entityIndex = model.Entities.IndexOf(
            model.Entities.FirstOrDefault(e => e.Name == entity.Name)!);

        if (entityIndex >= 0)
        {
            model.Entities[entityIndex] = updatedEntity;
        }

        entities[entityName] = updatedEntity;

        return shadowRelationships;
    }
}
