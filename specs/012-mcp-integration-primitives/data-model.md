# Data Model: MCP Integration Primitives

**Date**: 2026-03-10
**Branch**: `012-mcp-integration-primitives`
**Layer**: `ProjGraph.Mcp` (session-scoped, in-memory only)

---

## Entities

### DiagramResource

Represents a cached output from a ProjGraph tool invocation. Session-scoped (exists only while the MCP server process is running).

| Field | Type | Description                                                         |
|-------|------|---------------------------------------------------------------------|
| `Uri` | `string` | Unique resource URI (e.g., `projgraph://diagrams/graph/D%3A%2F...`) |
| `AnalysisType` | `string` | One of: `graph`, `class`, `erd`, `stats`                            |
| `SourcePath` | `string` | Original (decoded) absolute file path that was analyzed             |
| `MimeType` | `string` | `text/plain` for Mermaid diagrams; `application/json` for stats     |
| `Content` | `string` | Full diagram or JSON content                                        |
| `Description` | `string` | Human-readable label (e.g., "Project graph for MySolution.slnx")    |
| `GeneratedAt` | `DateTimeOffset` | Timestamp of initial generation                                     |
| `LastUpdatedAt` | `DateTimeOffset` | Timestamp of most recent update                                     |

**Constraints**:

- URI must be unique within the cache.
- `Content` is replaced on update (last-write-wins).
- `GeneratedAt` is set once on creation; `LastUpdatedAt` is updated on every write.

**Validation rules**: None beyond presence — content is produced by trusted internal services.

---

### ResourceCache

In-memory collection of `DiagramResource` entries with bounded capacity and LRU eviction.

| Field | Type                             | Description |
|-------|----------------------------------|-------------|
| `Entries` | `Dictionary<string, CacheEntry>` | URI → (resource, LRU node) for O(1) lookup |
| `LruOrder` | `LinkedList<string>`             | Head = most recently used, Tail = least recently used |
| `MaxCapacity` | `int` (const 50)                 | Upper bound on cached entries |

**State transitions**:

```none
[Empty] →(Store new)→ [1..49 entries] →(Store new at limit)→ [50 entries, LRU evicted]
[Any]   →(Store existing URI)→ [same count, content updated, LRU order updated]
```

**Operations**:

- `Store(type, sourcePath, mimeType, content, description, server, ct)` — add or update entry; sends `resources/updated` or integrates with `ResourceCollection` for `list_changed`.
- `TryRead(uri)` — returns content for exact URI match; `null` if not cached.
- `ListResources()` — returns all cached `DiagramResource` instances (for listing, not reading).

---

### WorkspaceRoot

A workspace directory declared by the MCP client during the session.

| Field | Type | Description                                                            |
|-------|------|------------------------------------------------------------------------|
| `Uri` | `string` | Absolute URI to the root directory (e.g., `file:///D:/Projects/MyApp`) |
| `Name` | `string?` | Optional human-readable label provided by the client                   |
| `LocalPath` | `string` | Decoded local file system path (e.g., `D:\Projects\MyApp`)             |

**Constraints**:

- `LocalPath` must be an existing directory at the time of resolution (checked during path resolution, not on registration).
- Roots are populated once on first tool call and refreshed on `roots/list_changed` notification.

---

### RootsStatus

Enum-like discriminated union representing the server's knowledge of client Roots capability.

| Value | Meaning                                     |
|-------|---------------------------------------------|
| `Unknown` | Not yet queried (before first tool call)    |
| `Unsupported` | Client does not declare the Roots capability |
| `Ready(IList<WorkspaceRoot>)` | Roots have been fetched and cached          |

---

## Relationships

```none
ResourceCache          1 ─── * DiagramResource
                                     │
                              (registered in)
                                     │
                         McpServerResourceCollection
                         (SDK type, accessed via IOptions<McpServerOptions>)

WorkspaceRootService   holds ──→ RootsStatus (contains 0..* WorkspaceRoot)
ProjGraphTools         depends on ──→ WorkspaceRootService
ProjGraphTools         depends on ──→ DiagramResourceCache
```

---

## In-Scope Boundaries

This is **not** a persistent data model. Nothing is written to disk. All state is:

- Scoped to the lifetime of the MCP server process.
- Cleared when the server restarts.
- Not shared across multiple MCP server instances.
