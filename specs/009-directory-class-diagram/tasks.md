# Tasks: Folder/Directory Scanning for classdiagram

**Input**: Design documents from `/specs/009-directory-class-diagram/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **- [ ]**: Task checkbox
- **[ID]**: Task ID (T001, T002...)
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story label (US1, US2, etc.)

---

## Phase 1: Setup

**Purpose**: Shared infrastructure and foundational refactoring.

- [ ] T001 Factorize standard directory exclusions in `src/ProjGraph.Lib.Core/Infrastructure/DirectoryFilters.cs` to ensure consistency across the codebase.
- [ ] T002 [P] Create `DiscoverCsFilesUseCase` in `src/ProjGraph.Lib.ClassDiagram/Application/UseCases/DiscoverCsFilesUseCase.cs` for recursive directory scanning with exclusion logic.
- [ ] T003 [P] Add unit tests for `DiscoverCsFilesUseCase` in `tests/ProjGraph.Tests.Unit.ClassDiagram/Application/UseCases/DiscoverCsFilesUseCaseTests.cs`.

## Phase 2: Foundational

**Purpose**: Core logic and service updates for multi-file analysis.

- [ ] T004 Update `IClassAnalysisService` in `src/ProjGraph.Lib.ClassDiagram/Application/IClassAnalysisService.cs` to include `AnalyzeDirectoryAsync`.
- [ ] T005 Implement `AnalyzeDirectoryUseCase` in `src/ProjGraph.Lib.ClassDiagram/Application/UseCases/AnalyzeDirectoryUseCase.cs` to orchestrate multi-file scanning and unified Roslyn compilation.
- [ ] T006 Update `ClassAnalysisService` in `src/ProjGraph.Lib.ClassDiagram/Application/ClassAnalysisService.cs` to delegate to `AnalyzeDirectoryUseCase`.
- [ ] T007 [P] Add unit tests for `AnalyzeDirectoryUseCase` in `tests/ProjGraph.Tests.Unit.ClassDiagram/Application/UseCases/AnalyzeDirectoryUseCaseTests.cs`.
  - **Verification**: Explicitly test "Duplicate Class Names" edge case (partial classes across files, and same class name in different namespaces).

## Phase 3: User Story 1 - Folder-based Class Diagram (Priority: P1)

**Story Goal**: Users can generate a combined diagram from a folder path.
**Independent Test**: Provide a folder with related types and verify the output Mermaid matches expected relationships.

- [ ] T008 [US1] Update `ClassDiagramCommand` settings in `src/ProjGraph.Cli/Commands/ClassDiagramCommand.cs` to accept directory paths and remove strict `.cs` extension validation if it is a directory.
- [ ] T009 [US1] Implement directory vs file detection logic in `ClassDiagramCommand.ExecuteAsync` and call appropriate service methods.
- [ ] T010 [US1] Implement the 50-file warning threshold in `ClassDiagramCommand` or `AnalyzeDirectoryUseCase` per `FR-008`.
- [ ] T011 [P] [US1] Add integration test for directory-based diagram generation in `tests/ProjGraph.Tests.Integration.Cli/ClassDiagramCommandTests.cs`.

## Phase 4: User Story 2 - Recursive Directory Scanning (Priority: P2)

**Story Goal**: Nested subfolders are scanned and included by default.
**Independent Test**: Provide a directory with nested folders and verify types from all levels are in the final diagram.

- [ ] T012 [US2] Verify recursive scanning behavior in `DiscoverCsFilesUseCase` and ensure nested `.cs` files are correctly aggregated.
- [ ] T013 [P] [US2] Add integration test for recursive nested folder scanning in `tests/ProjGraph.Tests.Integration.Cli/ClassDiagramCommandTests.cs`.

## Phase 5: User Story 3 - Mixed Input Validation (Priority: P3)

**Story Goal**: Automatic detection of file vs directory without user flags.
**Independent Test**: Run CLI with both types of paths and ensure both work correctly.

- [ ] T014 [US3] Refine input path validation in `ClassDiagramCommand` to support both existing file paths and new directory paths seamlessly.
- [ ] T015 [P] [US3] Add unit tests for path validation logic in `tests/ProjGraph.Tests.Unit.Cli/Commands/ClassDiagramCommandTests.cs`.

## Phase 6: MCP Server Integration

**Story Goal**: Expose directory scanning via MCP tool.
**Independent Test**: Call `get_class_diagram` tool with a directory path via MCP inspector or client.

- [ ] T016 Update `ProjGraphTools.GetClassDiagramAsync` in `src/ProjGraph.Mcp/Program.cs` to handle directory paths using `AnalyzeDirectoryAsync`.
- [ ] T017 [P] Add contract tests for directory-based MCP calls in `tests/ProjGraph.Tests.Contract/McpClassDiagramTests.cs`.

## Phase 7: Polish & Documentation

- [ ] T018 Update `ARCHITECTURE.md` to reflect the new directory scanning use cases and factorization.
- [ ] T019 Update CLI help text and documentation in `docfx/` for the `classdiagram` command.
- [ ] T020 [SC-001] Performance manual validation: Run `classdiagram` on a folder with 20+ `.cs` files (e.g., `src/ProjGraph.Core/Models/`) and verify execution time is < 10 seconds.
- [ ] T021 [SC-004] Regression test: Run `samples/regenerate-samples.ps1` and verify that single-file output diagrams remain identical to current versions.

---

## Dependencies

- Phase 2 depends on Phase 1 (Core refactoring needs to be complete).
- Phase 3 depends on Phase 2 (CLI needs the service method).
- Phase 6 depends on Phase 2 (MCP needs the service method).

## Parallel Execution Examples

- T002, T003 can run together once T001 is fixed.
- T007, T011, T013 can be implemented in parallel with the respective execution tasks.
- T017 (MCP) can run in parallel with CLI implementation tasks once Phase 2 is done.

## Implementation Strategy

1. **MVP First**: Focus on T001-T011 to deliver a working CLI command for directory scanning (US1).
2. **Incremental**: Add recursions (US2) and MCP support (Phase 6) as secondary increments.
3. **Refactor**: Ensure standard directory filters are used across all scanning logic during Phase 1.
