# Implementation Plan: NuGet Package Reference Rendering in `visualize`

**Branch**: `008-nuget-package-rendering` | **Date**: 2026-02-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/008-nuget-package-rendering/spec.md`

## Summary

The goal is to enhance the `visualize` command to optionally include NuGet package references in the dependency graph. Currently, `DependencyType.PackageReference` exists in the core model but is not populated during analysis or handled by the renderers. We will update `IProjectParser` and `ProjectParser` to extract `PackageReference` items, modify `BuildGraphUseCase` and `IGraphService` to support an `includePackages` flag, and update all three renderers (Mermaid, Tree, Flat) to render these packages distinctly.

## Technical Context

**Language/Version**: .NET 10.0 (C# 14+)
**Primary Dependencies**: Microsoft.Build.Construction (for project parsing), Spectre.Console (for rendering)
**Storage**: N/A (In-memory graph processing)
**Testing**: xUnit, FluentAssertions, NSubstitute
**Target Platform**: .NET Core (Windows, Linux, macOS)
**Project Type**: Library/CLI/MCP Server
**Performance Goals**: Minimal overhead when flag is disabled; linear scaling with number of packages.
**Constraints**: MCP 1.0 Compliance, Zero Warnings, Strict SemVer
**Scale/Scope**: Direct `PackageReference` items in `.csproj` files only.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modern .NET 10 Baseline**: Targeting .NET 10.0+. Added `ProjectReference` records and `ProjectType.Package` for better modeling.
- [x] **II. MCP Native Interoperability**: `get_project_graph` tool will be updated with `includePackages` parameter. Contracts defined in `/contracts/mcp-schema.md`.
- [x] **III. Library-First Core**: All logic (parsing, building, rendering) remains in `ProjGraph.Lib.*` projects.
- [x] **IV. Absolute Testing Requirement**: Unit tests for `ProjectParser`, `BuildGraphUseCase`, and each renderer are planned. Integration tests for CLI and MCP are also specified.

## Project Structure

### Documentation (this feature)

```text
specs/008-nuget-package-rendering/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (MCP schema update)
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

```text
src/
├── ProjGraph.Core/
│   └── Models/          # Update Project record if needed for versioning
├── ProjGraph.Lib.Core/
│   ├── Abstractions/    # IProjectParser, DiagramOptions updates
│   └── Parsers/         # ProjectParser implementation update
├── ProjGraph.Lib.ProjectGraph/
│   ├── Application/     # IGraphService, BuildGraphUseCase updates
│   └── Rendering/       # Renderer updates (Mermaid, Tree, Flat)
├── ProjGraph.Cli/       # VisualizeCommand settings update
└── ProjGraph.Mcp/       # ProjGraphTools update
```

**Structure Decision**: Standard vertical slice update across existing layers.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
| ----------- | ------------ | ------------------------------------- |
| None | N/A | N/A |
