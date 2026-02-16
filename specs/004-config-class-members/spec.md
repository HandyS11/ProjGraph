# Feature Specification: Configure Class Member Visibility

**Feature Branch**: `004-config-class-members`
**Created**: 2026-02-16
**Status**: Draft
**Input**: User description: "I want to improve the classdiagram tool by allowing the user to set if the tool will look and display properties and/or functions. By default, theses two options are true but can be override."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - High-Level Architecture View (Priority: P1)

As a software architect, I want to generate a class diagram that only shows the classes and their relationships, without showing any internal members, so that I can focus on the system structure without being distracted by implementation details.

**Why this priority**: This is the primary use case for reducing noise in diagrams, which is the core of the user request.

**Independent Test**: Generate a diagram for a complex workspace with `includeProperties` and `includeFunctions` set to `false`, and verify that the output contains only class names and relationship lines.

**Acceptance Scenarios**:

1. **Given** a C# file with multiple properties and methods, **When** the `GetClassDiagram` tool is called with `includeProperties=false` and `includeFunctions=false`, **Then** the resulting Mermaid diagram shows class boxes containing only the class names.
2. **Given** no explicit configuration, **When** the `GetClassDiagram` tool is called, **Then** all properties and functions are visible by default.

---

### User Story 2 - Behavioral Focus (Priority: P2)

As a developer, I want to see only the functions (methods) of a class without the data properties, so that I can understand the available operations and behavior of the API.

**Why this priority**: Allows for specialized analysis of class behavior, which is a common requirement during code review or API design.

**Independent Test**: Generate a diagram with `includeProperties=false` and `includeFunctions=true` and verify that only methods are listed in the class boxes.

**Acceptance Scenarios**:

1. **Given** a class with both properties and methods, **When** requested with `includeProperties=false` and `includeFunctions=true`, **Then** the class box in the diagram lists the methods but does not list any properties.

---

### User Story 3 - Data Focus (Priority: P3)

As a data modeler, I want to see only the properties (data) of a class without the logic, so that I can understand the data structure and state maintained by the type.

**Why this priority**: Useful for documenting data models or DTOs where business logic is secondary.

**Independent Test**: Generate a diagram with `includeProperties=true` and `includeFunctions=false` and verify that only properties are listed.

**Acceptance Scenarios**:

1. **Given** a class with both properties and methods, **When** requested with `includeProperties=true` and `includeFunctions=false`, **Then** the class box in the diagram lists the properties but does not list any methods.

---

### Edge Cases

- **Empty Classes**: What happens when a class has no members and both toggles are set to true? (System should show an empty class box).
- **Dependency Discovery**: Even if a property/field is hidden via `includeProperties=false`, the tool MUST still use it to discover and draw relationship lines (associations) to other classes.
- **Member Type Grouping**: "Functions" maps to `MemberKind.Method`. "Properties" maps to `MemberKind.Property` and `MemberKind.Field`. Constructors are not currently extracted by the analyzer and are out of scope.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a way to toggle the visibility of type properties and fields in the generated diagram via an `includeProperties` parameter.
- **FR-002**: System MUST provide a way to toggle the visibility of type methods in the generated diagram via an `includeFunctions` parameter.
- **FR-003**: By default, both properties and functions MUST be included in the diagram.
- **FR-004**: The choice of member visibility MUST NOT affect the discovery of types or the rendering of relationship lines (associations, inheritance) between classes.
- **FR-005**: All currently extracted member types (`MemberKind.Property`, `MemberKind.Field`, `MemberKind.Method`) MUST be assigned to either the "Properties" or "Functions" category for visibility control.

### Key Entities

- **Analysis Options**: The configuration object that stores the user's preferences for member visibility.
- **Member Filter**: The logic responsible for determining if a member should be included in the rendering based on the options.

### MCP Tool Interface

- **Tool Name**: `GetClassDiagram`
- **Description**: Generates a Mermaid class diagram for the types defined in a specific C# file, with options to control member visibility.
- **Parameters**:
  - `filePath`: (string) Absolute path to the .cs file to analyze.
  - `includeInheritance`: (boolean) Whether to search the workspace for base classes and interfaces.
  - `includeDependencies`: (boolean) Whether to search for and include other classes used as properties or fields.
  - `includeProperties`: (boolean) Whether to display properties and fields in the class diagram (default: true).
  - `includeFunctions`: (boolean) Whether to display functions/methods in the class diagram (default: true).
  - `depth`: (integer) How many levels of relationships to follow.
  - `showTitle`: (boolean) Whether to include the title in the diagram (default: true).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can successfully generate a "clean" class diagram (no members) with a single tool call using the new parameters.
- **SC-002**: The default behavior remains unchanged for existing users who do not provide the new parameters.
- **SC-003**: The time taken to generate the diagram is reduced when members are excluded.
- **SC-004**: Diagram size (character count) is reduced by at least 50% for typical classes when members are hidden, improving readability in LLM contexts.
