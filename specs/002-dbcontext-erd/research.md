# Research: DbContext ERD Extraction

## Decision: Hybrid Roslyn-based Analysis

We will use **Roslyn (Source Code Analysis)** as the primary driver to ensure the tool works without requiring a successful build of the entire solution, which is critical for development-time tools and AI agents.

### Rationale

- **Speed**: Static analysis is significantly faster than compiling and loading assemblies.
- **Resilience**: Can work on single files or solutions with missing dependencies.
- **Portability**: Avoids issues with loading different .NET versions of EF Core into the tool's process.

### Implementation Strategy

1. **Discovery**:
   - Use `Compilation.GetSymbolsWithName` or recursive namespace traversal to find types inheriting from `DbContext`.
   - Identify `DbSet<T>` properties.

2. **Entity Analysis**:
   - Extract properties and types from the entity classes.
   - Map navigation properties (scalar and collection) to relationships.

3. **Relationship Mapping**:
   - **One-to-Many**: One side has a collection, the other has a reference or nothing.
   - **Many-to-Many**: Both sides have collections. Detect "Shadow" join tables by identifying N:M relationships that don't have an explicit join entity in the `DbSets`.
   - **One-to-One**: Both sides have references.

4. **Handling Fluent API (Limitations)**:
   - We will support basic `OnModelCreating` parsing of `HasOne/HasMany` chains using a syntax walker.
   - For complex configurations (loops, external config classes), we will provide a warning that the diagram might be based on conventions.

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
