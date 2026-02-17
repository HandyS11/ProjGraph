# Implementation Plan: Showcase Samples & Reference Documentation

**Branch**: `005-improve-samples` | **Date**: 2026-02-17 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/005-improve-samples/spec.md`

## Summary

This feature involves a complete overhaul of the `samples/` directory to serve as the definitive "Showcase" and documentation reference for ProjGraph. We will standardize the sample structure, eliminate build artifacts from the repository, expand models to demonstrate complex real-world scenarios (including a new complex E-commerce ERD), and provide internal scripts for documentation maintenance.

## Technical Context

**Language/Version**: .NET 10.0 (C# 14+)
**Primary Dependencies**: ProjGraph.Cli (local build), .NET SDK 10.0
**Storage**: Local file system (Markdown, C# Source, .slnx Solution Explorers)
**Testing**: `dotnet build` on all samples, internal `regen-samples.ps1` for visual verification
**Target Platform**: .NET Core (Windows, Linux, macOS)
**Project Type**: Documentation & Sample Code
**Performance Goals**: All samples MUST build in < 10s; Diagram generation < 2s/sample
**Constraints**: Zero build artifacts in repo, professional naming only (no Foo/Bar), SemVer 2.0.0 snapshots
**Scale/Scope**: 5+ standardized sample projects covering all tool features

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modern .NET 10 Baseline**: All samples will target `net10.0`.
- [x] **II. MCP Native Interoperability**: Samples demonstrate tools that are natively exposed via MCP.
- [x] **III. Library-First Core**: Samples demonstrate logic parsed from libraries (Class Diagram/ERD engines).
- [x] **IV. Absolute Testing Requirement**: Test cases include successful build and successful diagram generation for every sample.

## Project Structure

### Documentation (this feature)

```text
specs/005-improve-samples/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (defining the sample models)
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (CLI command references)
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

```text
src/
├── ProjGraph.Core/      # Shared Domain Models & Constants
├── ProjGraph.Lib/       # Principles III: Core Business Logic Libraries
├── ProjGraph.Cli/       # Thin CLI tool wrapper
└── ProjGraph.Mcp/       # Principles II: MCP Server interface
```

tests/
├── contract/            # MCP Contact Tests
├── integration/         # CLI & MCP Integration
└── unit/                # Library Logic Unit Tests

**Structure Decision**: [Document the selected structure]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
| ----------- | ------------ | ------------------------------------- |
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
