# Feature Specification: Solution Metrics Command (`projgraph stats`) and MCP Tool

**Feature Branch**: `010-project-stats-mcp`
**Created**: 2026-02-26
**Status**: Draft
**Input**: User description: "A new `stats` command that analyses a solution and prints key metrics: project count, test/library/exe breakdown, average dependency depth, most-depended-on projects. Expose the same stats as an MCP tool `get_project_stats` so AI assistants can query solution health programmatically."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View solution metrics at a glance (Priority: P1)

As a developer or architect, I want to run a single command against my solution and instantly see a summary of key architectural metrics — project count, type breakdown, dependency depth, and hotspot projects — so I can assess solution health without generating a full diagram.

**Why this priority**: This is the core deliverable of the feature. All other stories depend on this analysis being correct and available. A developer can immediately act on this information to spot architectural issues.

**Independent Test**: Run `projgraph stats <path-to-solution>` and verify the output includes total project count, a breakdown of test/library/executable counts, average dependency depth, and a ranked list of most-depended-on projects.

**Acceptance Scenarios**:

1. **Given** a valid solution path, **When** I run `projgraph stats <path>`, **Then** the output displays total project count, project type counts, average dependency depth, and the top most-referenced projects.
2. **Given** a valid solution path, **When** I run the command, **Then** the output is formatted for easy reading in a terminal (aligned columns or labelled sections).
3. **Given** an invalid or missing solution path, **When** I run the command, **Then** a clear error message is shown and the process exits with a non-zero code.

---

### User Story 2 - Architecture health gate in CI (Priority: P2)

As a team lead, I want to run `projgraph stats` in a CI pipeline to get a quick snapshot of solution structure, so I can track growth indicators over time and detect architectural drift early.

**Why this priority**: CI integration is a key stated motivator for this feature. The command must be reliable enough to script without needing diagram rendering.

**Independent Test**: Execute `projgraph stats <path>` in a CI script, capture the exit code, and verify it exits cleanly (code 0) when the solution is valid.

**Acceptance Scenarios**:

1. **Given** a CI pipeline runs `projgraph stats <solution>`, **When** the solution is valid, **Then** the command exits with code 0 and outputs stats to stdout.
2. **Given** the command runs in a non-interactive terminal, **When** output is captured, **Then** the text is human-readable and includes all key metrics.

---

### User Story 3 - AI assistant queries solution health via MCP (Priority: P3)

As an AI coding assistant, I want to call a `get_project_stats` MCP tool against a solution so I can receive structured metric data that I can use to reason about architecture and make recommendations, without needing to parse a diagram.

**Why this priority**: MCP exposure multiplies the usefulness of the stats feature for AI-driven workflows. It is independent of the CLI story and requires the stats analysis to already be working.

**Independent Test**: Call the `get_project_stats` MCP tool with a valid solution path and verify that the response contains structured fields for project count, type breakdown, dependency depth, and hotspot projects.

**Acceptance Scenarios**:

1. **Given** a valid solution path, **When** an AI assistant calls `get_project_stats`, **Then** the response is structured data containing project count, type breakdown, depth statistics, and ranked hotspot projects.
2. **Given** an invalid solution path, **When** the MCP tool is called, **Then** the tool returns a clear error message in the response payload.

---

### Edge Cases

- **Empty solution** (no projects): Output should clearly state zero projects were found rather than fail silently.
- **Solution with no inter-project dependencies**: Average depth should report as `0.0` (all projects are leaf nodes with no dependencies) rather than an error.
- **Projects with unrecognised output types**: Should be categorised as "Other" rather than dropped from the count.
- **Very large solutions (100+ projects)**: Analysis must complete within an acceptable time; the command should not hang.
- **Solution containing projects excluded by filters**: Stats should reflect only the analysed graph, not the raw file system.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept a path to a solution file or directory as the input to `projgraph stats`.
- **FR-002**: System MUST report the total number of projects in the solution.
- **FR-003**: System MUST categorise each project by output type (executable, library, test) and report the count per category.
- **FR-004**: System MUST calculate and report the average dependency depth across all projects in the solution graph.
- **FR-005**: System MUST identify and display the top most-depended-on projects, ranked by how many other projects directly reference them (direct in-degree).
- **FR-006**: System MUST output results in a human-readable format to stdout by default.
- **FR-007**: System MUST exit with a non-zero code when the input path is invalid or analysis fails.
- **FR-008**: System MUST expose solution metrics via an MCP tool named `get_project_stats`.
- **FR-009**: The `get_project_stats` MCP tool MUST return structured data containing all metrics from FR-002 through FR-005.
- **FR-010**: The `get_project_stats` MCP tool MUST return a descriptive error message when the provided path is invalid or analysis fails.
- **FR-011**: System MUST derive all metrics from the existing solution graph model without requiring source code analysis.

### Key Entities

- **SolutionStats**: Aggregated metrics snapshot for a solution — includes total project count, per-type counts, average dependency depth, and ranked hotspot list.
- **ProjectTypeBreakdown**: A grouping of projects by their output type (executable, library, test, other) with a count for each.
- **DependencyDepthStats**: The average, minimum, and maximum dependency depth values across all projects in the graph.
- **HotspotProject**: A project identified as heavily referenced — includes the project name and its in-degree count (number of projects that depend on it).

### MCP Tool Interface

- **Tool Name**: `get_project_stats`
- **Description**: Analyses a .NET solution or project file and returns key architectural metrics as structured data, including project counts by type, dependency depth statistics, and the most-referenced projects. Useful for AI assistants making architectural recommendations or monitoring solution health.
- **Parameters**:
  - `path`: (string) Absolute path to a solution file (`.sln` / `.slnx`) or a project file (`.csproj`).
  - `topN`: (integer, optional, default 5) Number of top most-referenced projects to include in the hotspot list.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `projgraph stats` completes analysis and displays output in under 5 seconds for solutions with up to 100 projects. *(Design goal — validated by manual profiling, not enforced by automated tests.)*
- **SC-002**: All project output types (executable, library, test) present in a solution are categorised correctly, with zero miscategorised projects for well-formed solution files.
- **SC-003**: The command exits with code 0 for all valid input paths and a non-zero code for all invalid paths.
- **SC-004**: The `get_project_stats` MCP tool returns all metrics (FR-002 through FR-005) in a machine-readable structure that requires no additional parsing by the caller.
- **SC-005**: Stats produced by the CLI command and the MCP tool are identical for the same input path.

## Assumptions

- Project type classification (executable, library, test) is determinable from the existing `SolutionGraph` model without additional file parsing or Roslyn analysis.
- Test projects are identified by a naming convention (e.g., contains `.Tests.`) or an existing project-type flag already present in the model; if neither is reliable, a suffix-match heuristic is acceptable.
- "Average dependency depth" is defined as the average of the longest path from each project to any transitive dependency (i.e., the critical path depth per project).
- "Most-depended-on" is measured by direct in-degree — the count of projects that directly reference each project; the top 5 are reported by default.
- The `--output` flag introduced in feature 007 is not required for this command in the initial implementation; stdout is sufficient.
- The stats command reuses the same solution-loading infrastructure as `projgraph visualize` — no new parsing layer is needed.
