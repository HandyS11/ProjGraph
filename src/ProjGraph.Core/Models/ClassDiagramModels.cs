namespace ProjGraph.Core.Models;

/// <summary>
/// Represents the type of a C# entity in a class diagram.
/// </summary>
public enum TypeKind
{
    Class = 0,
    Interface = 1,
    Struct = 2,
    Enum = 3
}

/// <summary>
/// Represents the visibility of a member.
/// </summary>
public enum Visibility
{
    Public = 0,
    Protected = 1,
    Internal = 2,
    Private = 3
}

/// <summary>
/// Represents the kind of member.
/// </summary>
public enum MemberKind
{
    Field,
    Property,
    Method
}

/// <summary>
/// Represents the kind of relationship between two types.
/// </summary>
public enum RelationshipKind
{
    Inheritance,
    Realization,
    Association,
    Dependency
}

/// <summary>
/// Represents a type definition in a class diagram.
/// </summary>
public record TypeDefinition(
    string Name,
    string Namespace,
    string FullName,
    TypeKind Kind,
    List<MemberDefinition> Members,
    bool IsAbstract = false
);

/// <summary>
/// Represents a member definition within a type.
/// </summary>
public record MemberDefinition(
    string Name,
    string Type,
    Visibility Visibility,
    MemberKind Kind,
    List<ParameterDefinition>? Parameters = null
);

/// <summary>
/// Represents a parameter definition for a method.
/// </summary>
public record ParameterDefinition(
    string Name,
    string Type
);

/// <summary>
/// Represents a relationship between two types.
/// </summary>
public record Relationship(
    string From,
    string To,
    RelationshipKind Kind,
    string? Label = null,
    string? Cardinality = null
);

/// <summary>
/// Root model for a class diagram.
/// </summary>
public record ClassModel(
    string? Title,
    List<TypeDefinition> Types,
    List<Relationship> Relationships
);