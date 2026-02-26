# Quickstart: Solution Metrics Command & MCP Tool

**Feature**: 010-project-stats-mcp
**Branch**: `010-project-stats-mcp`

---

## CLI Usage

### Basic stats (human-readable terminal output)

```bash
projgraph stats MySolution.slnx
```

**Example output:**

```none
─── MySolution.slnx ────────────────────────────────────────────

 Metric                    Value
 ─────────────────────────────────────────────
 Total projects             8
 - Libraries                4
 - Executables              1
 - Test projects            3
 - Other                    0
 Average dependency depth   2.1
 Min depth                  0
 Max depth                  4

 Most-referenced projects (direct in-degree):
  1. MyApp.Core          ← 6
  2. MyApp.Data          ← 4
  3. MyApp.Shared        ← 3
  4. MyApp.Abstractions  ← 2
  5. MyApp.Logging       ← 1
```

### From a single project file

```bash
projgraph stats src/MyApp.Api/MyApp.Api.csproj
```

### In CI (exit code only matters)

```bash
projgraph stats MySolution.slnx
echo "Exit: $?"
```

---

## MCP Tool Usage

Callable by any MCP-compatible AI assistant. The tool is registered as `get_project_stats` on the `ProjGraph` MCP server.

### Minimal call

```json
{
  "tool": "get_project_stats",
  "arguments": {
    "path": "/repos/MyApp/MyApp.slnx"
  }
}
```

### With custom top-N

```json
{
  "tool": "get_project_stats",
  "arguments": {
    "path": "/repos/MyApp/MyApp.slnx",
    "topN": 10
  }
}
```

### Response shape

```json
{
  "solutionName": "MyApp",
  "solutionPath": "/repos/MyApp/MyApp.slnx",
  "totalProjectCount": 8,
  "typeBreakdown": {
    "Library": 4,
    "Executable": 1,
    "Test": 3,
    "Other": 0
  },
  "depthStats": {
    "average": 2.1,
    "min": 0,
    "max": 4
  },
  "hotspotProjects": [
    { "name": "MyApp.Core", "inDegree": 6 },
    { "name": "MyApp.Data", "inDegree": 4 },
    { "name": "MyApp.Shared", "inDegree": 3 }
  ],
  "hasCycles": false
}
```

---

## Error scenarios

| Scenario                   | CLI behaviour | MCP behaviour |
|----------------------------|---|---|
| Path does not exist        | Prints error to stderr, exits non-zero | Tool returns error response |
| Empty solution (0 projects) | Prints "0 projects found" summary | Returns stats with `totalProjectCount: 0` |
| Graph contains cycles      | Prints warning + depth shown as "N/A" | Returns stats with `hasCycles: true`, depth values = -1 |

---

## Running tests for this feature

```bash
# Unit tests — StatsService and ComputeStatsUseCase
dotnet test tests/ProjGraph.Tests.Unit.ProjectGraph --filter "Category=Stats"

# MCP contract tests
dotnet test tests/ProjGraph.Tests.Contract --filter "Stats"

# CLI integration tests
dotnet test tests/ProjGraph.Tests.Integration.Cli --filter "Stats"
```

---

## Implementation files to create / edit

| File                                                                         | Action |
|------------------------------------------------------------------------------|---|
| `src/ProjGraph.Core/Models/SolutionStats.cs`                                 | NEW |
| `src/ProjGraph.Lib.ProjectGraph/Application/IStatsService.cs`                | NEW |
| `src/ProjGraph.Lib.ProjectGraph/Application/StatsService.cs`                 | NEW |
| `src/ProjGraph.Lib.ProjectGraph/Application/UseCases/ComputeStatsUseCase.cs` | NEW |
| `src/ProjGraph.Lib.ProjectGraph/DependencyInjection.cs`                      | EDIT — register new services |
| `src/ProjGraph.Cli/Commands/StatsCommand.cs`                                 | NEW |
| `src/ProjGraph.Cli/Program.cs`                                               | EDIT — add `stats` command |
| `src/ProjGraph.Mcp/Program.cs`                                               | EDIT — add `GetProjectStatsAsync` to `ProjGraphTools` |
| `tests/ProjGraph.Tests.Unit.ProjectGraph/StatsServiceTests.cs`               | NEW |
| `tests/ProjGraph.Tests.Unit.ProjectGraph/ComputeStatsUseCaseTests.cs`        | NEW |
| `tests/ProjGraph.Tests.Contract/McpProjectStatsContractTests.cs`             | NEW |
| `tests/ProjGraph.Tests.Integration.Cli/StatsCommandIntegrationTests.cs`      | NEW |
