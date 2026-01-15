# Feature Specification: DbContext ERD Generation

**Feature Branch**: 002-dbcontext-erd
**Created**: 2026-01-15
**Status**: Draft
**Input**: User description: "I want the tool to be able to generate a erd mermaid diagram based on a DbContext. The tool will be able to locate the dbContext in a solution or take a file path as argument. Also add it to the mcp server."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate ERD from Solution (Priority: P1)

As a developer working on a large .NET solution, I want to quickly visualize the database schema defined in my Entity Framework Core DbContext without having to run the application or connect to a live database.

**Why this priority**: This is the primary use case for understanding existing or new data models during development.

**Independent Test**: Running the command \projgraph erd --path MySolution.sln\ prints a valid Mermaid \rDiagram\ to the console.

**Acceptance Scenarios**:

1. **Given** a solution with one DbContext and several entities (User, Post, Comment), **When** I run the ERD command on the solution, **Then** I see a Mermaid diagram showing the entities and their relationships (1:N, etc.).
2. **Given** a solution with multiple DbContext classes, **When** I run the command without specifying a context name, **Then** the tool lists available contexts and asks for a selection or identifies the primary one.
3. **Given** a solution where no DbContext is found, **When** I run the command, **Then** I see a helpful error message indicating no Entity Framework contexts were detected.

---

### User Story 2 - Generate ERD from Specific File (Priority: P1)

As a developer, I want to generate a diagram for a specific DbContext file I am currently editing, even if it's not yet part of a fully buildable solution.

**Why this priority**: Enables fast feedback loops during the design phase of a data model.

**Independent Test**: Running \projgraph erd --file ./Data/AppDbContext.cs\ produces a Mermaid diagram based solely on the source code analysis of that file.

**Acceptance Scenarios**:

1. **Given** a .cs file containing a class inheriting from DbContext with DbSet properties, **When** I run the ERD command on that file, **Then** the tool extracts the entity names and generates the diagram.
2. **Given** a file that does not contain a DbContext, **When** I run the command, **Then** I see an error message stating the file does not contain a valid context.

---

### User Story 3 - AI-Assisted Schema Analysis via MCP (Priority: P2)

As an AI Assistant, I want to retrieve a Mermaid ERD of the project's data model so I can help the user write queries, plan migrations, or explain the architecture.

**Why this priority**: Extends the tool's utility to AI-driven workflows and automated documentation.

**Independent Test**: Calling the MCP tool \get_erd\ with a project path returns a structured response containing the Mermaid string.

**Acceptance Scenarios**:

1. **Given** an MCP client (like GitHub Copilot), **When** I call \get_erd\ for a valid workspace path, **Then** I receive the Mermaid ERD code representing the Entity Framework model.

### Edge Cases

- **Complex Relationships**: The tool MUST represent shadow join tables as separate nodes in the ERD to accurately reflect the database schema.
- **Inheritance**: How are TPH (Table Per Hierarchy) or TPT (Table Per Type) models represented? (Default: Represent base and derived classes as linked entities).
- **Missing References**: If entities are defined in a separate assembly not reachable via simple file scanning, the tool should attempt to follow project references or provide a warning.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST be able to identify classes inheriting from Microsoft.EntityFrameworkCore.DbContext through source code analysis.
- **FR-002**: System MUST extract entity sets defined as DbSet<T> properties within the DbContext.
- **FR-003**: System MUST identify relationships between entities by analyzing properties and navigation properties (e.g., virtual ICollection<Post>, public int UserId).
- **FR-004**: System MUST support generating Mermaid erDiagram syntax.
- **FR-005**: System MUST provide a CLI command erd that accepts --path (solution/project) or --file (source file).
- **FR-006**: System MUST expose an MCP tool get_erd that provides the Mermaid diagram for a given workspace path.
- **FR-007**: System MUST scan all projects in a solution and handle cases where multiple DbContext classes are present, allowing selection via the CLI or an optional parameter.

### Key Entities *(include if feature involves data)*

- **Entity**: Represents a table/class in the EF model. Has a name and a set of properties.
- **Relationship**: A link between two Entities (one-to-one, one-to-many, many-to-many).
- **DbContext**: The container for the model metadata.

### MCP Tool Interface

- **Tool Name**: get_erd
- **Description**: Generates a Mermaid Entity Relationship Diagram (ERD) based on an Entity Framework Core DbContext found in the specified path.
- **Parameters**:
  - path: (string) Absolute path to the solution, project, or specific DbContext file.
  - contextName: (string, optional) Specific DbContext class name to use if multiple are present.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Successfully generates a valid Mermaid ERD for a standard "Blog/Post" sample model in under 2 seconds.
- **SC-002**: 100% of generated Mermaid strings are syntactically correct and render without errors in Mermaid.live.
- **SC-003**: Tool correctly identifies at least 3 types of relationships: 1:1, 1:N, and N:M (using standard EF Core conventions).
- **SC-004**: Command-line interface returns a non-zero exit code and clear error message when no DbContext is found.
