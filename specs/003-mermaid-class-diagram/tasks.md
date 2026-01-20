# Tasks: Mermaid Class Diagram

**Input**: Design documents from `/specs/003-mermaid-class-diagram/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Register `get_class_diagram` tool schema in `src/ProjGraph.Mcp`
- [X] T002 [P] Create unit test project folders for the new feature in `tests/ProjGraph.Tests.Unit/Services/ClassAnalysis/`

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure and Library logic (Principle III)

- [X] T003 Create `ClassDiagramModels.cs` with `ClassModel`, `TypeDefinition`, etc., in `src/ProjGraph.Core/Models/`
- [X] T004 Define `IClassAnalysisService` interface in `src/ProjGraph.Lib/Interfaces/`
- [X] T005 Define `IClassDiagramRenderer` interface in `src/ProjGraph.Lib/Interfaces/`

**Checkpoint**: Foundation ready - models and interfaces established

---

## Phase 3: User Story 1 - Generate Diagram for a Specific File (Priority: P1) 🎯 MVP

**Goal**: Generate a basic Mermaid class diagram for types within a single provided `.cs` file.

**Independent Test**: Run `projgraph class-diagram path/to/file.cs` and verify Mermaid output contains classes from that file.

### Implementation for User Story 1

- [X] T007 [P] [US1] Implement `MermaidClassDiagramRenderer` in `src/ProjGraph.Lib/Rendering/MermaidClassDiagramRenderer.cs`
- [X] T008 [US1] Implement base `ClassAnalysisService.AnalyzeFileAsync` using Roslyn in `src/ProjGraph.Lib/Services/ClassAnalysis/ClassAnalysisService.cs`
- [X] T009 [US1] Implement `ClassDiagramCommand` using Spectre.Console in `src/ProjGraph.Cli/Commands/ClassDiagramCommand.cs`
- [X] T010 [US1] Register `ClassDiagramCommand` in `src/ProjGraph.Cli/Program.cs`
- [X] T011 [US1] Implement `get_class_diagram` tool handler in `src/ProjGraph.Mcp/Program.cs`
- [X] T012 [P] [US1] Add unit tests for Mermaid rendering in `tests/ProjGraph.Tests.Unit/Rendering/MermaidClassDiagramRendererTests.cs`
- [X] T013 [P] [US1] Add unit tests for single file analysis in `tests/ProjGraph.Tests.Unit/Services/ClassAnalysis/ClassAnalysisServiceTests.cs`

**Checkpoint**: User Story 1 functional - basic diagram generation working.

---

## Phase 4: User Story 2 - Discover Inheritance and Dependencies (Priority: P1)

**Goal**: Automatically find and include related types (base classes, interfaces, property types) from the workspace.

**Independent Test**: Run analysis on a class with a base class in another file and verify the relationship appears in the diagram.

### Implementation for User Story 2

- [X] T014 [US2] Implement Workspace discovery logic (heuristic search) in `src/ProjGraph.Lib/Services/ClassAnalysis/WorkspaceTypeDiscovery.cs`
- [X] T015 [US2] Update `ClassAnalysisService` to recursively add related types based on flags
- [X] T016 [US2] Add `--inheritance` and `--dependencies` options to `ClassDiagramCommand.Settings`
- [X] T017 [US2] Update `get_class_diagram` MCP tool handler to pass discovery flags
- [X] T018 [P] [US2] Add integration test for workspace discovery in `tests/ProjGraph.Tests.Integration/Cli/ClassDiagramDiscoveryTests.cs`

**Checkpoint**: User Story 2 functional - complex graphs can be generated via workspace search.

---

## Phase 5: User Story 3 - Visualizing Complex Relationships (Priority: P2)

**Goal**: Limit the depth of relationships discovered to prevent "diagram bloat".

**Independent Test**: Use `--depth 1` on a deep hierarchy and verify only immediate relatives are shown.

### Implementation for User Story 3

- [X] T019 [US3] Add depth tracking and limitation logic to `ClassAnalysisService`
- [X] T020 [US3] Add `--depth` option to CLI settings and MCP tool
- [X] T021 [P] [US3] Add unit test for depth-limited discovery in `tests/ProjGraph.Tests.Unit/Services/ClassAnalysis/ClassAnalysisDepthTests.cs`

**Checkpoint**: User Story 3 functional - users have control over diagram complexity.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T022 Handle generic type formatting (`~T~`) in `MermaidClassDiagramRenderer.cs`
- [X] T024 Ensure correct handling of external/unresolvable types as "opaque" nodes
- [X] T025 [P] Verify MCP contract compliance in `tests/ProjGraph.Tests.Contract/McpClassDiagramTests.cs`

**Final Checkpoint**: Feature complete, observable, and fully tested per constitution.
