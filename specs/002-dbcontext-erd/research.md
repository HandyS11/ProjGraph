# Research: DbContext ERD Extraction

## Decision: Heuristic Roslyn-based Analysis

We use **Roslyn (Source Code Analysis)** as the primary driver. Instead of requiring a full build or loading assemblies, the tool parses `DbContext` files and heuristically discovers entity definitions by searching adjacent directories and following using directives.

### Implementation Strategy

1. **Discovery**:
   - Parse the target `.cs` file for classes inheriting from `DbContext` (identified by name or base class).
   - Extract `DbSet<T>` properties to identify root entities.

2. **Heuristic Entity Discovery**:
   - For each entity type, search for a corresponding `.cs` file in the same directory, parent directory, and common subdirectories (e.g., `Entities`, `Models`).
   - Follow `using` directives to narrow down potential locations.
   - Recursively parse base classes to include inherited properties.

3. **Relationship Mapping**:
   - **One-to-Many**: Extracted by identifying collection properties on one side and matching reference/FK properties on the other.
   - **Many-to-Many**: Detected when two entities have collections of each other. The tool automatically creates a "shadow" join entity in the Mermaid diagram if one isn't explicitly defined.
   - **One-to-One**: Both sides have reference properties.

4. **Mermaid Rendering**:
   - Map EF properties to Mermaid ERD attributes.
   - Include PK/FK markers.
   - Add constraints (Required, MaxLength, Precision) as Mermaid comments.

### Alternatives Considered

- **EF Core Design-time Metadata**:
  - *Pros*: 100% accurate.
  - *Cons*: Requires the project to be buildable; requires loading the user's assemblies which can cause version conflicts with the tool.
  - *Resolution*: Rejected as primary but could be a future "Power Mode".

## Mermaid Syntax Mapping

| Relationship | Mermaid Syntax | EF Pattern |
|--------------|----------------|------------|
| One-to-Many (Required) | `A ||--o{ B` | `DbSet<B>`, `A` has `ICollection<B>` |
| One-to-One | `A ||--|| B` | Both have references |
| Many-to-Many | `A }|--|{ B` | Both have collections |

## Technology Choices

- **Microsoft.CodeAnalysis.CSharp**: For syntax and symbol analysis.
- **Microsoft.CodeAnalysis.Workspaces.MSBuild**: For solution/project level discovery.
