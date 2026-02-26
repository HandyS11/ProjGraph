# Implementation Plan: MCP Registry Submission

**Branch**: `011-mcp-registry-submission` | **Date**: 2026-02-26 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/011-mcp-registry-submission/spec.md`

## Summary

The primary requirement is to enable official registry submission for the ProjGraph MCP server. This involves adding ownership verification to the NuGet README via a specific HTML comment (`<!-- mcp-name: io.github.handys11/projgraph -->`) and integrating the `mcp-publisher` CLI tool into the GitHub Actions release workflow to publish the `server.json` metadata to the official MCP registry.

## Technical Context

**Language/Version**: .NET 10.0 (C# 14+)
**Primary Dependencies**: `mcp-publisher` (npm), GitHub Actions
**Storage**: N/A
**Testing**: CI Workflow validation, README content verification (NuGet package inspection)
**Target Platform**: GitHub Actions CI/CD
**Project Type**: Infrastructure / Deployment
**Performance Goals**: N/A
**Constraints**: MCP Registry Schema Compliance, Secure Credential Management
**Scale/Scope**: Deployment workflow update

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modern .NET 10 Baseline**: Targeting .NET 10.0+? Yes.
- [x] **II. MCP Native Interoperability**: Tool functionality exposed via MCP? Yes (Enables discovery).
- [x] **III. Library-First Core**: Logic in libraries, not just CLI/MCP? Yes (N/A for infra).
- [x] **IV. Absolute Testing Requirement**: Tests planned for unit, integration, and MCP contract? Yes (Verification comment and CLI smoke test).

## Project Structure

### Documentation (this feature)

```text
specs/011-mcp-registry-submission/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

```text
src/
├── ProjGraph.Mcp/       # server.json location
```

.github/
└── workflows/           # Release workflow updating

**Structure Decision**: Infrastructure update focusing on CI workflows and NuGet metadata.
