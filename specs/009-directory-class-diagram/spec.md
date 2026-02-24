# Feature Specification: Folder/Directory Scanning for classdiagram

**Feature Branch**: `009-directory-class-diagram`
**Created**: 2026-02-24
**Status**: Draft
**Input**: User description: "Accept a directory path in addition to a single .cs file. ProjGraph would enumerate all .cs files in the folder and build one combined diagram."

## Clarifications

### Session 2026-02-24

- Q: Which subfolders should be skipped during recursive scan? → A: Option B - Standard Ignores (bin, obj, .git, node_modules). Factorize these exclusions as they are already used in project mapping.
- Q: How should the system handle very large directories (>50 files)? → A: Option B - Warning Trigger. Log a warning to the user but proceed with generation.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Folder-based Class Diagram (Priority: P1)

A developer wants to visualize all classes in a specific domain or model folder to understand their relationships at a glance. They provide the folder path to the CLI or MCP tool, and the system generates a single Mermaid diagram containing all discovered classes.

**Why this priority**: This is the primary value proposition of the feature, allowing a broader view than single-file analysis.

**Independent Test**: Provide a path to a directory containing three `.cs` files with related classes; verify the output Mermaid code includes all three classes and their relationships.

**Acceptance Scenarios**:

1. **Given** a directory containing multiple `.cs` files, **When** the directory path is used as input for a class diagram, **Then** all classes from all `.cs` files are included in the output.
2. **Given** a directory with no `.cs` files, **When** processed, **Then** a clear message is returned stating no source files were found.

---

### User Story 2 - Recursive Directory Scanning (Priority: P2)

A developer has a project structure where classes are organized in subfolders (e.g., `Models/Validation`, `Models/Entities`). They want to scan the top-level `Models` folder and have all nested classes included.

**Why this priority**: Essential for modern project structures where deep nesting is common.

**Independent Test**: Provide a directory path that has `.cs` files in subdirectories; verify the resulting diagram includes the nested classes.

**Acceptance Scenarios**:

1. **Given** a directory with nested subfolders containing `.cs` files, **When** scanned, **Then** classes from subfolders are included in the diagram.

---

### User Story 3 - Mixed Input Validation (Priority: P3)

If a user provides a path, the system should automatically detect if it is a file or a directory and handle it correctly without requiring extra flags from the user.

**Why this priority**: Improves user experience and reduces cognitive load by making the tool "just work".

**Independent Test**: Run the command once with a file path and once with a directory path; verify both succeed.

**Acceptance Scenarios**:

1. **Given** a valid file path, **When** processed, **Then** only that file is analyzed.
2. **Given** a valid directory path, **When** processed, **Then** all files in that directory are analyzed.

### Edge Cases

- **Empty Folder**: If no classes are found in any `.cs` file, return a graceful "No classes found" message.
- **Invalid Path**: If the provided path does not exist, return an error.
- **Permissions**: If the system cannot read the directory, return a specific error.
- **Duplicate Class Names**: If the folder contains different files defining the same class (e.g. partial classes or same name in different namespaces), they should be handled correctly (combined for partials, distinct for namespaces).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept a directory path as a valid input for class diagram generation.
- **FR-002**: System MUST identify and process all `.cs` files within the provided directory.
- **FR-003**: System MUST recursively scan all subdirectories for `.cs` files by default.
- **FR-004**: System MUST combine analysis results from all processed files into a single Mermaid class diagram.
- **FR-005**: System MUST validate that the path exists and is accessible.
- **FR-006**: System MUST use existing class analysis logic for each individual file to ensure consistency in member/relationship extraction.
- **FR-007**: System MUST automatically exclude common artifact and noise folders during directory scanning (e.g., `bin`, `obj`, `.git`, `node_modules`).
- **FR-008**: System MUST log a warning message to the user if more than 50 `.cs` files are detected during a directory scan, but still proceed with the analysis.
- **MCP Implementation**: The warning MUST be prepended to the generated Mermaid output string (e.g., as a commented line `%% WARNING: ...`) so it is visible to the consuming LLM.

### Key Entities *(include if feature involves data)*

- **Analysis Context**: Represents the set of files being analyzed together, providing the scope for relationship discovery.
- **Mermaid Diagram**: The final output entity containing the text representation of the class graph.

### MCP Tool Interface

- **Tool Name**: `get_class_diagram`
- **Description**: Generates a Mermaid class diagram for the types defined in a specific C# file or directory, with options to discover inheritance and related types.
- **Parameters**: (No changes to schema needed if `path` remains the primary argument, but description should be updated to include directories)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can generate a combined class diagram for a directory with 20+ `.cs` files in under 10 seconds.
- **SC-002**: All classes and their cross-file relationships within the folder are correctly represented in the Mermaid output.
- **SC-003**: The tool successfully differentiates between files and directories automatically.
- **SC-004**: No regression: Single file analysis remains as performant as before.

## Assumptions & Technical Constraints

- Analysis of a folder will follow the same configuration rules (e.g., including/excluding members) as single file analysis.
- Memory usage for large folders will be proportional to the number of syntax trees in the compilation.
- **Factorization**: Standard directory exclusions (bin, obj, etc.) MUST be factorized across the core libraries where they are currently used (e.g., project/artifact mapping).
