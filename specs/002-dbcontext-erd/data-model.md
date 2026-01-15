# Data Model: DbContext ERD

## Domain Entities

### `EfModel`

The root container for an extracted Entity Framework model.

- `Entities`: List of `EfEntity`
- `Relationships`: List of `EfRelationship`
- `ContextName`: Name of the source `DbContext` class

### `EfEntity`

Represents a database table/entity.

- `Name`: String (e.g., "Post")
- `Properties`: List of `EfProperty`
- `IsJoinEntity`: Boolean (True if it's a shadow join table)

### `EfProperty`

Represents a column/property.

- `Name`: String
- `Type`: String
- `IsPrimaryKey`: Boolean
- `IsForeignKey`: Boolean

### `EfRelationship`

Represents a link between two entities.

- `SourceEntity`: String
- `TargetEntity`: String
- `Type`: Enum (`OneToOne`, `OneToMany`, `ManyToMany`)
- `IsRequired`: Boolean
- `Label`: String (usually the navigation property name)

## State Transitions

1. **Discovery**: Scan for `DbContext` -> List of strings.
2. **Extraction**: Analyze specific `DbContext` -> `EfModel`.
3. **Rendering**: `EfModel` -> Mermaid string.
