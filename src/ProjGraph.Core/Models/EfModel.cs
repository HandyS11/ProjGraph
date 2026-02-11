using System.Collections.ObjectModel;

namespace ProjGraph.Core.Models;

/// <summary>
/// Represents an Entity Framework model, containing the context name, entities, and relationships.
/// </summary>
public class EfModel
{
    /// <summary>
    /// Gets or sets the name of the Entity Framework context.
    /// </summary>
    public string ContextName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of entities in the model.
    /// </summary>
    public Collection<EfEntity> Entities { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of relationships between entities in the model.
    /// </summary>
    public Collection<EfRelationship> Relationships { get; set; } = [];
}

/// <summary>
/// Represents an entity in the Entity Framework model.
/// </summary>
public class EfEntity
{
    /// <summary>
    /// Gets or sets the name of the entity.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of properties associated with the entity.
    /// </summary>
    public Collection<EfProperty> Properties { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the entity is a join entity.
    /// </summary>
    public bool IsJoinEntity { get; set; }

    /// <summary>
    /// Gets or sets the name of the database table associated with the entity.
    /// </summary>
    public string TableName { get; set; } = string.Empty;
}

/// <summary>
/// Represents a property of an entity in the Entity Framework model.
/// </summary>
public class EfProperty
{
    /// <summary>
    /// Gets or sets the name of the property.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data type of the property.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the property is a primary key.
    /// </summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the property is a foreign key.
    /// </summary>
    public bool IsForeignKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the property is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the property is a value type.
    /// </summary>
    public bool IsValueType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the property was explicitly marked as required.
    /// </summary>
    public bool IsExplicitlyRequired { get; set; }

    /// <summary>
    /// Gets or sets the maximum length of the property value, if applicable.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets the precision of the property value, if applicable.
    /// </summary>
    public int? Precision { get; set; }

    /// <summary>
    /// Gets or sets the scale of the property value, if applicable.
    /// </summary>
    public int? Scale { get; set; }

    /// <summary>
    /// Gets or sets the default value of the property, if any.
    /// </summary>
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Represents a relationship between two entities in the Entity Framework model.
/// </summary>
public class EfRelationship
{
    /// <summary>
    /// Gets or sets the name of the source entity in the relationship.
    /// </summary>
    public string SourceEntity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the target entity in the relationship.
    /// </summary>
    public string TargetEntity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the relationship (e.g., One-to-One, One-to-Many, Many-to-Many).
    /// </summary>
    public EfRelationshipType Type { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the relationship is required.
    /// </summary>
    public bool IsRequired { get; set; }
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
