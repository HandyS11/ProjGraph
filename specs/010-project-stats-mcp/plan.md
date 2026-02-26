# Implementation Plan: Solution Metrics Command (`projgraph stats`) and MCP Tool

**Branch**: `010-project-stats-mcp` | **Date**: 2026-02-26 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/010-project-stats-mcp/spec.md`

## Summary

Add a `projgraph stats` CLI command and a `get_project_stats` MCP tool that analyse a solution's dependency graph and return key architectural metrics: total project count, project type breakdown (library/executable/test/other), dependency depth statistics (average, min, max), and a ranked list of the most-referenced projects by direct in-degree. All metrics are computed from the existing `SolutionGraph` model returned by `IGraphService.BuildGraphAsync()` — no Roslyn analysis or new parsing is needed.

The `StatsService` lives in `ProjGraph.Lib.ProjectGraph`, keeping business logic in the library layer (Constitution III). The CLI `StatsCommand` (Spectre.Console) and the MCP `GetProjectStatsAsync` tool are thin consumers of that service. Stats are returned as a JSON string from the MCP tool for AI-parsable structured data.

## Technical Context

**Language/Version**: .NET 10.0 (C# 14+)
**Primary Dependencies**: `Spectre.Console.Cli` (CLI formatting), `ModelContextProtocol.Server` (MCP tool), `System.Text.Json` (MCP response serialisation), `Microsoft.Extensions.DependencyInjection` (DI wiring)
**Storage**: N/A — pure in-memory computation over `SolutionGraph`
**Testing**: xUnit, FluentAssertions, Moq
**Target Platform**: .NET 10.0 cross-platform (Windows, Linux, macOS)
**Project Type**: Library (`StatsService`) + CLI command + MCP tool
**Performance Goals**: Analysis completes in <5 seconds for solutions with ≤100 projects
**Constraints**: MCP 1.0 Compliance, Zero Warnings, Strict SemVer, no new project files needed
**Scale/Scope**: Pure graph traversal over `SolutionGraph.Projects` and `SolutionGraph.Dependencies`; O(P + D) time complexity where P = projects, D = dependency edges

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modern .NET 10 Baseline**: Targeting .NET 10.0+ — all new code goes into existing .NET 10 projects.
- [x] **II. MCP Native Interoperability**: `get_project_stats` MCP tool added to `ProjGraphTools`.
- [x] **III. Library-First Core**: `StatsService` and `ComputeStatsUseCase` live in `ProjGraph.Lib.ProjectGraph`; CLI and MCP are consumers only.
- [x] **IV. Absolute Testing Requirement**: Unit tests for `StatsService`/use case, MCP contract tests, and CLI integration tests are all planned (see Project Structure below).

**Result**: All gates pass. No violations. Complexity Tracking table not required.

## Project Structure

### Documentation (this feature)

```text
specs/010-project-stats-mcp/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── get_project_stats.json   ← MCP tool schema (Phase 1)
└── tasks.md             ← Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code Changes

```text
src/
├── ProjGraph.Core/
│   └── Models/
│       └── SolutionStats.cs          NEW — SolutionStats, DependencyDepthStats, HotspotProject records
├── ProjGraph.Lib.ProjectGraph/
│   ├── Application/
│   │   ├── IStatsService.cs          NEW
│   │   ├── StatsService.cs           NEW
│   │   └── UseCases/
│   │       └── ComputeStatsUseCase.cs  NEW
│   └── DependencyInjection.cs        EDIT — register StatsService + use case
├── ProjGraph.Cli/
│   ├── Program.cs                    EDIT — add "stats" command
│   └── Commands/
│       └── StatsCommand.cs           NEW
└── ProjGraph.Mcp/
    └── Program.cs                    EDIT — add GetProjectStatsAsync to ProjGraphTools

tests/
├── ProjGraph.Tests.Unit.ProjectGraph/
│   ├── StatsServiceTests.cs          NEW
│   └── ComputeStatsUseCaseTests.cs   NEW
├── ProjGraph.Tests.Contract/
│   └── McpProjectStatsContractTests.cs  NEW
└── ProjGraph.Tests.Integration.Cli/
    └── StatsCommandIntegrationTests.cs  NEW
```

**Structure Decision**: No new .csproj files are needed. All additions are new files within existing projects. `SolutionStats` models go to `ProjGraph.Core` (shared domain layer), stats logic goes to `ProjGraph.Lib.ProjectGraph` (library layer, Constitution III).
