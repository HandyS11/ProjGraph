# Implementation Plan: File Output (`--output` flag)

**Branch**: `007-cli-file-output` | **Date**: 2026-02-24 | **Spec**: [007-cli-file-output/spec.md](spec.md)
**Input**: Feature specification from `/specs/007-cli-file-output/spec.md`

## Summary

This feature adds a `-o|--output <file>` option to the `visualize`, `class-diagram`, and `erd` CLI commands. This allows users to reliably save diagrams to a file, avoiding shell redirection issues on Windows PowerShell. Each command will read the `OutputPath` from its settings and use `IFileSystem` to write the rendered diagram to disk. If the output file has a `.md` extension (or others except `.mmd`), the diagram will be automatically wrapped in a Mermaid markdown code fence.

## Technical Context

**Language/Version**: .NET 10.0 (C# 14+)
**Primary Dependencies**: `Spectre.Console`, `ProjGraph.Lib.Core` (for `IFileSystem`, `IOutputConsole`)
**Storage**: Files (rendered diagrams)
**Testing**: xUnit, FluentAssertions, Moq
**Target Platform**: .NET Core (Windows, Linux, macOS)
**Project Type**: CLI (`ProjGraph.Cli`)
**Performance Goals**: N/A (minimal overhead)
**Constraints**: MCP 1.0 Compliance (functionality remains in Lib), Zero Warnings, Strict SemVer
**Scale/Scope**: Thin change across 3 commands.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modern .NET 10 Baseline**: Targeting .NET 10.0+?
- [x] **II. MCP Native Interoperability**: Tool functionality already in Lib/MCP. CLI-specific file writing doesn't affect MCP.
- [x] **III. Library-First Core**: File writing logic will use `IFileSystem` from `Lib.Core`.
- [x] **IV. Absolute Testing Requirement**: Integration tests planned for CLI output.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
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
