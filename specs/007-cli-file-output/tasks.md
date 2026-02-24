# Tasks: File Output (`--output` flag)

**Input**: Design documents from `/specs/007-cli-file-output/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- All descriptions include exact file paths

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Update core abstractions to support file writing

- [X] T001 Add `WriteAllTextAsync` to `IFileSystem` in `src/ProjGraph.Lib.Core/Abstractions/IFileSystem.cs`
- [X] T002 Add `CreateDirectory` to `IFileSystem` in `src/ProjGraph.Lib.Core/Abstractions/IFileSystem.cs`
- [X] T003 Implement `WriteAllTextAsync` (ensuring UTF-8 without BOM) and `CreateDirectory` in `src/ProjGraph.Lib.Core/Infrastructure/PhysicalFileSystem.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Ensure file system abstractions are working correctly

- [X] T004 [P] Add unit tests for `IFileSystem.WriteAllTextAsync` (verifying UTF-8 without BOM) and `CreateDirectory` in `tests/ProjGraph.Tests.Unit.Core/PhysicalFileSystemTests.cs`

---

## Phase 3: User Story 1 - Save diagram to file (Priority: P1) 🎯 MVP

**Goal**: Enable saving the dependency graph, ERD, and class diagrams to a file.

**Independent Test**: Run `projgraph visualize <path> -o output.mmd` and verify the file exists with the mermaid diagram.

### Implementation for User Story 1

- [X] T005 [P] [US1] Add `Output` property to `VisualizeCommand.Settings` in `src/ProjGraph.Cli/Commands/VisualizeCommand.cs`
- [X] T006 [US1] Update `VisualizeCommand.ExecuteAsync` to handle file output and markdown fencing in `src/ProjGraph.Cli/Commands/VisualizeCommand.cs`
- [X] T007 [P] [US1] Add `Output` property to `ErdCommand.Settings` in `src/ProjGraph.Cli/Commands/ErdCommand.cs`
- [X] T008 [US1] Update `ErdCommand.ExecuteAsync` to handle file output and markdown fencing in `src/ProjGraph.Cli/Commands/ErdCommand.cs`
- [X] T009 [P] [US1] Add `Output` property to `ClassDiagramCommand.Settings` in `src/ProjGraph.Cli/Commands/ClassDiagramCommand.cs`
- [X] T010 [US1] Update `ClassDiagramCommand.ExecuteAsync` to handle file output and markdown fencing in `src/ProjGraph.Cli/Commands/ClassDiagramCommand.cs`

**Checkpoint**: Core commands now support `--output`.

---

## Phase 4: User Story 2 - CI/CD Integration (Priority: P2)

**Goal**: Ensure reliable file creation for automation.

**Independent Test**: Use the `--output` flag with different file extensions (`.md` vs `.mmd`) and verify markdown fence presence/absence.

### Tests for User Story 2

- [X] T011 [P] [US2] Create integration tests for file output in `tests/ProjGraph.Tests.Integration.Cli/VisualizeCliTests.cs`
- [X] T012 [P] [US2] Create integration tests for file output in `tests/ProjGraph.Tests.Integration.Cli/ErdCliTests.cs`
- [X] T013 [P] [US2] Create integration tests for file output in `tests/ProjGraph.Tests.Integration.Cli/ClassDiagramCliTests.cs`

---

## Phase 5: User Story 3 - Feedback on success (Priority: P2)

**Goal**: Give users clear feedback when a file is saved.

**Independent Test**: Verify "Saved to <path>" appears in the console when using `-o`.

### Implementation for User Story 3

- [X] T014 [US3] Ensure success confirmation message is printed in all commands in `src/ProjGraph.Cli/Commands/`

---

## Phase 6: Polish

- [X] T015 Final verification of command help text for `-o|--output` flag.
- [X] T016 Manual validation of error handling (e.g., read-only file system).

## Dependencies

- Phase 1 must be completed before Phase 3 (needed for file writing).
- Phase 3 can run mostly in parallel (different commands).
- User stories depend on Phase 1 & 2 foundations.

## Parallel Execution Opportunities

- T005, T007, T009 (updating Settings across 3 files)
- T011, T012, T013 (writing integration tests for 3 different commands)
- Implementation tasks for different commands (T006, T008, T010) are independent.
