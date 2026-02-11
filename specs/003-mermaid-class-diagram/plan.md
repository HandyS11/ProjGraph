# Implementation Plan: Mermaid Class Diagram

**Branch**: `003-mermaid-class-diagram` | **Date**: 2026-01-20 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-mermaid-class-diagram/spec.md`

## Summary

This feature adds a new command to `ProjGraph` (CLI and MCP) that generates a Mermaid class diagram for a specific `.cs` file. It supports manual discovery of inheritance and dependencies within the workspace, allowing users to visualize the structural context of a class.

The implementation will follow the "Library-First" principle, with the core analysis logic residing in `ProjGraph.Lib`, and thin wrappers for the CLI and MCP.

## Technical Context

**Language/Version**: .NET 10.0 (C# 14+)
**Primary Dependencies**: `Microsoft.CodeAnalysis.CSharp`, `Spectre.Console`
**Storage**: N/A
**Testing**: xUnit, FluentAssertions
**Target Platform**: .NET Core (Windows, Linux, macOS)
**Project Type**: Library, CLI, MCP Server
**Performance Goals**: < 2s for analysis of a single file and its immediate dependencies.
**Constraints**: MCP 1.0 Compliance, Zero Warnings
**Scale/Scope**: Analysis of individual C# files and their local relationship graph.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modern .NET 10 Baseline**: Targeting .NET 10.0+?
- [x] **II. MCP Native Interoperability**: Tool functionality exposed via MCP?
- [x] **III. Library-First Core**: Logic in libraries, not just CLI/MCP?
- [x] **IV. Absolute Testing Requirement**: Tests planned for unit, integration, and MCP contract?

## Project Structure

### Documentation (this feature)

```text
specs/003-mermaid-class-diagram/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (MCP schema)
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

> **Note:** The original monolithic `ProjGraph.Lib` was subsequently split into domain-specific libraries during implementation.

```text
src/
├── ProjGraph.Core/              # Models: ClassModel, TypeDefinition, MemberDefinition, Relationship
├── ProjGraph.Lib/               # Composition Root (DI wiring only)
├── ProjGraph.Lib.Core/          # Shared Infrastructure (CompilationFactory, Abstractions)
├── ProjGraph.Lib.ClassDiagram/  # ClassAnalysisService, TypeProcessor, MermaidClassDiagramRenderer
├── ProjGraph.Cli/               # ClassDiagramCommand
└── ProjGraph.Mcp/               # get_class_diagram tool
```

```text
tests/
├── ProjGraph.Tests.Unit.ClassDiagram/    # ClassAnalysisService & Renderer Unit Tests
├── ProjGraph.Tests.Integration.Cli/      # CLI Integration Tests
├── ProjGraph.Tests.Integration.Mcp/      # MCP Integration Tests
├── ProjGraph.Tests.Contract/             # MCP Contract Tests for get_class_diagram
└── ProjGraph.Tests.Shared/               # Shared Test Helpers
```

**Structure Decision**: Standard repository structure. No major deviations.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
| ----------- | ------------ | ------------------------------------- |
| None | | |
