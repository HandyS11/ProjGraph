# Implementation Plan: MCP Integration Primitives

**Branch**: `012-mcp-integration-primitives` | **Date**: 2026-03-10 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/012-mcp-integration-primitives/spec.md`

## Summary

Extend ProjGraph's MCP server with three additional MCP primitives beyond the current Tools-only implementation:

1. **Prompts (P1)**: Four pre-built analysis workflow templates (`architecture_review`, `dependency_analysis`, `database_schema_review`, `class_structure_review`) that guide LLMs through multi-step analyses.
2. **Resources (P2)**: A static welcome resource + dynamic session-scoped diagram output cache with unique URIs, automatic `list_changed` and `updated` notifications, and LRU eviction at 50 entries.
3. **Roots (P3)**: Server requests client workspace roots at connection time and on `roots/list_changed`; tools resolve relative paths against cached roots.
4. **Progress Notifications (P4)**: Each tool reports stage-by-stage progress via `IProgress<ProgressNotificationValue>` injected automatically by the SDK.

**Technical approach**: All changes are additive to `ProjGraph.Mcp` only. No new projects. No library changes. Existing tool signatures preserved.

## Technical Context

**Language/Version**: .NET 10.0 (C# 14+)
**Primary Dependencies**: `ModelContextProtocol` v1.0.0, `Microsoft.Extensions.Hosting`
**Storage**: In-memory only (`DiagramResourceCache` singleton, max 50 entries, LRU eviction)
**Testing**: xUnit, FluentAssertions (contract + integration tests in existing test projects)
**Target Platform**: .NET Core (Windows, Linux, macOS) — stdio MCP transport
**Project Type**: MCP Server (`ProjGraph.Mcp`)
**Performance Goals**: Prompt resolution <5ms; resource list/read <1ms (all in-memory); tool latency unchanged
**Constraints**: MCP 1.0 compliance; zero warnings; existing tool signatures unchanged (no breaking changes); session-scoped cache only (no disk writes)
**Scale/Scope**: Single-instance MCP server process; session-scoped state only; up to 50 cached diagram resources

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modern .NET 10 Baseline**: Targeting .NET 10.0+ ✓ (unchanged)
- [x] **II. MCP Native Interoperability**: All new primitives (Prompts, Resources, Roots, Progress) exposed natively via MCP protocol ✓
- [x] **III. Library-First Core**: No new business logic introduced — Prompts/Resources/Roots are MCP delivery-layer concerns, not domain logic. Existing libraries unchanged ✓
- [x] **IV. Absolute Testing Requirement**: Contract tests for new `[McpServerPromptType]`/`[McpServerResourceType]` surfaces; integration tests for prompt resolution, resource listing/reading, roots capability; DI wiring tests ✓

**Post-design re-check**: PASS — design confirmed library-first (all new code in `ProjGraph.Mcp`); no constitution violations.

## Project Structure

### Documentation (this feature)

```text
specs/012-mcp-integration-primitives/
├── plan.md              ← this file
├── research.md          ← Phase 0: SDK patterns, decisions
├── data-model.md        ← Phase 1: DiagramResource, ResourceCache, WorkspaceRoot entities
├── quickstart.md        ← Phase 1: How to use prompts, resources, roots
├── contracts/
│   ├── mcp-prompts.md   ← Prompt schemas (4 prompts)
│   ├── mcp-resources.md ← Resource URIs and template
│   └── mcp-roots.md     ← Roots interaction flow
└── tasks.md             ← Phase 2 (/speckit.tasks command)
```

### Source Code Changes

```text
src/ProjGraph.Mcp/
├── Program.cs                        ← Add .WithPrompts<>(), .WithResources<>(), roots init
├── ProjGraphPrompts.cs               ← NEW: [McpServerPromptType] — 4 prompt templates
├── ProjGraphResources.cs             ← NEW: [McpServerResourceType] — welcome + template read handler
├── DiagramResourceCache.cs           ← NEW: in-memory cache with LRU eviction
└── WorkspaceRootService.cs           ← NEW: roots request + notification handling

tests/ProjGraph.Tests.Contract/
├── McpPromptContractTests.cs         ← NEW: verify [McpServerPromptType], 4 prompts
└── McpResourceContractTests.cs       ← NEW: verify [McpServerResourceType], URI patterns

tests/ProjGraph.Tests.Integration.Mcp/
├── McpPromptsTests.cs                ← NEW: prompt resolution, message content
├── McpResourcesTests.cs              ← NEW: resource listing, reading, cache dedup
└── McpRootsTests.cs                  ← NEW: root resolution, relative paths
```

**Structure Decision**: All new production code in `ProjGraph.Mcp`. New test classes in existing test projects (no new projects). Matches the existing pattern (`ProjGraphTools` → `ProjGraphPrompts`/`ProjGraphResources`).
