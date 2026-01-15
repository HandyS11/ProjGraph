# Tasks: DbContext ERD Generation

**Input**: Design documents from \/specs/002-dbcontext-erd/\
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Format: \[ID] [P?] [Story] Description\

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Add \Microsoft.CodeAnalysis.CSharp\, \Microsoft.CodeAnalysis.Workspaces.MSBuild\, and \Microsoft.EntityFrameworkCore\ to \Directory.Packages.props\
- [x] T002 Add package references for Roslyn and EF Core to \src/ProjGraph.Lib/ProjGraph.Lib.csproj\
- [x] T003 Update [contracts/mcp-tools.json](contracts/mcp-tools.json) with refined \get_erd\ schema if needed

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure and Library logic (Principle III)

- [x] T004 Create base ERD models \EfModel\, \EfEntity\, \EfProperty\, \EfRelationship\ in \src/ProjGraph.Core/Models/EfModel.cs\
- [x] T005 Implement \MermaidErdRenderer\ for converting \EfModel\ to Mermaid string in \src/ProjGraph.Lib/Rendering/MermaidErdRenderer.cs\
- [x] T006 Create \IEfAnalysisService\ interface in \src/ProjGraph.Lib/Services/IEfAnalysisService.cs\

**Checkpoint**: Foundation ready - models and output rendering established

---

## Phase 3: User Story 1 - Generate ERD from Solution (Priority: P1) 🎯 MVP

**Goal**: Automatically scan all projects in a solution to find and visualize DbContexts.

**Independent Test**: Running \projgraph erd --path ./MySolution.sln\ produces a valid Mermaid ERD for a solution with multiple projects.

### Implementation for User Story 1

- [x] T007 [US1] Implement \DbContext\ discovery logic for MSBuild solutions in \src/ProjGraph.Lib/Services/EfAnalysisService.cs\
- [x] T008 [US1] Implement \DbSet\ and entity type identification via Roslyn symbols in \src/ProjGraph.Lib/Services/EfAnalysisService.cs\
- [x] T009 [US1] Implement Relationship extraction logic (1:1, 1:N, N:M) using Roslyn symbols in \src/ProjGraph.Lib/Services/EfAnalysisService.cs\
- [x] T010 [US1] Implement shadow join table detection for N:M relationships in \src/ProjGraph.Lib/Services/EfAnalysisService.cs\
- [x] T011 [P] [US1] Create unit tests for solution-wide EF model extraction in \tests/ProjGraph.Tests.Unit/Services/EfAnalysisServiceTests.cs\
- [x] T012 [US1] Register \EfAnalysisService\ and implement \erd\ command with \--path\ support in \src/ProjGraph.Cli/Commands/ErdCommand.cs\

**Checkpoint**: User Story 1 (Solution scanning) is fully functional

---

## Phase 4: User Story 2 - Generate ERD from Specific File (Priority: P1)

**Goal**: Generate a diagram for a specific .cs file containing a DbContext.

**Independent Test**: Running \projgraph erd --file ./Data/AppDbContext.cs\ produces an ERD based only on that file's content.

### Implementation for User Story 2

- [x] T013 [US2] Extend \EfAnalysisService\` to support parsing standalone syntax trees in \src/ProjGraph.Lib/Services/EfAnalysisService.cs\
- [x] T014 [US2] Update \erd\ command to support the \--file\ argument in \src/ProjGraph.Cli/Commands/ErdCommand.cs\
- [x] T015 [P] [US2] Add unit tests for single-file EF model extraction in \tests/ProjGraph.Tests.Unit/Services/EfAnalysisServiceTests.cs\

**Checkpoint**: User Story 2 (File analysis) is fully functional

---

## Phase 5: User Story 3 - AI-Assisted Schema Analysis via MCP (Priority: P2)

**Goal**: Expose the ERD generation functionality to AI assistants via MCP.

**Independent Test**: Calling the \get_erd\ tool via an MCP client returns the Mermaid diagram for a given project path.

### Implementation for User Story 3

- [x] T016 [US3] Implement the \get_erd\ tool in \src/ProjGraph.Mcp/ProjGraphTools.cs\ calling \EfAnalysisService\
- [x] T017 [P] [US3] Create contract tests for the \get_erd\ tool schema in \tests/ProjGraph.Tests.Contract/McpErdContractTests.cs\
- [x] T018 [US3] Implement integration tests for the MCP ERD tool in \tests/ProjGraph.Tests.Integration/Mcp/McpErdTests.cs\

**Checkpoint**: All user stories are functional and exposed via MCP

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T019 Add interactive selection prompt and path fallback in \src/ProjGraph.Cli/Commands/ErdCommand.cs\
- [x] T020 [P] Add OpenTelemetry tracing and spans to EF extraction logic in \src/ProjGraph.Lib/Services/EfAnalysisService.cs\
- [x] T021 Final verification of \quickstart.md\ scenarios and documentation updates

---

## Dependencies & Execution Order

### User Story Dependencies

- **Foundational (Phase 2)** must be completed before any User Story.
- **User Story 1** and **User Story 2** can be implemented in any order but both are marked P1.
- **User Story 3** depends on the service logic from US1/US2 being stable.

### Parallel Opportunities

- T011, T015, T017 (Tests) can be developed alongside their respective implementation tasks if split by different files.
- T005 (Renderer) can be developed in parallel with T004 (Models).
- T020 (Tracing) can be added as a polish task.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Setup package dependencies.
2. Implement Core Models and Renderer.
3. Complete Solution-wide scanning (US1) and CLI command.
4. Verify with integrated test.

### Incremental Delivery

- Add single file support (US2).
- Expose via MCP (US3).
- Finalize with tracing and UX polish (Phase 6).
