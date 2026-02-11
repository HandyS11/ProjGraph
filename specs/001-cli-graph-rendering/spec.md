# Feature Specification: CLI Graph Rendering & MCP Hub

**Feature Branch**: `001-cli-graph-rendering`
**Created**: 2026-01-13
**Status**: Complete
**Input**: User description: "I am building a .net tool nammed projgraph. It is able to draw some graph in the CLI based on the dependencies of some .net project. Based on a .sln, .slnx or even a .csproj. There is also a mcp tool that do the same thing to be more compliant with other tools. This tool is code using a clean archi pattern and ensure code quality."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Visualize Project Dependencies in CLI (Priority: P1)

As a developer, I want to see a visual representation of how different projects in my solution depend on each other directly in my terminal so I can understand the architecture at a glance.

**Why this priority**: This is the core functionality of the tool and the primary value proposition.

**Independent Test**: Running the tool against a multi-project `.sln` file prints a clear dependency tree or graph to the standard output.

**Acceptance Scenarios**:

1. **Given** a directory containing a `.sln` file with 3 projects (A depends on B, B depends on C), **When** I run `projgraph` on the solution file, **Then** I see a hierarchical representation showing A -> B -> C.
2. **Given** a `.csproj` file with no project dependencies, **When** I run `projgraph` on it, **Then** it shows only the project itself as a single node.
3. **Given** a non-existent file path, **When** I run `projgraph`, **Then** it shows a clear error message that the file was not found.

---

### User Story 2 - Automated Graph Extraction via MCP (Priority: P2)

As an AI Assistant or automated tool, I want to programmatically query the dependency graph of a .NET project using an MCP tool so I can help the user with refactoring or architectural analysis.

**Why this priority**: Enables interoperability with AI agents (like GitHub Copilot) to provide deeper insights.

**Independent Test**: Using an MCP-compliant client to call the `get_project_graph` tool with a valid path returns a structured JSON object of dependencies.

**Acceptance Scenarios**:

1. **Given** an MCP host running the `projgraph` MCP toolset, **When** I call `get_project_graph` with a valid `.slnx` path, **Then** I receive a JSON response containing a list of `nodes` and `edges`.
2. **Given** an invalid project file, **When** I call the MCP tool, **Then** I receive an error response detailing the parsing failure.

---

### User Story 3 - Support for modern .slnx files (Priority: P3)

As a modern .NET developer, I want the tool to support the new XML-based `.slnx` format so I can use it in my latest projects.

**Why this priority**: Ensures longevity and support for the latest .NET ecosystem features.

**Independent Test**: Running the tool against a `.slnx` file produces the same graph accuracy as a legacy `.sln` file.

**Acceptance Scenarios**:

1. **Given** a `.slnx` file, **When** I run `projgraph`, **Then** the tool correctly parses the solution structure and renders the graph.

### Edge Cases

- **Circular Dependencies**: What happens when Project A depends on B, and B depends on A? The tool will detect circular dependencies using Tarjan's SCC and flag them as errors or visual cycles in the output.
- **Complex Solutions**: How does the tool handle solutions with 50+ projects? By default, it will render the full graph. Performance optimization via MSBuild evaluation caching will ensure speed.
- **Missing References**: What happens if a project references a project that isn't in the solution or on disk? The tool will list it as a "Missing" node in the graph with a distinct visual style.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support parsing standard `.sln` (Solution), `.slnx` (Modern Solution), and `.csproj` (Project) files.
- **FR-002**: System MUST render the dependency graph in the CLI using a Tree-based ASCII/ANSI format (via Spectre.Console) and support optional Mermaid.js text export.
- **FR-003**: System MUST identify and describe project-to-project references.
- **FR-004**: System MUST expose a Model Context Protocol (MCP) tool that provides identical graph data to the CLI.
- **FR-005**: The MCP tool MUST return data in a machine-readable format (JSON).

### Key Entities *(include if feature involves data)*

- **Project**: Represents a single .NET project, with a path and a unique identifier.
- **Dependency**: Represents a directional relationship from one Project to another.
- **Solution**: A container for multiple Projects and their global relationships.

### MCP Tool Interface

- **Tool Name**: `get_project_graph`
- **Description**: Analyzes a .NET solution or project file and returns the dependency graph as a set of nodes and edges.
- **Parameters**:
  - `path`: (string) The absolute path to the .sln, .slnx, or .csproj file.
  - `show_title`: (boolean, optional) Whether to include the title in the diagram (default: true).
  - `includePackages`: (boolean) (Out of scope for initial version) Whether to include NuGet package dependencies in the graph.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Graph rendering for solutions with up to 10 projects completes in under 1 second.
- **SC-002**: The MCP tool output is 100% consistent with the CLI output for the same input file.
- **SC-003**: 100% of properly formatted `.sln`, `.slnx`, and `.csproj` files are parsed without errors.
- **SC-004**: Users can identify a specific dependency path between any two projects in the graph within 5 seconds of looking at the CLI output.

---
