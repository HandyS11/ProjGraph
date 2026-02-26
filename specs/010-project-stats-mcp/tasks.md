# Tasks: Solution Metrics Command (`projgraph stats`) and MCP Tool

**Input**: Design documents from `/specs/010-project-stats-mcp/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.
**Tests**: Tests are included (Constitution IV — Absolute Testing Requirement).
**MVP Scope**: Complete Phase 2 + Phase 3 (Foundational + US1) for a working `projgraph stats` command.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel with other [P] tasks in the same phase (different files, no shared dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths included in all descriptions

---

## Phase 1: Setup

**Purpose**: Confirm clean starting point — no new projects to scaffold.

- [ ] T001 Verify solution builds clean on feature branch: `dotnet build ProjGraph.slnx --no-restore`

---

## Phase 2: Foundational (Blocking Prerequisites for All User Stories)

**Purpose**: Domain models, core computation use case, service layer, and DI registration. All user stories depend on these.

- [ ] T002 [P] Create `SolutionStats`, `DependencyDepthStats`, `HotspotProject` records with `[JsonPropertyName]` attributes in `src/ProjGraph.Core/Models/SolutionStats.cs`
- [ ] T003 [P] Define `IStatsService` interface with `ComputeStatsAsync(string path, int topN, CancellationToken)` in `src/ProjGraph.Lib.ProjectGraph/Application/IStatsService.cs`
- [ ] T004 Implement `ComputeStatsUseCase` (topological-sort depth DP, in-degree ranking, Tarjan cycle detection via existing `TarjanSccAlgorithm`, type breakdown) in `src/ProjGraph.Lib.ProjectGraph/Application/UseCases/ComputeStatsUseCase.cs`
- [ ] T005 Implement `StatsService` wrapping `ComputeStatsUseCase` in `src/ProjGraph.Lib.ProjectGraph/Application/StatsService.cs`
- [ ] T006 Register `ComputeStatsUseCase`, `IStatsService`/`StatsService` in `src/ProjGraph.Lib.ProjectGraph/DependencyInjection.cs`

**Checkpoint**: `dotnet build` passes with zero warnings; `IStatsService` is resolvable via DI.

---

## Phase 3: User Story 1 — View Solution Metrics at a Glance (Priority: P1) 🎯 MVP

**Goal**: `projgraph stats <path>` outputs total project count, type breakdown, dependency depth stats, and top 5 hotspot projects in a readable terminal table.

**Independent Test**: Run `projgraph stats <path-to-ProjGraph.slnx>` and verify the output shows all four metric sections. No diagram rendering required.

### Tests for User Story 1

> **Write tests first — they must fail before implementation begins (Constitution IV)**

- [ ] T007 [P] [US1] Unit tests for `ComputeStatsUseCase`: type breakdown counts, average/min/max depth, in-degree ranking, empty-graph edge case, cycle detection returns `HasCycles=true` — in `tests/ProjGraph.Tests.Unit.ProjectGraph/ComputeStatsUseCaseTests.cs`
- [ ] T008 [P] [US1] Unit tests for `StatsService`: delegates to use case, passes `topN` and `cancellationToken` correctly — in `tests/ProjGraph.Tests.Unit.ProjectGraph/StatsServiceTests.cs`

### Implementation for User Story 1

- [ ] T009 [US1] Implement `StatsCommand` (Spectre.Console table + rule output, path argument, optional `--top` option defaulting to 5) in `src/ProjGraph.Cli/Commands/StatsCommand.cs`
- [ ] T010 [US1] Register `stats` command and add CLI examples in `src/ProjGraph.Cli/Program.cs`
- [ ] T011 [US1] CLI integration test for a valid solution path: verifies output contains project count, type labels, depth stats, and hotspot section; records elapsed time to manually validate SC-001 (<5s) — in `tests/ProjGraph.Tests.Integration.Cli/StatsCommandIntegrationTests.cs`

**Checkpoint**: `projgraph stats ProjGraph.slnx` prints a metrics table; all T007/T008 unit tests and T011 integration test pass.

---

## Phase 4: User Story 2 — Architecture Health Gate in CI (Priority: P2)

**Goal**: Command exits with code 0 on success and non-zero on any failure (invalid path, parse error). Output is unambiguous plain text when captured in CI.

**Independent Test**: `projgraph stats /does/not/exist.slnx` exits with a non-zero code and a descriptive error message on stderr.

### Tests for User Story 2

- [ ] T012 [P] [US2] Integration tests for error cases: invalid path exits non-zero, empty solution prints zero-project summary and exits 0 — add to `tests/ProjGraph.Tests.Integration.Cli/StatsCommandIntegrationTests.cs`

### Implementation for User Story 2

- [ ] T013 [US2] Add `try/catch` error handling to `StatsCommand.ExecuteAsync` — invalid/missing path shows a clear stderr message and returns exit code 1; update `src/ProjGraph.Cli/Commands/StatsCommand.cs`

**Checkpoint**: All T012 error-case tests pass; `echo $LASTEXITCODE` (or `$?`) correctly reflects success/failure in a terminal script.

---

## Phase 5: User Story 3 — AI Assistant Queries Solution Health via MCP (Priority: P3)

**Goal**: `get_project_stats` MCP tool returns JSON-serialised `SolutionStats` for a valid path; returns an error message for an invalid path. Matches the schema in `contracts/get_project_stats.json`.

**Independent Test**: Call `get_project_stats` through the MCP contract test harness with a valid solution path and verify all top-level JSON keys are present and correctly typed.

### Tests for User Story 3

- [ ] T014 [P] [US3] MCP contract tests: verify `GetProjectStatsAsync` method exists, has `[McpServerTool(Name = "get_project_stats")]` attribute, has `[Description]` attribute mentioning "metrics", accepts `path` (required string) and `topN` (optional int, default 5) parameters — in `tests/ProjGraph.Tests.Contract/McpProjectStatsContractTests.cs`

### Implementation for User Story 3

- [ ] T015 [US3] Add `IStatsService` to `ProjGraphTools` constructor parameter list in `src/ProjGraph.Mcp/Program.cs`
- [ ] T016 [US3] Add `GetProjectStatsAsync` to `ProjGraphTools` in `src/ProjGraph.Mcp/Program.cs`: calls `IStatsService.ComputeStatsAsync`, serialises result with `JsonSerializer.Serialize`, throws `FileNotFoundException` on invalid path (consistent with other tools)

**Checkpoint**: All T014 contract tests pass; calling the tool via the MCP test harness returns valid JSON with all fields from `contracts/get_project_stats.json`.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: XML documentation, README updates.

- [ ] T017 [P] Add XML `<summary>` and `<param>` doc comments to all public members in `src/ProjGraph.Core/Models/SolutionStats.cs` (zero-warning requirement)
- [ ] T018 [P] Add XML doc comments to `src/ProjGraph.Lib.ProjectGraph/Application/IStatsService.cs`, `StatsService.cs`, and `UseCases/ComputeStatsUseCase.cs`
- [ ] T019 [P] Add XML doc comments to `src/ProjGraph.Cli/Commands/StatsCommand.cs` and the new `GetProjectStatsAsync` method in `src/ProjGraph.Mcp/Program.cs`

**Final Checkpoint**: `dotnet build ProjGraph.slnx` — zero warnings; `dotnet test` — all tests green.

---

## Dependency Graph

```none
T001
  └── T002, T003 (parallel — different files)
        └── T004 (uses SolutionStats from T002, implements IStatsService contract from T003)
              └── T005 (wraps T004)
                    └── T006 (DI registration)
                          ├── T007, T008 (unit tests — parallel)
                          ├── T009 (StatsCommand — depends on SolutionStats type)
                          │     └── T010 (CLI wiring — depends on StatsCommand class)
                          │           └── T011 (integration test — depends on CLI working)
                          │                 └── T012, T013 (CI error handling — builds on T009/T011)
                          └── T014 (MCP contract test — depends on ProjGraphTools type existing)
                                └── T015 (constructor wiring — inject IStatsService)
                                      └── T016 (MCP tool impl — depends on IStatsService in constructor)
T017, T018, T019 (polish — parallel, no functional dependencies)
```

## Parallel Execution Examples

### Foundational phase — after T001

Run **T002** and **T003** simultaneously (different new files).

### US1 tests — after T006

Run **T007** and **T008** simultaneously (different test files, no shared state).

### Polish — after T016

Run **T017**, **T018**, and **T019** simultaneously (non-overlapping files).

## Implementation Strategy

1. **MVP First**: Phases 2 + 3 (T001–T011) deliver a complete, usable `projgraph stats` command. Ship this increment first.
2. **Incremental Delivery**:
   - Add Phase 4 (T012–T013) to harden CI use case — small delta on top of US1.
   - Add Phase 5 (T014–T016) for MCP exposure — fully independent, reuses the service already built.
3. **Test-First**: Write T007/T008 before T009/T010; write T014 before T015/T016.
