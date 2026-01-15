namespace ProjGraph.Core.Models;

public class EfModel
{
    public string ContextName { get; set; } = string.Empty;
    public List<EfEntity> Entities { get; set; } = [];
    public List<EfRelationship> Relationships { get; set; } = [];
}

public class EfEntity
{
    public string Name { get; set; } = string.Empty;
    public List<EfProperty> Properties { get; set; } = [];
    public bool IsJoinEntity { get; set; }
}

public class EfProperty
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
}

public class EfRelationship
{
    public string SourceEntity { get; set; } = string.Empty;
    public string TargetEntity { get; set; } = string.Empty;
    public EfRelationshipType Type { get; set; }
    public bool IsRequired { get; set; }
    public string Label { get; set; } = string.Empty;
}

public enum EfRelationshipType
{
    OneToOne,
    OneToMany,
    ManyToMany
}