# Feature Specification: Mermaid Class Diagram

**Feature Branch**: `003-mermaid-class-diagram`
**Created**: 2026-01-20
**Status**: Complete
**Input**: User description: "Add a feature to draw classdiagram using mermaid. The user will give the exact path to the cs file. It can give option to add inheritance and dependencies on other classes (the tool will need to find them manually)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate Class Diagram for a Specific File (Priority: P1)

As a developer, I want to provide a path to a specific C# file and get a Mermaid class diagram focusing on the types defined in that file.

**Why this priority**: Primary entry point for the feature as requested.

**Independent Test**: Can be tested by running the command with a `.cs` file path and verifying the Mermaid output matches the classes in that file.

**Acceptance Scenarios**:

1. **Given** a path to `User.cs`, **When** I run the tool, **Then** I receive a Mermaid `classDiagram` containing all classes/interfaces defined in that file.

---

### User Story 2 - Discover Inheritance and Dependencies (Priority: P1)

As a developer, I want the tool to optionally "go deeper" and find the base classes or dependent classes of the types in my file, even if they are in other files.

**Why this priority**: Provides the "manual discovery" value requested by the user.

**Independent Test**: Provide a file containing a class that inherits from a base class in a different file. Run with `--include-hierarchy` and verify the base class and its relationship appear in the diagram.

**Acceptance Scenarios**:

1. **Given** `BusinessService.cs` (inherits from `BaseService`), **When** I run the tool with the inheritance option, **Then** the tool searches the workspace, finds `BaseService`, and draws the inheritance link (`BaseService <|-- BusinessService`).
2. **Given** `UserService.cs` (uses `UserRepository`), **When** I run the tool with the dependency option, **Then** the tool identifies `UserRepository` and adds it to the diagram with an association link.

---

### User Story 3 - Visualizing Complex Relationships (Priority: P2)

As a developer, I want to control the depth of the discovery so the diagram doesn't become too large when classes have many indirect dependencies.

**Why this priority**: Essential for keeping diagrams readable in complex codebases.

**Independent Test**: Run discovery on a class with multiple levels of inheritance/dependency and verify only the requested levels are shown.

**Acceptance Scenarios**:

1. **Given** a deep inheritance tree, **When** I set discovery depth to 1, **Then** only immediate parents and children are shown.

---

### Edge Cases

- **External Libraries**: If a class inherits from `System.Object` or a NuGet package class, the tool should represent the type name even if the source code isn't available for manual parsing.
- **Multiple Types per File**: If a `.cs` file contains multiple classes, all should be included in the initial set.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept a file path to a specific `.cs` file as the primary input.
- **FR-002**: System MUST parse the target file to identify all classes, interfaces, structs, and enums.
- **FR-003**: System MUST identify public and internal members (properties, methods, fields) for each type.
- **FR-004**: System MUST support an optional flag to "discover inheritance" which manually searches the workspace for base classes/interfaces of the target types.
- **FR-005**: System MUST support an optional flag to "discover dependencies" which identifies types used as properties, fields, or method parameters and adds them to the diagram.
- **FR-006**: System MUST output valid Mermaid `classDiagram` syntax, including relationship markers: `<|--` (Inheritance), `<|..` (Realization), `-->` (Association).
- **FR-007**: System MUST provide an MCP tool `get_class_diagram` that takes a `filePath` and optional discovery flags.
- **FR-008**: System MUST handle generic types (e.g., `IService<T>`) by formatting them correctly for Mermaid (e.g., `IService~T~`).

### Key Entities *(include if feature involves data)*

- **TypeNode**: A node in the diagram representing a discovered type (Class, Interface, etc.).
- **RelationshipEdge**: A connection between two TypeNodes with a specific Mermaid relationship type.
- **WorkspaceContext**: The broader directory structure searched during manual discovery of types.

## Success Criteria

1. **Resolution Accuracy**: At least 95% of workspace-internal type relationships (inheritance/dependencies) are correctly identified when discovery flags are enabled.
2. **Output Validity**: 100% of generated Mermaid code passes validation in standard Mermaid renderers.
3. **Performance**: Discovery across a medium-sized workspace (e.g., 100-500 files) completes in under 5 seconds.
4. **Usability**: The CLI and MCP tool clearly state when a dependency was found but its full definition could not be resolved (e.g., external SDK types).

## Assumptions

- "Manual discovery" means searching `.cs` files in the current workspace (repository root) to find type definitions that match names found in the target file.
- We will prioritize simplicity; we won't necessarily require a full Roslyn compilation context if string/regex parsing of the workspace is sufficient and faster, but we will use the most reliable method available in the codebase.
- The user is responsible for rendering the Mermaid output.

## MCP Tool Interface

- **Tool Name**: `get_class_diagram`
- **Description**: Generates a Mermaid class diagram for one or more C# files, with options to resolve inheritance and dependencies within the workspace.
- **Parameters**:
  - `filePath`: (string) Path to the .cs file to analyze.
  - `includeInheritance`: (boolean) Whether to search the workspace for base classes and interfaces.
  - `includeDependencies`: (boolean) Whether to search for and include classes used by the target types.
  - `depth`: (integer) How many levels of relationships to follow (default: 1).
  - `show_title`: (boolean, optional) Whether to include the title in the diagram (default: true).
