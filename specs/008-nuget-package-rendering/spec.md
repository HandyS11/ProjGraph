# Feature Specification: NuGet Package Reference Rendering in `visualize`

**Feature Branch**: `008-nuget-package-rendering`
**Created**: 2026-02-24
**Status**: Draft
**Input**: User description: "NuGet Package Reference Rendering in `visualize`: Add an --include-packages flag to render NuGet package dependencies distinctly in Mermaid, tree, and flat formats."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Project and Package Dependencies (Priority: P1)

As a developer, I want to see which NuGet packages my projects depend on alongside project-to-project references so I can understand the full dependency tree.

**Why this priority**: Core value of the feature; provides immediate visibility into external dependencies which is a primary reason for using a dependency graph.

**Independent Test**: Run `visualize` with `--include-packages` on a project with NuGet references and verify that packages are listed in the output.

**Acceptance Scenarios**:

1. **Given** a solution with projects having NuGet references, **When** running `visualize --include-packages`, **Then** NuGet packages are displayed as nodes in the graph.
2. **Given** a solution with projects having NuGet references, **When** running `visualize` without the flag, **Then** NuGet packages are not displayed, maintaining current behavior.

---

### User Story 2 - Distinct Visualization in Diagrams (Priority: P2)

As a developer, I want NuGet packages to be visually distinct from projects in Mermaid diagrams so I can quickly identify external vs. internal dependencies at a glance.

**Why this priority**: Enhances the visual utility of the Mermaid output, which is a key feature of ProjGraph.

**Independent Test**: Generate a Mermaid diagram for a project with NuGet references and verify that package nodes use a different shape (e.g., rounded boxes) than project nodes (e.g., square boxes).

**Acceptance Scenarios**:

1. **Given** the `--include-packages` flag and Mermaid format are used, **When** a package dependency is rendered, **Then** it is enclosed in rounded parentheses `(Package Name)` or similar distinct Mermaid syntax.

---

### User Story 3 - Distinct Labeling in Text Formats (Priority: P2)

As a developer using CLI text output (tree or flat), I want packages to have a `[pkg]` prefix so I can distinguish them from project references in plain text.

**Why this priority**: Provides parity for text-based rendering and ensures clarity in non-graphical environments.

**Independent Test**: Generate tree or flat output for a project with NuGet references and check that package nodes are prefixed with `[pkg]`.

**Acceptance Scenarios**:

1. **Given** the `--include-packages` flag and tree or flat output format, **When** a package is listed, **Then** its name is prefixed with `[pkg]`.

### Edge Cases

- **Multiple versions of same package**: If different projects reference different versions of the same package, how are they rendered? (Assumption: Rendered as separate nodes or includes version in name).
- **Projects with no NuGet packages**: Flag should not cause errors and just show projects.
- **Empty solution**: Flag should not cause errors.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a new command-line flag `--include-packages` for the `visualize` command.
- **FR-002**: System MUST update the dependency graph builder to extract `PackageReference` items from `.csproj` files when the flag is enabled.
- **FR-003**: System MUST identify package references as a distinct type of dependency (`DependencyType.PackageReference`).
- **FR-004**: System MUST render NuGet packages in Mermaid diagrams using rounded-corner nodes (e.g., `id(Name)`) to distinguish them from project nodes.
- **FR-005**: System MUST render NuGet packages in tree and flat text outputs with a `[pkg]` prefix before the package name.
- **FR-006**: System MUST exclude NuGet packages from the graph by default when the `--include-packages` flag is omitted.

### Key Entities

- **Package Node**: An external dependency pulled from a NuGet gallery, represented as a `Project` record with `ProjectType.Package` in the graph model.
- **Package Reference**: A source code link from a project to a **Package Node**, represented as a `Dependency` record with `DependencyType.PackageReference`.

### Assumptions

- **Direct References Only**: This feature focuses on direct `PackageReference` entries in the project file. Transitive dependencies (packages depending on other packages) are out of scope for this task.
- **Version Display**: Package names will include their version (e.g., `Newtonsoft.Json 13.0.1`) to help identify version conflicts.
- **Shape Choice**: Mermaid rounded nodes `id(label)` will be used for packages, while project nodes use standard `id[label]`.

### MCP Tool Interface

- **Tool Name**: `get_project_graph`
- **Description**: Analyzes a .NET solution or project file and returns the dependency graph as a Mermaid diagram.
- **Parameters**:
  - `path`: (string) Absolute path to the project or solution file.
  - `showTitle`: (boolean) Whether to include the title in the diagram (default: true).
  - `includePackages`: (boolean) Whether to include NuGet package dependencies in the graph (default: false).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of direct `<PackageReference>` items in the target projects are represented in the output when `includePackages` is active.
- **SC-002**: Visual distinction between projects and packages is achieved in 100% of tested outputs (Mermaid shapes, text prefixes).
- **SC-003**: ZERO performance degradation (<5% variance) when the tool is run without the package-inclusion flag.
- **SC-004**: System successfully handles and deduplicates multiple versions of the same package across projects.
