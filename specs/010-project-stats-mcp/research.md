# Research: Solution Metrics Command & MCP Tool

**Feature**: 010-project-stats-mcp
**Phase**: 0 — Resolve unknowns before design
**Date**: 2026-02-26

---

## 1. Existing Graph Model Capabilities

**Question**: Does `SolutionGraph` already contain enough data to compute all required metrics without additional parsing?

**Finding**: Yes. `SolutionGraph` (in `ProjGraph.Core.Models`) carries:

- `IReadOnlyList<Project> Projects` — each `Project` has a `ProjectType` enum value (`Library`, `Executable`, `Test`, `Other`, `Package`)
- `IReadOnlyList<Dependency> Dependencies` — each `Dependency` has `SourceId`, `TargetId`, and `DependencyType` (`ProjectReference`, `PackageReference`)

All required metrics (count, type breakdown, depth, in-degree) are derivable from these two collections alone.

**Decision**: No new parsing or Roslyn analysis is required. `IGraphService.BuildGraphAsync()` is the sole data source.

---

## 2. Project Type Classification

**Question**: Are `ProjectType` values already populated correctly by the existing parser, or does classification need to be added?

**Finding**: `ProjectType` is already assigned by `BuildGraphUseCase` / `ProjectParser`. The `Test` type is set when the project has `<IsTestProject>true</IsTestProject>` in the `.csproj` or is discovered through a test SDK reference. The `Package` type marks NuGet package nodes that are included when `includePackages = true`.

**Decision**:

- Stats will call `BuildGraphAsync(path, includePackages: false)` by default so NuGet package nodes are excluded from project counts and depth analysis.
- If any project cannot be typed, it falls into `ProjectType.Other` — already handled by the model.
- No heuristic override needed from the stats layer.

---

## 3. Dependency Depth Algorithm

**Question**: How should "average dependency depth" be calculated from `SolutionGraph.Dependencies`?

**Decision**: Depth is defined as the **longest path** from a project to any of its transitive dependencies (i.e., the project's "height" in the DAG). This models how deep the dependency chain extends below any given node.

**Algorithm**: Topological sort + dynamic programming (standard longest-path in a DAG):

1. Build an adjacency list from `Dependency` edges where `Type == ProjectReference`.
2. Compute the topological order of all project nodes.
3. For each node in reverse topological order, `depth[node] = 1 + max(depth[child])` for all `child` in its dependencies; leaf nodes have `depth = 0`.
4. `Average = mean(depth[all projects])`, `Min = min(depth)`, `Max = max(depth)`.

**Complexity**: O(P + D) where P = project count, D = dependency edge count. Well within the <5 second target even for 100+ projects.

**Cycle handling**: The existing `TarjanSccAlgorithm` in `ProjGraph.Lib.Core.Domain.Algorithms` handles SCC detection. If cycles exist in the graph, topological sort will not produce a complete ordering. `ComputeStatsUseCase` will detect cycles and fall back to BFS-based depth per node (or treat SCCs as single nodes). For the initial implementation, the simpler approach is: if Tarjan detects any SCC with >1 member, report depth stats as "N/A - cycle detected" with a depth value of -1, since cycles break DAG assumptions. This is safe and transparent for users.

**Alternatives considered**:

- *BFS from each node* — O(P × (P+D)); too slow for large graphs.
- *Average number of direct dependencies per project* — rejected; this is a "fan-out" metric, not "depth."

---

## 4. Hotspot / In-Degree Ranking

**Question**: Should "most-depended-on" be measured by direct or transitive in-degree?

**Decision**: **Direct in-degree** (count of projects that directly reference each project) is used. This is:

- Simple and fast to compute: one pass over `Dependencies` where `Type == ProjectReference`.
- Clearly interpretable ("5 projects directly depend on this library").
- The standard definition of "most referenced" in dependency graph analysis tools.

Transitive in-degree is more opaque to users, is more expensive to compute, and is susceptible to amplification in deep graphs. It is deferred as a possible future option.

**Top-N default**: Top 5 projects are shown by default. The `ComputeStatsUseCase` accepts a `topN` parameter; the CLI and MCP tool pass 5 as the default and may expose it as an optional setting.

---

## 5. CLI Output Format

**Question**: What output format should `projgraph stats` produce?

**Decision**: Use **Spectre.Console table and rule markup** consistent with the rest of the CLI, rendered to `IOutputConsole`. Output consists of:

1. A titled section header (solution name)
2. A table with metric rows (label + value)
3. A ranked list for hotspot projects

No `--format` flag is needed for the initial implementation; a single human-readable format is sufficient. If machine-readable output is required in CI scripts, the MCP tool is the preferred channel.

---

## 6. MCP Tool Return Type

**Question**: Should `GetProjectStatsAsync` return `Task<string>` (JSON) or a typed `Task<SolutionStats>`?

**Decision**: **`Task<string>`** with JSON-serialised `SolutionStats` content (using `System.Text.Json.JsonSerializer.Serialize`).

**Rationale**:

- All existing MCP tools in `ProjGraphTools` return `Task<string>`. Consistency avoids surprising the MCP server's type serialisation behaviour.
- JSON strings are directly parseable by AI assistants without needing schema knowledge of the C# record.
- The `SolutionStats` record uses `[JsonPropertyName]` attributes for clean camelCase JSON keys.

**Alternatives considered**:

- *Return typed object* — MCP SDK may serialise this, but the interaction with `[McpServerTool]` attribute and the SDK's return serialiser is not tested in this codebase. Risk is not justified.

---

## 7. DI Registration

**Finding**: `ProjectGraphServiceRegistration.AddProjGraphProjectGraph()` in `ProjGraph.Lib.ProjectGraph` is the correct registration point. Add:

- `services.AddSingleton<ComputeStatsUseCase>()`
- `services.AddSingleton<IStatsService, StatsService>()`

Both `ProjGraph.Cli` and `ProjGraph.Mcp` call `AddProjGraphLib()` which internally calls `AddProjGraphProjectGraph()`, so both consumers receive `IStatsService` with no additional wiring changes outside their entry points.

---

## Summary of Decisions

| Decision | Choice                              | Rationale |
|---|-------------------------------------|---|
| Data source | `IGraphService.BuildGraphAsync()`   | All needed data is already exposed |
| `includePackages` default | `false`                             | Exclude NuGet nodes from project-count metrics |
| Depth definition | Longest path in DAG (topological DP) | Standard, O(P+D), clearly interpretable |
| Cycle handling | Report -1 / "N/A" with user message | Safe, transparent, avoids silent corruption |
| Hotspot metric | Direct in-degree                    | Simple, fast, interpretable |
| Top-N hotspots | 5 (default)                         | Reasonable dashboard number |
| CLI format | Spectre.Console table               | Consistent with existing CLI commands |
| MCP return type | `Task<string>` (JSON)               | Consistent with all other MCP tools |
| New projects needed | None                                | All changes fit within existing projects |
