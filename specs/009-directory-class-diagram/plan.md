# Implementation Plan: 009-directory-class-diagram

**Branch**: `009-directory-class-diagram` | **Date**: 2026-02-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/009-directory-class-diagram/spec.md`

## Summary

Accept a directory path as input for class diagram generation. ProjGraph will recursively scan the directory for all `.cs` files (excluding standard noise folders like `bin`, `obj`, `.git`, `node_modules`), parse them, and build a single combined Mermaid class diagram. This allows users to visualize entire folders or domains at a glance.

## Technical Context

**Language/Version**: .NET 10.0 (C# 14+)
**Primary Dependencies**: Microsoft.CodeAnalysis.CSharp (Roslyn)
**Storage**: N/A (Memory analysis)
**Testing**: xUnit, FluentAssertions, Moq. Existing unit tests in `ProjGraph.Tests.Unit.ClassDiagram`.
**Target Platform**: .NET Core (Cross-platform)
**Project Type**: Library (ProjGraph.Lib.ClassDiagram), CLI (ProjGraph.Cli), MCP Server (ProjGraph.Mcp)
**Performance Goals**: < 10 seconds for 20+ `.cs` files (SC-001).
**Constraints**: Recursion by default (FR-003), Factorized exclusions (FR-007, using `DirectoryFilters`).
**Scale/Scale**: Limit to 50 files before warning (FR-008).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modern .NET 10 Baseline**: Targeting .NET 10.0+?
- [x] **II. MCP Native Interoperability**: Tool functionality exposed via MCP?
- [x] **III. Library-First Core**: Logic in libraries, not just CLI/MCP?
- [x] **IV. Absolute Testing Requirement**: Tests planned for unit, integration, and MCP contract?

## Project Structure

### Documentation (this feature)

```text
specs/009-directory-class-diagram/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command - MUST include MCP schema)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── ProjGraph.Core/      # Shared Domain Models & Constants
├── ProjGraph.Lib.ClassDiagram/ # Class Diagram generation logic
├── ProjGraph.Cli/       # Thin CLI tool wrapper
└── ProjGraph.Mcp/       # MCP Server interface
```

tests/
├── ProjGraph.Tests.Contract/ # MCP Contact Tests
├── ProjGraph.Tests.Integration.Cli/ # CLI Integration Tests
├── ProjGraph.Tests.Unit.ClassDiagram/ # Library Logic Unit Tests

**Structure Decision**: No new projects required. Logic will be added to `ProjGraph.Lib.ClassDiagram` and exposed via existing `ClassAnalysisService`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
| ----------- | ------------ | ------------------------------------- |
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
