namespace ProjGraph.Core.Models;

/// <summary>
/// Represents the type of a C# entity in a class diagram.
/// </summary>
public enum TypeKind
{
    Class = 0,
    Interface = 1,
    Struct = 2,
    Enum = 3,
    Record = 4
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
    Field = 0,
    Property = 1,
    Method = 2
}

/// <summary>
/// Represents the kind of relationship between two types.
/// </summary>
public enum RelationshipKind
{
    Inheritance = 0,
    Realization = 1,
    Association = 2,
    Dependency = 3
}

/// <summary>
/// Represents a type definition in a class diagram.
/// </summary>
/// <param name="Name">The name of the type.</param>
/// <param name="Namespace">The namespace the type belongs to.</param>
/// <param name="FullName">The fully qualified name of the type.</param>
/// <param name="Kind">The kind of type (class, interface, struct, etc.).</param>
/// <param name="Members">The members defined on this type.</param>
/// <param name="IsAbstract">Whether the type is abstract.</param>
public record TypeDefinition(
    string Name,
    string Namespace,
    string FullName,
    TypeKind Kind,
    IReadOnlyList<MemberDefinition> Members,
    bool IsAbstract = false
);

/// <summary>
/// Represents a member definition within a type.
/// </summary>
/// <param name="Name">The name of the member.</param>
/// <param name="Type">The type of the member.</param>
/// <param name="Visibility">The visibility of the member.</param>
/// <param name="Kind">The kind of member (field, property, method).</param>
/// <param name="Parameters">The parameters if this member is a method.</param>
public record MemberDefinition(
    string Name,
    string Type,
    Visibility Visibility,
    MemberKind Kind,
    IReadOnlyList<ParameterDefinition>? Parameters = null
);

/// <summary>
/// Represents a parameter definition for a method.
/// </summary>
/// <param name="Name">The name of the parameter.</param>
/// <param name="Type">The type of the parameter.</param>
public record ParameterDefinition(
    string Name,
    string Type
);

/// <summary>
/// Represents a relationship between two types.
/// </summary>
/// <param name="From">The source type of the relationship.</param>
/// <param name="To">The target type of the relationship.</param>
/// <param name="Kind">The kind of relationship.</param>
/// <param name="Label">An optional label for the relationship.</param>
/// <param name="Cardinality">An optional cardinality descriptor.</param>
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
/// <param name="Title">An optional title for the diagram.</param>
/// <param name="Types">The type definitions in the diagram.</param>
/// <param name="Relationships">The relationships between types in the diagram.</param>
public record ClassModel(
    string? Title,
    IReadOnlyList<TypeDefinition> Types,
    IReadOnlyList<Relationship> Relationships
);
