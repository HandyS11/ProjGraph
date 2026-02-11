# Feature Specification: DbContext & ModelSnapshot ERD Generation

**Feature Branch**: 002-dbcontext-erd
**Created**: 2026-01-15
**Updated**: 2026-01-29 (Added ModelSnapshot support)
**Status**: Complete
**Input**: User description: "I want the tool to be able to generate a erd mermaid diagram based on a DbContext or a ModelSnapshot. The tool will be able to locate the base file in a solution or take a file path as argument. Also add it to the mcp server."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate ERD from Solution (Priority: P1)

As a developer working on a large .NET solution, I want to quickly visualize the database schema defined in my Entity Framework Core DbContext or migration history without having to run the application or connect to a live database.

**Why this priority**: This is the primary use case for understanding existing or new data models during development.

**Independent Test**: Running `projgraph erd [path]` where path is a `.cs` file (DbContext or ModelSnapshot) prints a valid Mermaid `erDiagram` to the console.

**Acceptance Scenarios**:

1. **Given** a directory with one DbContext, several entities, and migration snapshots, **When** I run the ERD command without arguments, **Then** the tool finds the nearest context or snapshot and displays a Mermaid diagram showing the entities and their relationships.
2. **Given** a file or directory with multiple DbContext or ModelSnapshot classes, **When** I run the command, **Then** the tool provides an interactive selection prompt to choose the target.
3. **Given** a file or solution where no DbContext or ModelSnapshot is found, **When** I run the command, **Then** I see a helpful error message.

---

### User Story 2 - Generate ERD from Discovery or Specific File (Priority: P1)

As a developer, I want to generate a diagram for a specific DbContext file or a ModelSnapshot I am currently editing, even if it's not yet part of a fully buildable solution.

**Why this priority**: Enables fast feedback loops during the design phase or while reviewing migrations.

**Independent Test**: Running `projgraph erd ./Migrations/AppDbContextModelSnapshot.cs` produces a Mermaid diagram based on the migration snapshot analysis.

**Acceptance Scenarios**:

1. **Given** a .cs file containing a class inheriting from DbContext or ModelSnapshot, **When** I run the ERD command on that file, **Then** the tool extracts the metadata and generates the diagram.
2. **Given** a file that does not contain a DbContext or ModelSnapshot, **When** I run the command, **Then** I see an error message stating the file does not contain a valid source.

---

### User Story 3 - AI-Assisted Schema Analysis via MCP (Priority: P2)

As an AI Assistant, I want to retrieve a Mermaid ERD of the project's data model so I can help the user write queries, plan migrations, or explain the architecture.

**Why this priority**: Extends the tool's utility to AI-driven workflows and automated documentation.

**Independent Test**: Calling the MCP tool `get_erd` with a file path returns a structured response containing the Mermaid string.

**Acceptance Scenarios**:

1. **Given** an MCP client (like GitHub Copilot), **When** I call `get_erd` for a valid workspace path, **Then** I receive the Mermaid ERD code representing the Entity Framework model.

### Edge Cases

- **Complex Relationships**: The tool MUST represent shadow join tables for many-to-many relationships even if an explicit join entity is not in the `DbSets`.
- **Inheritance**: The tool extracts properties from base classes by locating base class source files recursively in the solution root.
- **Missing References**: The tool uses a heuristic discovery method to find entity files in common locations (`Entities`, `Models`, etc.) if they are not in the same file as the `DbContext`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST be able to identify classes inheriting from `DbContext` or `ModelSnapshot` through Roslyn source code analysis.
- **FR-002**: System MUST extract entity metadata from `DbSet<T>` properties (DbContext) or `modelBuilder.Entity` calls (ModelSnapshot).
- **FR-003**: System MUST identify relationships (1:1, 1:N, N:M) by analyzing property types, naming conventions, and Fluent API configurations.
- **FR-004**: System MUST support generating Mermaid `erDiagram` syntax with property markers (PK, FK) and constraints.
- **FR-005**: System MUST provide a CLI command `erd` that accepts an optional `path`.
- **FR-006**: System MUST expose an MCP tool `get_erd` that provides the Mermaid diagram from either context or snapshot.
- **FR-007**: System MUST support interactive selection if multiple relevant classes or files are found.

### Key Entities *(include if feature involves data)*

- **Entity**: Represents a table/class. Has a name, properties (with attributes like PK, FK, Required, etc.), and an indicator for join entities.
- **Relationship**: A link between two Entities (one-to-one, one-to-many, many-to-many).
- **EfModel**: The top-level container for the extracted metadata.

### MCP Tool Interface

- **Tool Name**: `get_erd`
- **Description**: Generates a Mermaid Entity Relationship Diagram (ERD) from an Entity Framework Core DbContext file.
- **Parameters**:
  - `path`: (string) Absolute path to the DbContext .cs file.
  - `contextName`: (string, optional) Specific DbContext class name to use if multiple are present.
  - `show_title`: (boolean, optional) Whether to include the title in the diagram (default: true).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Successfully generates a valid Mermaid ERD for a standard "Blog/Post" sample model in under 2 seconds.
- **SC-002**: 100% of generated Mermaid strings are syntactically correct and render without errors in Mermaid.live.
- **SC-003**: Tool correctly identifies at least 3 types of relationships: 1:1, 1:N, and N:M (using standard EF Core conventions).
- **SC-004**: Command-line interface returns a non-zero exit code and clear error message when no DbContext is found.
