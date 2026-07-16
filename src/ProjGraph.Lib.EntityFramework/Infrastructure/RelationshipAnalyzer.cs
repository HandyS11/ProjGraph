using Microsoft.CodeAnalysis;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using ProjGraph.Lib.EntityFramework.Infrastructure.Extensions;

// ReSharper disable InconsistentNaming

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

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
    /// This method iterates through all ROOT entities in the model (owned types are excluded — see the
    /// walked-source filter below), identifies their relationships, and updates the model with the
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

        // No EfRelationship is ever created for an owned type: the renderer derives the owned
        // identifying-relationship line from the owned entities it chooses to draw as boxes, so a
        // fabricated EfRelationship pointing at (or from) an owned entity would be a second,
        // unsynchronised source of truth. Excluding owned entities from the walked "source" set here
        // is what makes that guarantee hold — without it, an owned type's CLR class need only declare
        // a navigation property (entirely legal in EF Core) to have its bare, non-unique Name fabricated
        // into a spurious relationship (see AnalyzeEntityRelationships' target-side note for why the
        // target side cannot reach an owned entity through the dictionary lookup, and is filtered
        // explicitly anyway for defence in depth).
        foreach (var entity in entities.Values.Where(e => !e.IsOwned))
        {
            var symbol = EntityAnalyzer.FindEntitySymbol(entity, compilation);
            if (symbol is null)
            {
                continue;
            }

            AnalyzeEntityRelationships(entity, symbol, entities, model, addedRelationships);
        }

        // Many-to-many relationships are decomposed into an explicit join entity plus two
        // one-to-many edges to it; the original many-to-many edge is removed in the process, so no
        // separate cleanup of "direct" edges between the joined pair is needed (removing them would
        // also drop legitimate co-existing relationships such as a distinct owner reference).
        ConvertManyToManyToJoinTables(model);
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

            // Defence in depth: owned entities are keyed in the dictionary by "{Owner}.{Nav}" (always
            // containing a literal '.', see FluentOwnedTypeWalker.Capture), while targetType.Name is a
            // bare CLR identifier that can never contain '.'. So this TryGetValue can never actually
            // resolve to an owned entity today — but that safety currently rests entirely on the key
            // format staying dot-qualified. Checking IsOwned explicitly here means a future change to
            // that format (or an unforeseen key collision) can't silently reopen the same bug the
            // walked-source filter above closes.
            if (targetEntity.IsOwned)
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
        var (relType, src, tgt) =
            DetermineRelationshipValues(sourceEntity, targetEntity, prop, targetType, isCollection);

        return new EfRelationship
        {
            SourceEntity = src,
            TargetEntity = tgt,
            Type = relType,
            IsRequired = !prop.Type.IsNullable()
        };
    }

    private static void MarkConventionForeignKey(EfEntity entity, string navigationName, string targetEntityName)
    {
        var potentialNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            navigationName + EfAnalysisConstants.Suffixes.IdSuffix,
            targetEntityName + EfAnalysisConstants.Suffixes.IdSuffix
        };

        for (var i = 0; i < entity.Properties.Count; i++)
        {
            if (potentialNames.Contains(entity.Properties[i].Name))
            {
                entity.Properties[i] = EfPropertyFactory.CopyWith(entity.Properties[i], new EfPropertyOverrides
                {
                    IsForeignKey = true
                });
            }
        }
    }

    /// <summary>
    /// Determines the type and entity direction of a relationship based on the navigation property.
    /// </summary>
    /// <param name="sourceEntity">The source <see cref="EfEntity"/> in the relationship.</param>
    /// <param name="targetEntity">The target <see cref="EfEntity"/> in the relationship.</param>
    /// <param name="prop">The <see cref="IPropertySymbol"/> representing the navigation property.</param>
    /// <param name="targetType">The <see cref="INamedTypeSymbol"/> representing the type of the target entity.</param>
    /// <param name="isCollection">A boolean indicating whether the navigation property is a collection.</param>
    /// <returns>
    /// A tuple containing the relationship type, source entity name, and target entity name.
    /// </returns>
    private static (EfRelationshipType Type, string SourceEntity, string TargetEntity) DetermineRelationshipValues(
        EfEntity sourceEntity,
        EfEntity targetEntity,
        IPropertySymbol prop,
        INamedTypeSymbol targetType,
        bool isCollection)
    {
        if (isCollection)
        {
            var type = NavigationPropertyAnalyzer.HasInverseCollection(prop, targetType)
                ? EfRelationshipType.ManyToMany
                : EfRelationshipType.OneToMany;
            return (type, sourceEntity.Name, targetEntity.Name);
        }

        return DetermineReferenceNavigationValues(sourceEntity, targetEntity, prop, targetType);
    }

    /// <summary>
    /// Determines the relationship values for reference navigation properties.
    /// </summary>
    /// <param name="sourceEntity">The source <see cref="EfEntity"/> in the relationship.</param>
    /// <param name="targetEntity">The target <see cref="EfEntity"/> in the relationship.</param>
    /// <param name="prop">The <see cref="IPropertySymbol"/> representing the navigation property.</param>
    /// <param name="targetType">The <see cref="INamedTypeSymbol"/> representing the type of the target entity.</param>
    /// <returns>
    /// A tuple containing the relationship type, source entity name, and target entity name.
    /// For One-to-Many relationships with a reference navigation, source and target are swapped.
    /// </returns>
    private static (EfRelationshipType Type, string SourceEntity, string TargetEntity)
        DetermineReferenceNavigationValues(
            EfEntity sourceEntity,
            EfEntity targetEntity,
            IPropertySymbol prop,
            INamedTypeSymbol targetType)
    {
        if (NavigationPropertyAnalyzer.HasInverseReference(prop, targetType))
        {
            // If it has an inverse reference (including self-references with an explicit inverse), treat as One-to-One
            return (EfRelationshipType.OneToOne, sourceEntity.Name, targetEntity.Name);
        }

        // Target has a collection back, or no inverse navigation found: treat as One-to-Many
        // The foreign key will be on the source entity (the one with the reference navigation)
        return (EfRelationshipType.OneToMany, targetEntity.Name, sourceEntity.Name);
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
                .Order()
                .ToArray();
            var joinTableName = entitiesSorted[0] + entitiesSorted[1];

            // Matching on Name (not EffectiveKey) is intentional and safe here: a ManyToMany
            // EfRelationship can now only be produced by AnalyzeEntityRelationships walking a ROOT
            // entity as both source and target (owned entities are excluded from the walked-source set
            // above, and the target-side dictionary lookup there can never resolve to an owned entity
            // either — see its comment). For root entities EffectiveKey falls back to Name, so
            // m2m.SourceEntity/TargetEntity always hold a root entity's Name here, and this lookup
            // cannot observe an owned entity's "{Owner}.{Nav}" Key.
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
                    Name = m2m.SourceEntity + EfAnalysisConstants.Suffixes.IdSuffix,
                    Type = sourcePkType,
                    IsPrimaryKey = true,
                    IsForeignKey = true,
                    IsValueType = true // ID fields are always value types (int, Guid, etc.)
                },
                new EfProperty
                {
                    Name = m2m.TargetEntity + EfAnalysisConstants.Suffixes.IdSuffix,
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

}
