# Data Model: Class Diagram

This document defines the entities and relationships used to represent C# class diagrams.

## Entities

### `ClassModel`

The root container for a class diagram analysis.

- **Title**: (string) Optional title for the diagram.
- **Types**: (List<TypeDefinition>) List of types (classes, interfaces, etc.) included in the diagram.
- **Relationships**: (List<Relationship>) List of relationships between the types.

### `TypeDefinition`

Represents a specific type (Class, Interface, Struct, or Enum).

- **Name**: (string) The short name of the type.
- **Namespace**: (string) The namespace containing the type.
- **FullName**: (string) Combined namespace and name.
- **Kind**: (TypeKind) Enum: `Class`, `Interface`, `Struct`, `Enum`.
- **Members**: (List<MemberDefinition>) List of properties and methods.
- **IsExternal**: (bool) True if the source code was not found in the workspace.

### `MemberDefinition`

Represents a property, field, or method within a type.

- **Name**: (string) Name of the member.
- **Type**: (string) The return type or property type.
- **Visibility**: (Visibility) Enum: `Public`, `Protected`, `Internal`, `Private`.
- **Kind**: (MemberKind) Enum: `Property`, `Field`, `Method`.
- **Parameters**: (List<ParameterDefinition>) For methods, the list of parameters.

### `Relationship`

Represents a connection between two types.

- **From**: (string) FullName of the source type.
- **To**: (string) FullName of the target type.
- **Kind**: (RelationshipKind) Enum:
  - `Inheritance` (`<|--`)
  - `Realization` (`<|..`)
  - `Association` (`-->`)
  - `Dependency` (`..>`)

## Relationships

- `ClassModel` 1 -- * `TypeDefinition`
- `ClassModel` 1 -- * `Relationship`
- `TypeDefinition` 1 -- * `MemberDefinition`
- `Relationship` * -- 1 `TypeDefinition` (as From)
- `Relationship` * -- 1 `TypeDefinition` (as To)

## State Transitions

1. **Discovery**: Initial set of types from target file.
2. **Expansion**: Optionally search workspace for base classes and used types.
3. **Analysis**: Extract members and visibility for all discovered types.
4. **Graph Construction**: Create `Relationship` records based on the analysis.
5. **Rendering**: Convert `ClassModel` to Mermaid syntax.
