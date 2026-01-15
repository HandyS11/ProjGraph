# Implementation Plan: DbContext ERD Generation

**Branch**: `002-dbcontext-erd` | **Date**: 2026-01-15 | **Spec**: [specs/002-dbcontext-erd/spec.md](spec.md)
**Input**: Feature specification from `/specs/002-dbcontext-erd/spec.md`

## Summary

This feature enables generating Mermaid Entity Relationship Diagrams (ERD) from Entity Framework Core `DbContext` classes found in a .NET solution or a specific file. The implementation will use Roslyn-based static analysis to identify `DbContext` classes, their `DbSet` properties, and entity relationships (including shadow join tables for many-to-many). The functionality will be accessible via a new `erd` CLI command and a `get_erd` MCP tool.

## Technical Context

**Language/Version**: .NET 10.0 (C# 14+)
**Primary Dependencies**: `Microsoft.CodeAnalysis.CSharp`, `Microsoft.CodeAnalysis.Workspaces.MSBuild`, `Microsoft.EntityFrameworkCore` (for symbol analysis)
**Storage**: N/A
**Testing**: xUnit, FluentAssertions
**Target Platform**: .NET Core (Windows, Linux, macOS)
**Project Type**: Library, CLI, MCP Server
**Performance Goals**: < 2 seconds for solutions with < 50 entities
**Constraints**: MCP 1.0 Compliance, Zero Warnings, Strict SemVer
**Scale/Scope**: Solution-wide analysis of EF Core models

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modern .NET 10 Baseline**: Targeting .NET 10.0+?
- [x] **II. MCP Native Interoperability**: Tool functionality exposed via MCP?
- [x] **III. Library-First Core**: Logic in libraries, not just CLI/MCP?
- [x] **IV. Absolute Testing Requirement**: Tests planned for unit, integration, and MCP contract?
- [x] **V. Traceability & Semantic Stability**: OpenTelemetry and SemVer considered?

## Project Structure

### Documentation (this feature)

```text
specs/002-dbcontext-erd/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (MUST include MCP schema)
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

```text
src/
├── ProjGraph.Core/      # Shared Domain Models
├── ProjGraph.Lib/       # Core Business Logic (ERD extraction)
├── ProjGraph.Cli/       # Thin CLI tool wrapper (erd command)
└── ProjGraph.Mcp/       # MCP Server interface (get_erd tool)
```

tests/
├── contract/            # MCP Contact Tests for get_erd
├── integration/         # CLI & MCP Integration Tests
└── unit/                # Unit tests for Roslyn-based ERD extraction

**Structure Decision**: Logic will reside in `ProjGraph.Lib` within new namespaces for EF analysis. `ProjGraph.Core` will host the intermediate ERD models.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
| ----------- | ------------ | ------------------------------------- |
| N/A | | |
