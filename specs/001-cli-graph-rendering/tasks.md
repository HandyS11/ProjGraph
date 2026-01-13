# Tasks: CLI Graph Rendering & MCP Hub

**Input**: Design documents from `/specs/001-cli-graph-rendering/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/mcp-tools.json](contracts/mcp-tools.json)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and base architecture setup.

- [x] T001 Create repository solution file and project folders for ProjGraph
- [x] T002 [P] Initialize .NET 10 project files in `src/ProjGraph.Core/`, `src/ProjGraph.Lib/`, `src/ProjGraph.Cli/`, and `src/ProjGraph.Mcp/`
- [x] T003 [P] Add primary package dependencies to project files (Buildalyzer, Spectre.Console, ModelContextProtocol SDK)
- [x] T004 Setup global EditorConfig and Solution-level project properties for strict code quality

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core entities and shared cross-cutting concerns (Principle III).

- [x] T005 Implement Project, Dependency, and Solution entities in `src/ProjGraph.Core/Models/`
- [x] T006 Implement IDependencyGraph interface and Tarjan's SCC algorithm logic in `src/ProjGraph.Lib/Algorithms/`
- [x] T007 [P] Implement structured logging and OpenTelemetry activity sources in `src/ProjGraph.Core/Diagnostics/`
- [x] T008 [P] Initialize xUnit test projects for Unit, Integration, and Contract tests in `tests/`

**Checkpoint**: Core models and graph algorithms are established and testable.

---

## Phase 3: User Story 1 - CLI Visualizer (Priority: P1) 🎯 MVP

**Goal**: Render a .NET dependency graph directly in the terminal from .sln or .csproj files.

**Independent Test**: Running `projgraph visualize <path-to-sln>` produces a colorized Spectre.Console Tree representation showing dependencies.

### Tests for User Story 1

- [x] T009 [P] [US1] Create unit tests for .csproj and legacy .sln parsing in `tests/ProjGraph.Tests.Unit/`
- [x] T010 [P] [US1] Create integration tests for CLI command execution in `tests/ProjGraph.Tests.Integration/`

### Implementation for User Story 1

- [x] T011 [US1] Implement MSBuild-based project reference extractor in `src/ProjGraph.Lib/Parsers/ProjectParser.cs`
- [x] T012 [US1] Implement legacy Solution parser (Regex/MSBuild) in `src/ProjGraph.Lib/Parsers/SolutionParser.cs`
- [x] T013 [P] [US1] Create CLI entry point and 'visualize' command using System.CommandLine in `src/ProjGraph.Cli/`
- [x] T014 [US1] Implement Spectre.Console Tree rendering logic in `src/ProjGraph.Cli/Rendering/`
- [x] T015 [US1] Integrate cycle detection and highlight cyclic nodes in `src/ProjGraph.Cli/Rendering/`
- [x] T016 [US1] Implement formatted Mermaid.js text export option in `src/ProjGraph.Cli/Commands/`

**Checkpoint**: CLI Visualization is fully functional for legacy solutions and projects.

---

## Phase 4: User Story 2 - MCP Tool (Priority: P2)

**Goal**: Expose the dependency graph extraction as an automated tool for AI agents.

**Independent Test**: Use an MCP client to call `get_project_graph` with a valid path and receive a structured JSON response of nodes and edges.

### Tests for User Story 2

- [x] T017 [P] [US2] Create MCP Contract tests to validate tool schema compliance in `tests/ProjGraph.Tests.Contract/`
- [x] T018 [P] [US2] Create integration tests for MCP Server tool invocation in `tests/ProjGraph.Tests.Integration/`

### Implementation for User Story 2

- [x] T019 [US2] Setup MCP Server host with Standard Input/Output transport in `src/ProjGraph.Mcp/`
- [x] T020 [US2] Implement tool handler for `get_project_graph` in `src/ProjGraph.Mcp/Tools/`
- [x] T021 [US2] Implement JSON serialization DTOs for the graph data in `src/ProjGraph.Mcp/Dto/`

**Checkpoint**: ProjGraph is now an interoperable MCP Server.

---

## Phase 5: User Story 3 - .slnx Support (Priority: P3)

**Goal**: Support the modern XML-based .slnx solution format.

**Independent Test**: Running the tool against a `.slnx` file correctly identifies all projects and their relationships.

### Tests for User Story 3

- [x] T022 [P] [US3] Create unit tests for .slnx XML parsing in `tests/ProjGraph.Tests.Unit/`

### Implementation for User Story 3

- [x] T023 [US3] Implement XML-based .slnx parser in `src/ProjGraph.Lib/Parsers/SlnxParser.cs`
- [x] T024 [US3] Integrate SlnxParser into the unified parsing entry point in `src/ProjGraph.Lib/`

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Final quality assurance and documentation.

- [ ] T025 Finalize XML documentation comments for automated documentation generation
- [ ] T026 Add final performance benchmarks for 50+ project solutions in `tests/ProjGraph.Tests.Integration/`

## Dependency Graph

```text
Foundational (T005-T008)
      ↓
User Story 1 (T009-T016)  ← [MVP]
      ↓
User Story 2 (T017-T021)
      ↓
User Story 3 (T022-T024)
      ↓
Phase 6: Polish
```

## Parallel Execution Examples

- **Core & Foundational**: T002, T003, T007, T008 can all be performed simultaneously after T001.
- **Story Development**: T011 and T013 can be started in parallel once models (T005) are ready.
- **Contract Work**: MCP Schema/Contract tests (T017) can be written alongside CLI implementation (T014).

## Implementation Strategy

1. **MVP First**: Complete Phase 1-3 to deliver a working CLI visualization tool.
2. **Incremental Delivery**: Phase 4 enables AI assistance. Phase 5 adds modern format support.
3. **Quality**: Tests (Phase 1-2 projects) must be established before logic implementation for each story.
