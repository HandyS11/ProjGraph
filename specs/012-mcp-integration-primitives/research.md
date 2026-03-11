# Phase 0 Research: MCP Integration Primitives

**Date**: 2026-03-10
**Branch**: `012-mcp-integration-primitives`
**SDK**: `ModelContextProtocol` v1.0.0

All NEEDS CLARIFICATION items from the Technical Context have been resolved. Findings are sourced from the SDK source code at `C:\Users\vclergue\.nuget\packages\modelcontextprotocol\`.

---

## Decision 1: Dynamic Resource Registration Strategy

**Question**: How do we expose session-generated diagram outputs as addressable resources that appear in `listResources`, while also providing a read handler for any diagram URI?

**Decision**: Hybrid approach — attribute-based template for the read handler + runtime `ResourceCollection` mutation for discovery.

**Detail**:

- `ProjGraphResources` class with `[McpServerResourceType]` registers a **template** resource: `[McpServerResource(UriTemplate = "projgraph://diagrams/{type}/{path}")]`. This appears in `listResourceTemplates` (clients can construct URIs from it).
- `DiagramResourceCache` singleton holds generated diagram content keyed by `(type, canonicalPath)`.
- When a tool generates output, it calls `DiagramResourceCache.Store(...)` which:
  1. Adds/updates the cache entry.
  2. Creates or replaces a concrete `McpServerResource` in `McpServerResourceCollection` with an exact URI (e.g., `projgraph://diagrams/graph/D%3A%2Fproject%2FMy.slnx`). This entry appears in `listResources`.
  3. Concrete entry's read handler is a delegate that reads from the cache by URI — consistent with the template handler.
- `ResourceCollection.Add()/Remove()` automatically fires `notifications/resources/list_changed` (SDK wires `Changed` → `SendNotificationAsync` in `McpServerImpl` constructor).
- The tool also calls `server.SendNotificationAsync(NotificationMethods.ResourceUpdatedNotification, ...)` when updating an existing entry.
- The `ResourceCollection` is accessed via `IOptions<McpServerOptions>.Value.ResourceCollection` injected into `DiagramResourceCache`.

**Alternatives considered**:

- *Template-only (no concrete registration)*: resources only appear in `listResourceTemplates`, never in `listResources`. Clients cannot discover which specific diagrams have been generated. ✗ Rejected.
- *Concrete-only (no template)*: Every generated resource needs a complete read handler closure captured at creation time. No benefit over hybrid. ✗ Rejected — same code, less discoverability.
- *Separate `McpServerResource` class per analysis type*: Creates 4 classes for graph/class/erd/stats. Unnecessary duplication. ✗ Rejected.

---

## Decision 2: Welcome Resource Implementation

**Question**: Should the welcome resource be attribute-based (`[McpServerResource]`) or manually added to `ResourceCollection`?

**Decision**: Attribute-based via `[McpServerResource]` on a `static` method in `ProjGraphResources`.

**Detail**:

- `[McpServerResource(Name = "projgraph-welcome", Uri = "projgraph://welcome", MimeType = "text/plain")]` on a `static string GetWelcome()` method.
- The SDK adds this to `ResourceCollection` at startup (exact URI, no template parameters).
- Content is a static string describing ProjGraph's capabilities and available tools.
- Because it's registered at startup (not at runtime), `list_changed` is NOT triggered — it's always there.

**Alternatives considered**:

- *Manually added in `Program.cs`*: Same result, but loses the co-location with `ProjGraphResources`. ✗ Rejected.

---

## Decision 3: Progress Notification Injection

**Question**: How is `IProgress<ProgressNotificationValue>` injected into tool methods?

**Decision**: Add `IProgress<ProgressNotificationValue> progress` as a parameter to each tool method. It is automatically bound by the SDK (not a JSON tool input).

**Detail**:

- SDK auto-injects `IProgress<ProgressNotificationValue>` exactly like `CancellationToken` — no attribute required, not included in the JSON schema.
- If the client included a `progressToken` in `_meta`, reports are forwarded as `notifications/progress`. Otherwise, the instance is a no-op.
- Reports use `ProgressNotificationValue { Progress = n, Total = N, Message = "..." }` at 3 stages per tool: parse/build, analyze, render.
- This requires no changes to library interfaces (`IGraphService`, etc.) — progress is reported at the tool method level, between service calls.

**Alternatives considered**:

- *Reporting progress inside library services*: Requires modifying `IGraphService`, `IClassAnalysisService`, etc. Violates III (library-first) by coupling library APIs to MCP protocol types. ✗ Rejected.

---

## Decision 4: Roots Capability Detection and Caching

**Question**: How and when does the server request client roots? How are they cached?

**Decision**: `WorkspaceRootService` singleton initialized with a `McpServer` reference from the first tool call, cached for the session.

**Detail**:

- No startup hook exists for server → client requests (server is passive until the first client interaction).
- `WorkspaceRootService` is constructor-injected into `ProjGraphTools`. On first use (lazy init), it checks `server.ClientCapabilities?.Roots`:
  - If `null`: roots not supported; store `RootsStatus.Unsupported`. Relative paths → throw descriptive error.
  - If not `null`: call `server.RequestRootsAsync(...)`, cache the result as `IList<Root>`.
- Register a notification handler for `NotificationMethods.RootsListChangedNotification` that refreshes the cache.
- `WorkspaceRootService.TryResolveAsync(string path, McpServer server, CancellationToken ct)` returns:
  - `path` unchanged if already absolute.
  - Resolved absolute path if a unique match is found under a root.
  - `ArgumentException` with list of matches if ambiguous.
  - `InvalidOperationException` if roots not supported and path is relative.

**Alternatives considered**:

- *Initialize roots in `Program.cs` at startup*: The MCP SDK has no `OnConnected` lifecycle hook for stdio servers. The client capabilities are only available after the `initialize` handshake, which happens before the first tool call. However, there is no server-side hook to react to completion of `initialize`. ✗ Not feasible without custom transport code.
- *Check capabilities on every tool call*: Re-request roots on every call. Expensive and unnecessary. ✗ Rejected — cache on first use.
- *`IHostedService` background polling*: Adds complexity, races with the first tool call. ✗ Rejected.

**Note**: `McpServer` is auto-injected into tool methods by the SDK. `WorkspaceRootService` needs `McpServer` available — tool methods pass it through. Alternatively, `WorkspaceRootService` can be scoped per-tool-call if injected directly.

---

## Decision 5: LRU Eviction for Resource Cache

**Question**: How do we enforce the 50-resource limit with LRU eviction while maintaining thread safety?

**Decision**: `DiagramResourceCache` uses a `Dictionary<string, CacheEntry>` for O(1) lookup combined with a `LinkedList<string>` as an LRU order queue, protected by a `Lock` (C# 13 `lock` object).

**Detail**:

- Max capacity: 50 (constant `MaxCachedResources`).
- On `Store(type, path, content)`:
  1. Compute URI from type + URL-encoded path.
  2. If URI already in cache: move to front of LRU list, update content, fire `resources/updated` notification.
  3. If URI not in cache and count == 50: remove LRU tail entry from cache + `ResourceCollection`, add new entry to front.
  4. If URI not in cache and count < 50: add new entry to front, add concrete `McpServerResource` to `ResourceCollection`.
- This is an in-process singleton — no concurrency beyond async tool calls. `Lock` prevents concurrent modification.

**Alternatives considered**:

- *`System.Runtime.Caching.MemoryCache` with size limit*: No built-in LRU order or direct `ResourceCollection` integration. ✗ Rejected.
- *`ConcurrentDictionary` only (no LRU)*: No eviction strategy; unbounded growth. ✗ Rejected.

---

## Decision 6: Prompt Return Type

**Question**: What return type should prompt methods use?

**Decision**: `IEnumerable<ChatMessage>` (from `Microsoft.Extensions.AI`).

**Detail**:

- SDK supports `string`, `PromptMessage`, `IEnumerable<PromptMessage>`, `GetPromptResult`, `ChatMessage`, `IEnumerable<ChatMessage>`.
- `ChatMessage` (from `Microsoft.Extensions.AI`) is the most natural type — already used by the SDK and is clean to construct.
- Each prompt returns a 2-message sequence: a `User` message containing the analysis instructions, and an `Assistant` pre-fill message acknowledging the task.
- No async required — all prompts are pure functions of their arguments.

**Alternatives considered**:

- *`PromptMessage[]`*: Lower-level, requires `Role` enum instead of `ChatRole`. ✗ Not preferred — `ChatMessage` is cleaner.
- *`string`*: Single text blob, not structured as a conversation. ✗ Rejected — multi-message gives the LLM a clearer context.

---

## Decision 7: Resource URI Encoding

**Question**: How should file paths be encoded in resource URIs? File paths contain `:`, `/`, `\` which conflict with URI syntax.

**Decision**: URL-encode the path segment using `Uri.EscapeDataString()`, yielding URIs like `projgraph://diagrams/graph/D%3A%5Cproject%5CMy.slnx`.

**Detail**:

- `Uri.EscapeDataString(path)` encodes `:` → `%3A`, `\` → `%5C`, `/` → `%2F`.
- The template `projgraph://diagrams/{type}/{path}` binds `{path}` to the entire encoded segment.
- `DiagramResourceCache` stores entries keyed by `$"projgraph://diagrams/{type}/{Uri.EscapeDataString(canonicalPath)}"`.
- The template read handler decodes `path` parameter with `Uri.UnescapeDataString()` to look up the cache.

**Alternatives considered**:

- *Base64 encode path*: Opaque, harder to debug. ✗ Rejected.
- *Hash (SHA256) of path*: Opaque, not reversible. ✗ Rejected.
