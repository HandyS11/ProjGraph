using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Services.EfAnalysis.Constants;
using ProjGraph.Lib.Services.EfAnalysis.Extensions;

namespace ProjGraph.Lib.Services.EfAnalysis;

/// <summary>
/// Provides methods for analyzing and managing relationships between entities in an Entity Framework model.
/// </summary>
/// <remarks>
/// The <see cref="RelationshipAnalyzer"/> class contains static methods to analyze entity relationships,
/// create relationships, determine relationship types, and handle many-to-many relationships by converting
/// them into join tables. It is designed to work with the Entity Framework model and Roslyn's code analysis APIs.
/// </remarks>
public static class RelationshipAnalyzer
{
    /// <summary>
    /// Analyzes the relationships between entities in the provided entity framework model and updates the model accordingly.
    /// </summary>
    /// <param name="model">The <see cref="EfModel"/> representing the entity framework model to be analyzed and updated.</param>
    /// <param name="entities">A dictionary containing all entities in the model, keyed by their names.</param>
    /// <param name="compilation">The <see cref="Compilation"/> object used to analyze the entity symbols.</param>
    /// <remarks>
    /// This method iterates through all entities in the model, identifies their relationships, and updates the model with the
    /// discovered relationships. It ensures that duplicate relationships are not added by maintaining a set of added relationship keys.
    /// Additionally, it converts any many-to-many relationships into join tables.
    /// </remarks>
    public static void AnalyzeRelationships(
        EfModel model,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        // Initialize with existing relationships to prevent duplicates
        var addedRelationships = model.Relationships
            .Select(r => r.GenerateKey())
            .ToHashSet();

        foreach (var entity in entities.Values)
        {
            var symbol = EntityAnalyzer.FindEntitySymbol(entity, compilation);
            if (symbol is null)
            {
                continue;
            }

            AnalyzeEntityRelationships(entity, symbol, entities, model, addedRelationships);
        }

        ConvertManyToManyToJoinTables(model);

        // Remove direct relationships when join tables exist
        RemoveDirectRelationshipsWithJoinTables(model);
    }

    /// <summary>
    /// Analyzes the relationships of a given entity and updates the entity framework model with the identified relationships.
    /// </summary>
    /// <param name="entity">The <see cref="EfEntity"/> representing the source entity being analyzed.</param>
    /// <param name="symbol">The <see cref="INamedTypeSymbol"/> representing the source entity's symbol.</param>
    /// <param name="entities">A dictionary containing all entities in the model, keyed by their names.</param>
    /// <param name="model">The <see cref="EfModel"/> representing the entity framework model.</param>
    /// <param name="addedRelationships">A <see cref="HashSet{T}"/> containing the keys of already added relationships to avoid duplicates.</param>
    /// <remarks>
    /// This method iterates through the properties of the given entity's symbol to identify navigation properties.
    /// For each navigation property, it determines the target entity and relationship type, creates a new relationship,
    /// generates a unique key for the relationship, and adds it to the model if it has not been added already.
    /// </remarks>
    private static void AnalyzeEntityRelationships(
        EfEntity entity,
        INamedTypeSymbol symbol,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        HashSet<string> addedRelationships)
    {
        foreach (var prop in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (!NavigationPropertyAnalyzer.IsNavigationProperty(prop, out var targetType, out var isCollection))
            {
                continue;
            }

            if (targetType is null || !entities.TryGetValue(targetType.Name, out var targetEntity))
            {
                continue;
            }

            // Mark potential foreign key properties by convention
            if (!isCollection)
            {
                MarkConventionForeignKey(entity, prop.Name, targetType.Name);
            }
            else
            {
                // For collections, the foreign key is typically on the target entity 
                // and follows the pattern [SourceEntity]Id
                MarkConventionForeignKey(targetEntity, symbol.Name, symbol.Name);
            }

            var relationship = CreateRelationship(entity, targetEntity, prop, targetType, isCollection);
            var relationshipKey = relationship.GenerateKey();

            if (addedRelationships.Add(relationshipKey))
            {
                model.Relationships.Add(relationship);
            }
        }
    }

    /// <summary>
    /// Creates a new entity framework relationship between a source entity and a target entity based on the provided navigation property.
    /// </summary>
    /// <param name="sourceEntity">The source <see cref="EfEntity"/> in the relationship.</param>
    /// <param name="targetEntity">The target <see cref="EfEntity"/> in the relationship.</param>
    /// <param name="prop">The <see cref="IPropertySymbol"/> representing the navigation property.</param>
    /// <param name="targetType">The <see cref="INamedTypeSymbol"/> representing the type of the target entity.</param>
    /// <param name="isCollection">A boolean indicating whether the navigation property is a collection.</param>
    /// <returns>
    /// An <see cref="EfRelationship"/> object representing the relationship between the source and target entities.
    /// </returns>
    /// <remarks>
    /// This method initializes a new <see cref="EfRelationship"/> object with default values, such as the source entity name,
    /// target entity name and relationship type. It also determines the specific type of the relationship
    /// (e.g., One-to-One, One-to-Many, Many-to-Many) by delegating to the <see cref="DetermineRelationshipType"/> method.
    /// </remarks>
    private static EfRelationship CreateRelationship(
        EfEntity sourceEntity,
        EfEntity targetEntity,
        IPropertySymbol prop,
        INamedTypeSymbol targetType,
        bool isCollection)
    {
        var relationship = new EfRelationship
        {
            SourceEntity = sourceEntity.Name,
            TargetEntity = targetEntity.Name,
            Type = EfRelationshipType.OneToOne,
            IsRequired = !prop.Type.IsNullable()
        };

        DetermineRelationshipType(relationship, sourceEntity, targetEntity, prop, targetType, isCollection);

        return relationship;
    }

    private static void MarkConventionForeignKey(EfEntity entity, string navigationName, string targetEntityName)
    {
        var potentialNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"{navigationName}{EfAnalysisConstants.Suffixes.IdSuffix}",
            $"{targetEntityName}{EfAnalysisConstants.Suffixes.IdSuffix}"
        };

        foreach (var prop in entity.Properties.Where(prop => potentialNames.Contains(prop.Name)))
        {
            prop.IsForeignKey = true;
        }
    }

    /// <summary>
    /// Determines the type of relationship based on whether it is a collection or a reference.
    /// </summary>
    /// <param name="relationship">The <see cref="EfRelationship"/> object representing the relationship being analyzed.</param>
    /// <param name="sourceEntity">The source <see cref="EfEntity"/> in the relationship.</param>
    /// <param name="targetEntity">The target <see cref="EfEntity"/> in the relationship.</param>
    /// <param name="prop">The <see cref="IPropertySymbol"/> representing the navigation property.</param>
    /// <param name="targetType">The <see cref="INamedTypeSymbol"/> representing the type of the target entity.</param>
    /// <param name="isCollection">A boolean indicating whether the navigation property is a collection.</param>
    /// <remarks>
    /// If the navigation property is a collection, the method delegates to <see cref="HandleCollectionNavigation"/> to determine
    /// the relationship type. Otherwise, it delegates to <see cref="HandleReferenceNavigation"/> for further analysis.
    /// </remarks>
    private static void DetermineRelationshipType(
        EfRelationship relationship,
        EfEntity sourceEntity,
        EfEntity targetEntity,
        IPropertySymbol prop,
        INamedTypeSymbol targetType,
        bool isCollection)
    {
        if (isCollection)
        {
            HandleCollectionNavigation(relationship, targetType, prop);
        }
        else
        {
            HandleReferenceNavigation(relationship, sourceEntity, targetEntity, prop, targetType);
        }
    }

    /// <summary>
    /// Determines the type of collection navigation property in an entity relationship.
    /// </summary>
    /// <param name="relationship">The <see cref="EfRelationship"/> object representing the relationship being analyzed.</param>
    /// <param name="targetType">The <see cref="INamedTypeSymbol"/> representing the type of the target entity.</param>
    /// <param name="prop">The <see cref="IPropertySymbol"/> representing the navigation property.</param>
    /// <remarks>
    /// This method checks if the navigation property has an inverse collection. If it does, the relationship type is set to
    /// <see cref="EfRelationshipType.ManyToMany"/>. Otherwise, it is set to <see cref="EfRelationshipType.OneToMany"/>.
    /// </remarks>
    private static void HandleCollectionNavigation(
        EfRelationship relationship,
        INamedTypeSymbol targetType,
        IPropertySymbol prop)
    {
        relationship.Type = NavigationPropertyAnalyzer.HasInverseCollection(prop, targetType)
            ? EfRelationshipType.ManyToMany
            : EfRelationshipType.OneToMany;
    }

    /// <summary>
    /// Handles the navigation logic for reference properties in an entity relationship.
    /// </summary>
    /// <param name="relationship">The <see cref="EfRelationship"/> object representing the relationship being analyzed.</param>
    /// <param name="sourceEntity">The source <see cref="EfEntity"/> in the relationship.</param>
    /// <param name="targetEntity">The target <see cref="EfEntity"/> in the relationship.</param>
    /// <param name="prop">The <see cref="IPropertySymbol"/> representing the navigation property.</param>
    /// <param name="targetType">The <see cref="INamedTypeSymbol"/> representing the type of the target entity.</param>
    /// <remarks>
    /// This method determines the type of relationship (e.g., One-to-One, One-to-Many) based on the navigation property
    /// and its inverse. It updates the relationship object with the appropriate type, source, and target.
    /// </remarks>
    private static void HandleReferenceNavigation(
        EfRelationship relationship,
        EfEntity sourceEntity,
        EfEntity targetEntity,
        IPropertySymbol prop,
        INamedTypeSymbol targetType)
    {
        if (NavigationPropertyAnalyzer.HasInverseReference(prop, targetType))
        {
            // If it has an inverse reference (including self-references with an explicit inverse), treat as One-to-One
            relationship.Type = EfRelationshipType.OneToOne;
        }
        else
        {
            // Target has a collection back, or no inverse navigation found: treat as One-to-Many
            // The foreign key will be on the source entity (the one with the reference navigation)
            relationship.Type = EfRelationshipType.OneToMany;
            relationship.SourceEntity = targetEntity.Name;
            relationship.TargetEntity = sourceEntity.Name;
        }
    }

    /// <summary>
    /// Converts many-to-many relationships in the entity framework model into join tables.
    /// </summary>
    /// <param name="model">The <see cref="EfModel"/> representing the entity framework model.</param>
    /// <remarks>
    /// This method identifies all many-to-many relationships in the model, removes them, and replaces them with
    /// join tables. For each many-to-many relationship, a new join table is created, added to the model, and
    /// two one-to-many relationships are established between the join table and the source/target entities.
    /// </remarks>
    private static void ConvertManyToManyToJoinTables(EfModel model)
    {
        var manyToManyRelationships = model.Relationships
            .Where(r => r.Type is EfRelationshipType.ManyToMany)
            .ToList();

        foreach (var m2m in manyToManyRelationships)
        {
            model.Relationships.Remove(m2m);

            var entitiesSorted = new[] { m2m.SourceEntity, m2m.TargetEntity }
                .OrderBy(e => e)
                .ToArray();
            var joinTableName = $"{entitiesSorted[0]}{entitiesSorted[1]}";

            var sourceEntity = model.Entities.FirstOrDefault(e => e.Name == m2m.SourceEntity);
            var targetEntity = model.Entities.FirstOrDefault(e => e.Name == m2m.TargetEntity);

            var sourcePkType = GetPrimaryKeyType(sourceEntity);
            var targetPkType = GetPrimaryKeyType(targetEntity);

            var joinEntity = CreateJoinEntity(joinTableName, m2m, sourcePkType, targetPkType);
            model.Entities.Add(joinEntity);

            AddJoinTableRelationships(model, joinTableName, m2m);
        }
    }

    private static string GetPrimaryKeyType(EfEntity? entity)
    {
        if (entity is null)
        {
            return EfAnalysisConstants.DataTypes.Int;
        }

        var pk = entity.Properties.FirstOrDefault(p => p.IsPrimaryKey);
        return pk?.Type ?? EfAnalysisConstants.DataTypes.Int;
    }

    /// <summary>
    /// Creates a join entity for a many-to-many relationship in the entity framework model.
    /// </summary>
    /// <param name="joinTableName">The name of the join table to be created.</param>
    /// <param name="m2m">The <see cref="EfRelationship"/> representing the many-to-many relationship.</param>
    /// <param name="sourcePkType">The type of the primary key for the source entity.</param>
    /// <param name="targetPkType">The type of the primary key for the target entity.</param>
    /// <returns>
    /// An <see cref="EfEntity"/> representing the join table with properties for the source and target entity IDs.
    /// </returns>
    /// <remarks>
    /// The created join entity includes two properties: one for the source entity ID and one for the target entity ID.
    /// Both properties are marked as primary keys and foreign keys.
    /// </remarks>
    private static EfEntity CreateJoinEntity(string joinTableName, EfRelationship m2m, string sourcePkType,
        string targetPkType)
    {
        return new EfEntity
        {
            Name = joinTableName,
            IsJoinEntity = true,
            Properties =
            [
                new EfProperty
                {
                    Name = $"{m2m.SourceEntity}{EfAnalysisConstants.Suffixes.IdSuffix}",
                    Type = sourcePkType,
                    IsPrimaryKey = true,
                    IsForeignKey = true,
                    IsValueType = true // ID fields are always value types (int, Guid, etc.)
                },
                new EfProperty
                {
                    Name = $"{m2m.TargetEntity}{EfAnalysisConstants.Suffixes.IdSuffix}",
                    Type = targetPkType,
                    IsPrimaryKey = true,
                    IsForeignKey = true,
                    IsValueType = true
                }
            ]
        };
    }

    /// <summary>
    /// Adds relationships between a join table and the source and target entities in the entity framework model.
    /// </summary>
    /// <param name="model">The <see cref="EfModel"/> representing the entity framework model.</param>
    /// <param name="joinTableName">The name of the join table to be added to the relationships.</param>
    /// <param name="m2m">The <see cref="EfRelationship"/> representing the many-to-many relationship to be converted.</param>
    /// <remarks>
    /// This method creates two one-to-many relationships between the join table and the source/target entities
    /// of the provided many-to-many relationship. These relationships are then added to the model.
    /// </remarks>
    private static void AddJoinTableRelationships(EfModel model, string joinTableName, EfRelationship m2m)
    {
        model.Relationships.Add(new EfRelationship
        {
            SourceEntity = m2m.SourceEntity,
            TargetEntity = joinTableName,
            Type = EfRelationshipType.OneToMany,
            IsRequired = true
        });

        model.Relationships.Add(new EfRelationship
        {
            SourceEntity = m2m.TargetEntity,
            TargetEntity = joinTableName,
            Type = EfRelationshipType.OneToMany,
            IsRequired = true
        });
    }

    /// <summary>
    /// Removes direct relationships between entities that are already connected through join tables.
    /// </summary>
    /// <param name="model">The <see cref="EfModel"/> representing the entity framework model.</param>
    /// <remarks>
    /// This method identifies join tables in the model, determines the entities they connect, and removes any direct relationships
    /// between those entities. A join table is identified as an entity with exactly two foreign key properties, which are also primary keys.
    /// </remarks>
    private static void RemoveDirectRelationshipsWithJoinTables(EfModel model)
    {
        var joinTables = model.Entities.Where(e => e.IsJoinEntity || IsJoinTable(e)).ToList();

        var relationshipsToRemove = joinTables
            .Select(joinTable => joinTable.Properties.Where(p => p.IsForeignKey).ToList())
            .Where(fkProperties => fkProperties.Count == 2)
            .Select(fkProperties => fkProperties
                .Select(fk =>
                    fk.Name.EndsWith(EfAnalysisConstants.Suffixes.IdSuffix, StringComparison.OrdinalIgnoreCase)
                        ? fk.Name[..^2]
                        : null)
                .Where(name => name != null)
                .ToList())
            .Where(entityNames => entityNames.Count == 2)
            .Select(entityNames =>
            {
                var entity1 = entityNames[0]!;
                var entity2 = entityNames[1]!;
                return model.Relationships.Where(r =>
                    (r.SourceEntity == entity1 && r.TargetEntity == entity2) ||
                    (r.SourceEntity == entity2 && r.TargetEntity == entity1));
            })
            .SelectMany(relationships => relationships)
            .Distinct()
            .ToList();

        // Remove the direct relationships
        foreach (var rel in relationshipsToRemove)
        {
            model.Relationships.Remove(rel);
        }
    }

    /// <summary>
    /// Determines if the given entity is a join table.
    /// </summary>
    /// <param name="entity">The <see cref="EfEntity"/> to evaluate.</param>
    /// <returns>
    /// A boolean value indicating whether the entity is a join table.
    /// A join table is defined as an entity that has exactly two foreign key properties,
    /// which are also primary keys.
    /// </returns>
    private static bool IsJoinTable(EfEntity entity)
    {
        var fkProperties = entity.Properties.Where(p => p.IsForeignKey).ToList();
        var pkProperties = entity.Properties.Where(p => p.IsPrimaryKey).ToList();

        // A join table should have exactly 2 FKs that are also PKs
        return fkProperties.Count == 2 &&
               pkProperties.Count == 2 &&
               fkProperties.All(fk => fk.IsPrimaryKey);
    }
}