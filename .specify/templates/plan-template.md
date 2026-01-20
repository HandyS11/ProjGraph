# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: .NET 10.0 (C# 14+)
**Primary Dependencies**: [e.g., Microsoft.Extensions.Logging, MCP.SDK or NEEDS CLARIFICATION]
**Storage**: [if applicable, e.g., SQLite, Files, or N/A]
**Testing**: xUnit, FluentAssertions, Moq
**Target Platform**: .NET Core (Windows, Linux, macOS)
**Project Type**: [Library/CLI/MCP Server]
**Performance Goals**: [domain-specific, e.g., <20ms execution time, <50MB heap]
**Constraints**: MCP 1.0 Compliance, Zero Warnings, Strict SemVer
**Scale/Scope**: [domain-specific]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [ ] **I. Modern .NET 10 Baseline**: Targeting .NET 10.0+?
- [ ] **II. MCP Native Interoperability**: Tool functionality exposed via MCP?
- [ ] **III. Library-First Core**: Logic in libraries, not just CLI/MCP?
- [ ] **IV. Absolute Testing Requirement**: Tests planned for unit, integration, and MCP contract?

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
