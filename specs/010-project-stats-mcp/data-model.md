# Data Model: Solution Metrics

**Feature**: 010-project-stats-mcp
**Phase**: 1 — Design
**Date**: 2026-02-26

---

## Overview

Three new value-object records are added to `ProjGraph.Core.Models` in `SolutionStats.cs`. They are pure data containers — no methods, no dependencies. They are referenced by `StatsService` (library layer), `StatsCommand` (CLI), and `GetProjectStatsAsync` (MCP).

---

## Entity: `SolutionStats`

**File**: `src/ProjGraph.Core/Models/SolutionStats.cs`

Aggregated metrics snapshot for one solution or project analysis run.

| Property | Type                                   | Description |
|---|----------------------------------------|---|
| `SolutionName` | `string`                               | The name of the analysed solution or project (from `SolutionGraph.Name`) |
| `SolutionPath` | `string`                               | The absolute path to the analysed file (from `SolutionGraph.Path`) |
| `TotalProjectCount` | `int`                                  | Number of non-Package projects in the graph |
| `TypeBreakdown` | `IReadOnlyDictionary<ProjectType, int>` | Count per project type (Library, Executable, Test, Other) |
| `DepthStats` | `DependencyDepthStats`                 | Aggregate depth metrics across all projects |
| `HotspotProjects` | `IReadOnlyList<HotspotProject>`        | Top-N most directly-referenced projects, descending by in-degree |
| `HasCycles` | `bool`                                 | True if a dependency cycle was detected; when true, `DepthStats` values are -1 |

**JSON key naming**: camelCase via `[JsonPropertyName]` attributes (e.g., `solutionName`, `totalProjectCount`, etc.)

---

## Entity: `DependencyDepthStats`

**File**: `src/ProjGraph.Core/Models/SolutionStats.cs` (same file)

Depth statistics across all projects in the graph.

| Property | Type     | Description |
|---|----------|---|
| `Average` | `double` | Mean longest-path depth across all projects; -1 if cycles detected |
| `Min` | `int`    | Minimum depth (= 0 for leaf projects); -1 if cycles detected |
| `Max` | `int`    | Maximum depth (= longest chain in graph); -1 if cycles detected |

---

## Entity: `HotspotProject`

**File**: `src/ProjGraph.Core/Models/SolutionStats.cs` (same file)

A single entry in the ranked hotspot list.

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Project name |
| `InDegree` | `int` | Number of projects that directly reference this project (`ProjectReference` edges only) |

---

## Relationships

```none
SolutionGraph (existing)
    └── used as input by ComputeStatsUseCase
            └── produces SolutionStats
                    ├── DependencyDepthStats (embedded)
                    └── IReadOnlyList<HotspotProject> (embedded)
```

`SolutionStats` is a read-only computed snapshot. It is never persisted or mutated after creation.

---

## State Transitions

Not applicable — `SolutionStats` is a single-use, immutable result object produced per analysis run.

---

## Validation Rules

| Rule | Description |
|---|---|
| `TotalProjectCount >= 0` | Zero is valid (empty solution) |
| `DepthStats.Average >= 0` | Unless `HasCycles == true`, in which case -1 is reported |
| `HotspotProjects` may be empty | Valid when the solution has zero projects |
| `TypeBreakdown` keys cover all `ProjectType` values except `Package` | All known types are always present (with count 0 if none found) |

---

## C# Record Sketch

```csharp
// src/ProjGraph.Core/Models/SolutionStats.cs

using System.Text.Json.Serialization;

namespace ProjGraph.Core.Models;

public record SolutionStats(
    [property: JsonPropertyName("solutionName")]   string SolutionName,
    [property: JsonPropertyName("solutionPath")]   string SolutionPath,
    [property: JsonPropertyName("totalProjectCount")] int TotalProjectCount,
    [property: JsonPropertyName("typeBreakdown")]  IReadOnlyDictionary<string, int> TypeBreakdown,
    [property: JsonPropertyName("depthStats")]     DependencyDepthStats DepthStats,
    [property: JsonPropertyName("hotspotProjects")] IReadOnlyList<HotspotProject> HotspotProjects,
    [property: JsonPropertyName("hasCycles")]      bool HasCycles
);

public record DependencyDepthStats(
    [property: JsonPropertyName("average")] double Average,
    [property: JsonPropertyName("min")]     int Min,
    [property: JsonPropertyName("max")]     int Max
);

public record HotspotProject(
    [property: JsonPropertyName("name")]     string Name,
    [property: JsonPropertyName("inDegree")] int InDegree
);
```

> **Note**: `TypeBreakdown` uses `IReadOnlyDictionary<string, int>` (not `ProjectType` enum key) so it serialises cleanly to JSON without needing a custom converter. The string key is the `ProjectType` enum name (e.g., `"Library"`, `"Test"`).
