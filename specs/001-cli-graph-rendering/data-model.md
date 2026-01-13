# Data Model: CLI Graph Rendering & MCP Hub

## Entities

### Project
Represents a single .NET unit of work.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Unique identifier for the project node. |
| Name | String | Display name of the project. |
| RelativePath | String | Path relative to the solution or root. |
| FullPath | String | Absolute path on disk. |
| Framework | String | Target framework identifier (e.g., net10.0). |
| ProjectType | Enum | Library, Executable, Test, etc. |

### Dependency (Edge)
Represents a directional link between two projects.

| Field | Type | Description |
|-------|------|-------------|
| SourceId | Guid | The project that has the reference. |
| TargetId | Guid | The project being referenced. |
| DependencyType | Enum | ProjectReference, or optional PackageReference. |
| Version | String? | Version constraint (if package). |

### Solution
The root container for the graph.

| Field | Type | Description |
|-------|------|-------------|
| Name | String | Name of the solution file. |
| Path | String | Path to the `.sln` or `.slnx`. |
| Projects | List<Project> | All projects discovered. |
| Edges | List<Dependency> | All established relationships. |

## Relationships

- A **Solution** contains many **Projects**.
- A **Solution** contains many **Dependencies**.
- An **Edge** connects exactly two **Projects** (Source -> Target).

## State Transitions & Validation

- **Parsing State**: Project paths must exist on disk before being added to the model.
- **Validation**: 
    - No duplicate Project IDs within a Solution.
    - Edges must point to valid Project nodes.
    - Circular dependencies are allowed in the model but flagged during rendering.
