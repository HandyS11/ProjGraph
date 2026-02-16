# Tasks: Configure Class Member Visibility

**Feature**: [spec.md](spec.md)
**Plan**: [plan.md](plan.md)

## Phase 1: Setup

- [x] T001 Register `IncludeProperties` and `IncludeFunctions` fields in `src/ProjGraph.Lib.ClassDiagram/Application/AnalysisOptions.cs`
- [x] T002 Update `GetClassDiagramAsync` parameters and description in `src/ProjGraph.Mcp/Program.cs`
- [x] T003 Update `IClassAnalysisService.AnalyzeFileAsync` interface to include new visibility flags in `src/ProjGraph.Lib.ClassDiagram/Application/IClassAnalysisService.cs`
- [x] T004 Update `ClassAnalysisService.AnalyzeFileAsync` implementation in `src/ProjGraph.Lib.ClassDiagram/Application/ClassAnalysisService.cs`

## Phase 2: Foundational

- [x] T005 Update `AnalyzeFileUseCase.ExecuteAsync` to accept and set new options in `src/ProjGraph.Lib.ClassDiagram/Application/UseCases/AnalyzeFileUseCase.cs`
- [x] T006 [P] Update `TypeAnalyzer.AnalyzeType` signature to accept `includeProperties` and `includeFunctions` flags in `src/ProjGraph.Lib.ClassDiagram/Infrastructure/TypeAnalyzer.cs`
- [x] T007 [P] Update `TypeProcessor.ProcessTypeQueueInternalAsync` to pass flags from options to `AnalyzeType` in `src/ProjGraph.Lib.ClassDiagram/Infrastructure/TypeProcessor.cs`

## Phase 3: User Story 1 - High-Level Architecture View (Priority: P1)

Goal: Generate diagrams with no internal members.
Independent Test: Run `GetClassDiagram` with `includeProperties=false` and `includeFunctions=false` and verify empty class boxes.

- [x] T008 [US1] Implement member filtering logic in `TypeAnalyzer.AnalyzeType`: skip Property/Field members when `includeProperties=false`, skip Method members when `includeFunctions=false` in `src/ProjGraph.Lib.ClassDiagram/Infrastructure/TypeAnalyzer.cs`
- [x] T009 [P] [US1] Create unit test case for fully hidden members in `tests/ProjGraph.Tests.Unit.ClassDiagram/TypeAnalyzerTests.cs`
- [x] T010 [US1] Create unit test verifying rendered Mermaid output still contains relationship arrows (`-->`, `<|--`) when members are hidden via `includeProperties=false` and `includeFunctions=false`
- [x] T011 [US1] Create contract test for MCP tool with member visibility toggles in `tests/ProjGraph.Tests.Contract/McpClassDiagramTests.cs`

## Phase 4: User Story 2 - Behavioral Focus (Priority: P2)

Goal: Show only functions/methods in the class diagram.
Independent Test: Run with `includeProperties=false` and verify only methods are visible.

- [x] T012 [P] [US2] Create unit test case for "Functions Only" visibility in `tests/ProjGraph.Tests.Unit.ClassDiagram/TypeAnalyzerTests.cs`
- [x] T013 [US2] Verify that inheritance relationships or other types remain visible in the diagram even if members are hidden in `tests/ProjGraph.Tests.Unit.ClassDiagram/TypeAnalyzerTests.cs`

## Phase 5: User Story 3 - Data Focus (Priority: P3)

Goal: Show only properties/fields in the class diagram.
Independent Test: Run with `includeFunctions=false` and verify only properties are visible.

- [x] T014 [P] [US3] Create unit test case for "Properties Only" visibility in `tests/ProjGraph.Tests.Unit.ClassDiagram/TypeAnalyzerTests.cs`
- [x] T015 [US3] Verify that dependency relationships (Associations) are still discovered correctly from hidden fields/properties in `tests/ProjGraph.Tests.Unit.ClassDiagram/TypeAnalyzerTests.cs`

## Final Phase: Polish

- [x] T016 [US1] Create unit test for empty class (no members, both toggles true) renders correctly as empty class box
- [x] T017 Create regression test: call `GetClassDiagram` without new parameters and assert output is identical to current baseline (SC-002)
- [x] T018 Verify all tests pass and check for zero warnings in the solution
- [x] T019 Validate SC-004 by generating a diagram for `samples/classdiagram/complex-hierarchy/` with and without members and comparing character counts (expect >= 50% reduction)
- [x] T020 Update `ClassDiagramCommand` to support and pass member visibility flags in `src/ProjGraph.Cli/Commands/ClassDiagramCommand.cs`
- [x] T021 Add CLI integration tests for member visibility in `tests/ProjGraph.Tests.Integration.Cli/ClassDiagramCommandTests.cs`
