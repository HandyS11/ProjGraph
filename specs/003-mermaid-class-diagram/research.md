# Research: Mermaid Class Diagram Generation

## Objective

Research the best practices for generating Mermaid class diagrams from C# source files, focusing on type discovery, relationship extraction, and Mermaid syntax compatibility.

## Findings

### 1. Mermaid Class Diagram Syntax

Mermaid's `classDiagram` supports the following key features we need:

- **Class Definition**: `class MyClass { +string Name \n +DoSomething() }`
- **Generics**: `class IService~T~` (Use `~` for `<` and `>`).
- **Inheritance**: `BaseClass <|-- DerivedClass`
- **Realization (Interface)**: `Interface <|.. Implementation`
- **Association**: `ClassA --> ClassB` (Usage in properties/fields).
- **Composition/Aggregation**: `ClassA *-- ClassB` / `ClassA o-- ClassB` (Might be overkill for a simple diagram, but good to know).

### 2. Type Discovery & Workspace Search

**Current Approach (EF Analysis)**:

- Uses heuristics like sibling directories and naming conventions (`Models`, `Entities`).
- Searches for files named `{TypeName}.cs`.

**Proposed Approach for Class Diagram**:

- **Baseline**: Parse the primary `.cs` file using `CSharpSyntaxTree`.
- **Relationship Extraction**: Identify base classes, implemented interfaces, and property/field types.
- **Manual Workspace Search**:
  - Instead of just checking `{TypeName}.cs`, we should ideally search for the string `class {TypeName}` or `interface {TypeName}` or `struct {TypeName}` in all `.cs` files if the simple filename check fails.
  - Limitation: This might be slow for very large workspaces.
  - Solution: Implement a two-tier search:
        1. Check `{TypeName}.cs` in the same directory and subdirectories.
        2. Check for `{TypeName}.cs` in the entire workspace.
        3. (Optional/Future) Full-text search for the type definition.

### 3. Roslyn Compilation context

- To accurately resolve types (especially with same names in different namespaces), a `Compilation` is needed.
- We can create a `Compilation` by adding all relevant `.cs` files found during discovery.
- `CompilationFactory.CreateCompilation(syntaxTrees)` already exists in `ProjGraph.Lib`.

### 4. Naming Collisions & Mermaid IDs

- Mermaid doesn't strictly require unique IDs if class names are unique.
- If we have `NamespaceA.User` and `NamespaceB.User`, we should probably use their full names or unique aliases in Mermaid to avoid drawing them as the same node.
- Mermaid Syntax: `class NamespaceA_User["NamespaceA.User"]` where the text in brackets is the display name.

### 5. Filtering & Depth

- **Depth**: The user requested an "option to add inheritance and dependencies". We should support a `depth` parameter (default 1) to control how many steps of relationships to follow.
- **Member Filtering**: Default to public members. We could add flags for `--all-members`.

## Technical Decisions

- **Decision**: Use `CSharpSyntaxTree` for initial parsing and heuristic file discovery, followed by `CSharpCompilation` for semantic analysis once related files are gathered.
- **Rationale**: Matches existing patterns in `EfAnalysisService` and provides accurate type resolution.
- **Alternatives considered**:
  - Just using regex/string parsing: Faster but brittle and can't resolve complex generic types or cross-file inheritance accurately.
  - Requiring a `.sln` or `.csproj` file: More accurate but less flexible than the requested "exact path to the cs file" approach.
- **Decision**: Mermaid syntax will use `~T~` for generics and `<|--`, `<|..`, `-->` for relationships.
- **Rationale**: Standard Mermaid syntax for class diagrams.

## Unresolved Items (NEEDS CLARIFICATION)

- Should we include private members if the user specifically asks, or stick to public/internal? (Decision: Public/Internal by default, maybe add a flag later).
- How to handle external types (e.g., `List<T>`, `System.IO.Stream`)? (Decision: Show them as nodes but don't try to find their source code).
