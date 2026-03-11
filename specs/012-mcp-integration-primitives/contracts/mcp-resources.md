# MCP Resources Contract

**Date**: 2026-03-10
**Branch**: `012-mcp-integration-primitives`
**Registration**: `builder.Services.AddMcpServer().WithResources<ProjGraphResources>()`
**SDK type**: `[McpServerResourceType]` / `[McpServerResource]`

---

## Static Resource: `projgraph://welcome`

**URI**: `projgraph://welcome`
**Name**: `projgraph-welcome`
**MIME Type**: `text/plain`
**Description**: Static welcome resource describing ProjGraph's capabilities and available tools.
**Lifecycle**: Always present (registered at startup, never evicted).

### Content

```none
Welcome to ProjGraph MCP Server

ProjGraph analyzes .NET solution architectures and generates Mermaid diagrams and metrics.

Available Tools:
─────────────────────────────────────────────────────────────────────────────
• get_project_graph  — Dependency graph for a .sln, .slnx, or .csproj file
• get_class_diagram  — Class diagram for a .cs file or directory
• get_erd            — Entity Relationship Diagram from an EF Core DbContext or ModelSnapshot
• get_project_stats  — Architectural metrics (dependency depth, hotspots, cycles)

Available Prompts (guided workflows):
─────────────────────────────────────────────────────────────────────────────
• architecture_review    — Comprehensive architecture review of a .NET solution
• dependency_analysis    — Hotspot and cycle analysis for a solution
• database_schema_review — EF Core schema design review
• class_structure_review — Class hierarchy and design pattern review

Generated Diagrams (session cache):
─────────────────────────────────────────────────────────────────────────────
Diagrams generated during this session are cached as resources under
projgraph://diagrams/{type}/{encodedPath} and appear in listResources.
```

---

## Dynamic Resource Template: `projgraph://diagrams/{type}/{path}`

**URI Template**: `projgraph://diagrams/{type}/{path}`
**Name**: `projgraph-diagram`
**MIME Type**: Varies per entry (`text/plain` for Mermaid, `application/json` for stats)
**Description**: Cached diagram output from a previous tool invocation.
**Lifecycle**: Session-scoped. Created when a tool generates output. Evicted (LRU) when cache reaches 50 entries. Cleared on server restart.

### URI Template Parameters

| Parameter | Description | Example                            |
|-----------|-------------|------------------------------------|
| `type` | Analysis type | `graph`, `class`, `erd`, `stats`   |
| `path` | URL-encoded absolute file path | `D%3A%5CProjects%5CMySolution.slnx` |

### Concrete URI Examples

| Tool | Example URI                                                    |
|------|----------------------------------------------------------------|
| `get_project_graph` | `projgraph://diagrams/graph/D%3A%5CProjects%5CMySolution.slnx` |
| `get_class_diagram` | `projgraph://diagrams/class/D%3A%5CSrc%5CMyClass.cs`           |
| `get_erd` | `projgraph://diagrams/erd/D%3A%5CSrc%5CMyDbContext.cs`         |
| `get_project_stats` | `projgraph://diagrams/stats/D%3A%5CProjects%5CMySolution.slnx` |

### ResourceContents Type

When read, each resource returns a `TextResourceContents` with:

- `Uri`: exact resource URI
- `MimeType`: `text/plain` or `application/json`
- `Text`: full diagram or JSON content

### Metadata Annotation

Each concrete resource registered in `ResourceCollection` includes:

- `Name`: `"{type} — {filename}"` (e.g., `"graph — MySolution.slnx"`)
- `Description`: `"Generated {timestamp} from {sourcePath}"`
- `MimeType`: as above

---

## Resource Listing Behavior

| MCP Method | Returns | Includes                                                          |
|------------|---------|-------------------------------------------------------------------|
| `resources/list` | Concrete resources | Welcome resource + all currently cached diagram resources         |
| `resources/templates/list` | Template resources | The `projgraph://diagrams/{type}/{path}` template                 |
| `resources/read` | Content | Any exact URI in `listResources`; or any URI matching the template |

---

## Notification Behavior

| Event | Notification Sent                                                                                  |
|-------|----------------------------------------------------------------------------------------------------|
| New diagram resource created | `notifications/resources/list_changed` (automatic via `ResourceCollection.Add()`)                  |
| Existing diagram resource updated | `notifications/resources/updated` with `Uri` parameter (sent explicitly by `DiagramResourceCache`) |
| LRU eviction of a resource | `notifications/resources/list_changed` (automatic via `ResourceCollection.Remove()`)               |
| Server restart | No notification — client must re-list on reconnect                                                 |

---

## Cache Limits

| Parameter | Value                                            |
|-----------|--------------------------------------------------|
| Max entries | 50                                               |
| Eviction policy | LRU (least recently used)                        |
| Eviction granularity | One entry at a time (evict before inserting new) |
| Thread safety | `Lock` on all operations                         |

---

## Implementation Notes

- Class: `ProjGraphResources` in `src/ProjGraph.Mcp/ProjGraphResources.cs`
- Services: `DiagramResourceCache` singleton in `src/ProjGraph.Mcp/DiagramResourceCache.cs`
- `ProjGraphResources` has `[McpServerResourceType]`; methods have `[McpServerResource]`
- Welcome resource: `static string GetWelcome()` — no DI
- Template read handler: instance method with `DiagramResourceCache` injected via constructor
- `DiagramResourceCache` injects `IOptions<McpServerOptions>` to access `ResourceCollection`
- `ProjGraphTools` is updated to call `DiagramResourceCache.Store(...)` after generating each output
- `McpServer server` parameter in tool methods is used to send `resources/updated` notification
