using System.Collections.ObjectModel;

namespace ProjGraph.Core.Models;

/// <summary>
/// Represents an Entity Framework model, containing the context name, entities, and relationships.
/// </summary>
public class EfModel
{
    /// <summary>
    /// Gets or initializes the name of the Entity Framework context.
    /// </summary>
    public string ContextName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the collection of entities in the model.
    /// </summary>
    public Collection<EfEntity> Entities { get; init; } = [];

    /// <summary>
    /// Gets or initializes the collection of relationships between entities in the model.
    /// </summary>
    public Collection<EfRelationship> Relationships { get; init; } = [];
}

/// <summary>
/// Represents an entity in the Entity Framework model.
/// </summary>
public class EfEntity
{
    /// <summary>
    /// Gets or initializes the name of the entity.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the collection of properties associated with the entity.
    /// </summary>
    public Collection<EfProperty> Properties { get; init; } = [];

    /// <summary>
    /// Gets or initializes a value indicating whether the entity is a join entity.
    /// </summary>
    public bool IsJoinEntity { get; init; }

    /// <summary>
    /// Gets or initializes the name of the database table associated with the entity.
    /// </summary>
    public string TableName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes this entity's identity within the model: <c>{Owner}.{Nav}</c> for an owned
    /// entity, and empty for a root entity (which is identified by its <see cref="Name"/>). Prefer
    /// <see cref="EffectiveKey"/>, which applies that fallback. <see cref="Name"/> holds the CLR type
    /// name and is not unique — two owners may own the same type.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Gets this entity's identity: <see cref="Key"/> when set, otherwise <see cref="Name"/>. Every
    /// owner/owned lookup must match on this rather than on <see cref="Name"/>.
    /// </summary>
    public string EffectiveKey => string.IsNullOrEmpty(Key) ? Name : Key;

    /// <summary>
    /// Gets or initializes a value indicating whether the entity is an EF Core owned type
    /// (configured via <c>OwnsOne</c>/<c>OwnsMany</c>) rather than a root entity.
    /// </summary>
    public bool IsOwned { get; init; }

    /// <summary>
    /// Gets or initializes the <see cref="EffectiveKey"/> of the entity that owns this one, when
    /// <see cref="IsOwned"/> is <see langword="true"/>; otherwise <see langword="null"/>.
    /// </summary>
    public string? OwnerEntity { get; init; }

    /// <summary>
    /// Gets or initializes the owner's navigation property name for this owned type (e.g.
    /// <c>ShipToAddress</c>), when <see cref="IsOwned"/> is <see langword="true"/>; otherwise
    /// <see langword="null"/>. Source of both EF's column prefix and the identifying relationship.
    /// </summary>
    public string? NavigationName { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether this owned type is a collection
    /// (<c>OwnsMany</c>) rather than a reference (<c>OwnsOne</c>).
    /// </summary>
    public bool IsCollection { get; init; }
}

/// <summary>
/// Represents a property of an entity in the Entity Framework model.
/// </summary>
public class EfProperty
{
    /// <summary>
    /// Gets or initializes the name of the property.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the data type of the property.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes a value indicating whether the property is a primary key.
    /// </summary>
    public bool IsPrimaryKey { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the property is a foreign key.
    /// </summary>
    public bool IsForeignKey { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the property is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the property is a value type.
    /// </summary>
    public bool IsValueType { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the property was explicitly marked as required.
    /// </summary>
    public bool IsExplicitlyRequired { get; init; }

    /// <summary>
    /// Gets or initializes the maximum length of the property value, if applicable.
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Gets or initializes the precision of the property value, if applicable.
    /// </summary>
    public int? Precision { get; init; }

    /// <summary>
    /// Gets or initializes the scale of the property value, if applicable.
    /// </summary>
    public int? Scale { get; init; }

    /// <summary>
    /// Gets or initializes the default value of the property, if any.
    /// </summary>
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Represents a relationship between two entities in the Entity Framework model.
/// </summary>
public class EfRelationship
{
    /// <summary>
    /// Gets or initializes the name of the source entity in the relationship.
    /// </summary>
    public string SourceEntity { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the name of the target entity in the relationship.
    /// </summary>
    public string TargetEntity { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the type of the relationship (e.g., One-to-One, One-to-Many, Many-to-Many).
    /// </summary>
    public EfRelationshipType Type { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the relationship is required.
    /// </summary>
    public bool IsRequired { get; init; }
}

/// <summary>
/// Represents the type of relationship between two entities in the Entity Framework model.
/// </summary>
public enum EfRelationshipType
{
    /// <summary>
    /// A one-to-one relationship between two entities.
    /// </summary>
    OneToOne = 0,

    /// <summary>
    /// A one-to-many relationship between two entities.
    /// </summary>
    OneToMany = 1,

    /// <summary>
    /// A many-to-many relationship between two entities.
    /// </summary>
    ManyToMany = 2
}
