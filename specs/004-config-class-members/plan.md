# Implementation Plan: Configure Class Member Visibility

**Branch**: `004-config-class-members` | **Date**: 2026-02-16 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/004-config-class-members/spec.md`

## Summary

This feature improves the `GetClassDiagram` tool by allowing users to toggle the visibility of class properties and functions. The goal is to provide cleaner, more focused diagrams for large codebases. The technical approach involves extending `AnalysisOptions` in `ProjGraph.Lib.ClassDiagram` and updating `TypeAnalyzer` to filter members before building the model, while maintaining relationship discovery in `TypeProcessor`.

## Technical Context

**Language/Version**: .NET 10.0 (C# 14+)
**Primary Dependencies**: Microsoft.CodeAnalysis.CSharp (Roslyn), ProjGraph.Lib.Core
**Storage**: N/A
**Testing**: xUnit, FluentAssertions
**Target Platform**: .NET Core (Windows, Linux, macOS)
**Project Type**: Library/MCP Server
**Performance Goals**: < 5s for medium-sized workspaces; SC-004: > 50% character reduction in diagrams when members are hidden.
**Constraints**: MCP 1.0 Compliance, Zero Warnings, Strict SemVer
**Scale/Scope**: Workspace-wide class analysis and rendering.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modern .NET 10 Baseline**: Targeting .NET 10.0+?
- [x] **II. MCP Native Interoperability**: Tool functionality exposed via MCP?
- [x] **III. Library-First Core**: Logic in libraries, not just CLI/MCP?
- [x] **IV. Absolute Testing Requirement**: Tests planned for unit, integration, and MCP contract?

## Project Structure

### Documentation (this feature)

```text
specs/004-config-class-members/
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
├── ProjGraph.Core/      # Shared Domain Models (MemberDefinition, MemberKind)
├── ProjGraph.Lib.ClassDiagram/ # Core Analysis and Rendering Logic
└── ProjGraph.Mcp/       # MCP Server interface (Tool parameters)
```

tests/
├── ProjGraph.Tests.Contract/ # MCP Contract Tests
├── ProjGraph.Tests.Integration.Mcp/ # MCP Integration
└── ProjGraph.Tests.Unit.ClassDiagram/ # Analysis Logic Unit Tests

**Structure Decision**: Logic primarily resides in `ProjGraph.Lib.ClassDiagram.Application.AnalysisOptions` and `Rendering`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
| ----------- | ------------ | ------------------------------------- |
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
