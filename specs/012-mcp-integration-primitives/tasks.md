# Tasks: MCP Integration Primitives

**Input**: Design documents from `/specs/012-mcp-integration-primitives/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓

**Tests**: Included — required by Constitution IV and spec.md SC-007.

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no conflicting dependencies)
- **[Story]**: User story this task belongs to (US1–US4)
- All paths are relative to workspace root (`d:\ProjGraph\`)

---

## Phase 1: Setup (Baseline Verification)

**Purpose**: Confirm the existing MCP server builds cleanly before any additive changes.

- [X] T001 Verify solution builds without errors or warnings: `dotnet build ProjGraph.slnx` from repo root and confirm zero errors, zero warnings

**Checkpoint**: Solution builds cleanly — ready for additive changes.

---

## Phase 2: User Story 1 — Pre-Built Analysis Prompts (Priority: P1) 🎯 MVP

**Goal**: Expose four guided MCP prompt templates (`architecture_review`, `dependency_analysis`, `database_schema_review`, `class_structure_review`) so clients can discover and invoke them via `prompts/list` and `prompts/get`.

**Independent Test**: Call `prompts/list` and verify 4 prompts are returned with names, descriptions, and declared arguments. Call `prompts/get` for each with valid arguments and verify a 2-message sequence is returned that references the correct ProjGraph tool by name.

- [X] T002 [P] [US1] Create `tests/ProjGraph.Tests.Contract/McpPromptContractTests.cs` — verify `[McpServerPromptType]` attribute on `ProjGraphPrompts` class; verify 4 methods each decorated with `[McpServerPrompt]`; verify return type `IEnumerable<ChatMessage>`; verify `architecture_review(string path)`, `dependency_analysis(string path, string topN = "5")`, `database_schema_review(string path, string? contextName = null)`, `class_structure_review(string path)` parameter signatures
- [X] T003 [P] [US1] Create `src/ProjGraph.Mcp/ProjGraphPrompts.cs` — `[McpServerPromptType]` class; 4 `static` methods returning `IEnumerable<ChatMessage>` (from `Microsoft.Extensions.AI`): `ArchitectureReview(string path)`, `DependencyAnalysis(string path, string topN = "5")`, `DatabaseSchemaReview(string path, string? contextName = null)`, `ClassStructureReview(string path)`; each method returns exactly 2 messages (User role with multi-step analysis instructions referencing the correct tool, Assistant role pre-fill); message content must match the prompt text in `contracts/mcp-prompts.md`
- [X] T004 [US1] Update `src/ProjGraph.Mcp/Program.cs` — add `.WithPrompts<ProjGraphPrompts>()` to the MCP server builder chain
- [X] T005 [US1] Create `tests/ProjGraph.Tests.Integration.Mcp/McpPromptsTests.cs` — integration tests: `prompts/list` returns exactly 4 prompts; each prompt entry has non-empty name, description, and declared arguments; `prompts/get architecture_review` with a path arg returns User message containing `get_project_graph` and `get_project_stats` tool references; `prompts/get dependency_analysis` references `get_project_graph` and `get_project_stats`; `prompts/get database_schema_review` references `get_erd`; `prompts/get class_structure_review` references `get_class_diagram`

**Checkpoint**: User Story 1 fully functional — 4 prompts discoverable and resolvable independently of all other features.

---

## Phase 3: User Story 2 — Addressable Diagram Resources (Priority: P2)

**Goal**: Cache every tool output as an addressable MCP resource with a unique URI under `projgraph://diagrams/{type}/{encodedPath}`. Expose a static welcome resource at `projgraph://welcome`. Send `list_changed` / `updated` notifications on every cache change. Enforce 50-entry LRU eviction.

**Independent Test**: List resources (welcome resource returned). Call `get_project_graph`. List resources again (diagram resource added). Read resource by URI (content matches tool output). Re-run same tool (exactly one resource for that path, not two). Restart server (only welcome resource present).

- [X] T006 [US2] Create `src/ProjGraph.Mcp/DiagramResourceCache.cs` — singleton; `Lock`-protected `Dictionary<string, CacheEntry>` for O(1) lookup + `LinkedList<string>` LRU queue (head = most recently used, tail = LRU); `const int MaxCachedResources = 50`; method `Store(string type, string sourcePath, string mimeType, string content, string description, IMcpServer server, CancellationToken ct)` — computes URI via `$"projgraph://diagrams/{type}/{Uri.EscapeDataString(sourcePath)}"`, creates `DiagramResource` (fields per data-model.md), if URI exists: update content + `LastUpdatedAt`, move to LRU head, send `notifications/resources/updated`; if new and at capacity: evict LRU tail from `ResourceCollection`, then add to front; if new and under capacity: add concrete `McpServerResource` to `ResourceCollection` (name, description, mimeType per contracts/mcp-resources.md); method `TryRead(string uri) → string?` — return `Content` for exact URI match, null otherwise; method `ListResources() → IReadOnlyList<DiagramResource>`; inject `IOptions<McpServerOptions>` to access `ResourceCollection`
- [X] T007 [US2] Create `src/ProjGraph.Mcp/ProjGraphResources.cs` — `[McpServerResourceType]` class; static method `GetWelcome()` returning `string` with `[McpServerResource(Uri = "projgraph://welcome", Name = "projgraph-welcome", MimeType = "text/plain")]` — content must match the welcome text in `contracts/mcp-resources.md`; instance method `ReadDiagram(RequestContext<ReadResourceRequestParams> context)` with `[McpServerResource(UriTemplate = "projgraph://diagrams/{type}/{path}")]` — extracts `type` and URL-decodes `path` from template variables, calls `_cache.TryRead(uri)`, returns `TextResourceContents` with full content, throws `McpException` if not found; inject `DiagramResourceCache` via constructor
- [X] T008 [P] [US2] Create `tests/ProjGraph.Tests.Contract/McpResourceContractTests.cs` — verify `[McpServerResourceType]` on `ProjGraphResources`; verify `[McpServerResource]` on `GetWelcome` with URI `projgraph://welcome`, name `projgraph-welcome`, MIME type `text/plain`; verify `[McpServerResource(UriTemplate = "projgraph://diagrams/{type}/{path}")]` on `ReadDiagram`; verify `DiagramResourceCache` is registered as singleton in DI; verify `DiagramResourceCache.MaxCachedResources == 50`
- [X] T009 [US2] Update `src/ProjGraph.Mcp/ProjGraphTools.cs` — add `DiagramResourceCache _cache` constructor parameter; after `get_project_graph` generates Mermaid output: call `await _cache.Store("graph", resolvedPath, "text/plain", diagram, $"Project graph for {filename}", server, ct)`; after `get_class_diagram`: `Store("class", resolvedPath, "text/plain", diagram, $"Class diagram for {filename}", server, ct)`; after `get_erd`: `Store("erd", resolvedPath, "text/plain", diagram, $"Entity diagram for {filename}", server, ct)`; after `get_project_stats`: `Store("stats", resolvedPath, "application/json", json, $"Stats for {filename}", server, ct)`
- [X] T010 [US2] Update `src/ProjGraph.Mcp/Program.cs` — add `builder.Services.AddSingleton<DiagramResourceCache>()` and `.WithResources<ProjGraphResources>()` to the MCP server builder chain
- [X] T011 [US2] Create `tests/ProjGraph.Tests.Integration.Mcp/McpResourcesTests.cs` — `resources/list` returns welcome resource before any tool calls; after calling `get_project_graph` a diagram resource appears with correct URI format; `resources/read` by that URI returns full Mermaid content; re-running the same tool does NOT add a second resource for that path (dedup); `resources/updated` notification is received on content refresh; calling `resources/templates/list` returns the `projgraph://diagrams/{type}/{path}` template entry

**Checkpoint**: User Story 2 fully functional — cached resources accessible and notifications firing independently of other stories.

---

## Phase 4: User Story 3 — Workspace-Aware Root Discovery (Priority: P3)

**Goal**: Server lazily fetches workspace roots from the client on first tool call (if roots capability declared) and resolves relative paths against those roots. Refreshes on `roots/list_changed` notification. Returns clear errors for ambiguous paths or unsupported roots.

**Independent Test**: Configure MCP test client with a declared root directory. Call any tool with a relative filename. Verify the server resolves it to the absolute path under the root and the tool executes successfully. Verify error cases (no match, multiple matches, no roots support).

- [X] T012 [US3] Create `src/ProjGraph.Mcp/WorkspaceRootService.cs` — singleton; internal enum `RootsStatusKind { Unknown, Unsupported, Ready }`; `SemaphoreSlim _initLock = new(1, 1)` to prevent concurrent initialization races; `TryResolveAsync(string path, IMcpServer server, CancellationToken ct) → Task<string>`: if path is absolute (`Path.IsPathFullyQualified`) return as-is; lazy init on first call — check `server.ClientCapabilities?.Roots`: if null store `Unsupported`, else call `server.RequestRootsAsync(ct)` and store `Ready(roots)`; if `Unsupported` and relative path: throw `InvalidOperationException("Client does not support workspace roots. Please provide an absolute path.")`; if `Ready`: search all root `LocalPath` directories recursively for the filename; 0 matches: throw `FileNotFoundException($"File '{path}' not found under any workspace root")`; 1 match: return absolute path; 2+ matches: throw `InvalidOperationException($"'{path}' matches multiple roots: {list}. Provide an absolute path.")`; register `roots/list_changed` notification handler that re-fetches roots via `server.RequestRootsAsync(ct)` and updates cache
- [X] T013 [US3] Update `src/ProjGraph.Mcp/Program.cs` — add `builder.Services.AddSingleton<WorkspaceRootService>()` before server build; after server is built register the `roots/list_changed` notification handler by resolving `WorkspaceRootService` from the service provider
- [X] T014 [US3] Update `src/ProjGraph.Mcp/ProjGraphTools.cs` — add `WorkspaceRootService _rootService` constructor parameter; at the start of each of the 4 tool methods, replace the raw `path` parameter with `path = await _rootService.TryResolveAsync(path, server, ct)` before passing to any library service
- [X] T015 [US3] Create `tests/ProjGraph.Tests.Integration.Mcp/McpRootsTests.cs` — verify relative path `"MySolution.slnx"` resolves correctly when client declares a root containing that file; verify absolute path is passed through unchanged; verify `FileNotFoundException` when file not found under any root; verify `InvalidOperationException` listing matches when file exists under multiple roots; verify `InvalidOperationException` with clear "absolute path required" message when client does not declare roots capability; verify cached roots are refreshed after client sends `roots/list_changed` notification

**Checkpoint**: User Story 3 fully functional — relative paths resolve against workspace roots; graceful degradation when roots not supported.

---

## Phase 5: User Story 4 — Progress Notifications for Long Analyses (Priority: P4)

**Goal**: Each of the 4 tools emits `IProgress<ProgressNotificationValue>` reports at 3 named stages (parse/discover → analyze/build → render). Notifications are silently dropped if the client provided no progress token.

**Independent Test**: Call any tool and observe at least 3 progress notifications with non-empty `Message` text. Verify the tool still succeeds when no progress token is present.

- [X] T016 [US4] Update `src/ProjGraph.Mcp/ProjGraphTools.cs` — add `IProgress<ProgressNotificationValue> progress` parameter to all 4 tool method signatures (auto-injected by SDK, not part of JSON schema); in `get_project_graph`: report `{ Progress=1, Total=3, Message="Parsing solution file" }`, `{ Progress=2, Total=3, Message="Building dependency graph" }`, `{ Progress=3, Total=3, Message="Rendering diagram" }` at appropriate points in the execution flow; in `get_class_diagram`: report `"Discovering C# files"`, `"Analyzing types and members"`, `"Rendering class diagram"`; in `get_erd`: report `"Parsing EF Core context"`, `"Analyzing entities and relationships"`, `"Rendering entity diagram"`; in `get_project_stats`: report `"Parsing solution"`, `"Computing dependency metrics"`, `"Summarizing results"`; progress reports must not block execution (fire-and-forget pattern per SDK)
- [X] T017 [P] [US4] Create `tests/ProjGraph.Tests.Integration.Mcp/McpProgressTests.cs` — verify each tool emits exactly 3 progress notifications with `Total = 3` and non-empty `Message`; verify `Progress` values are 1, 2, 3 in order; verify tool call completes successfully with progress present; verify tool call completes successfully when client provides no `progressToken` (no exception thrown)

**Checkpoint**: User Story 4 fully functional — all 4 tools emit structured progress at 3 stages; zero regressions.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Zero-warning build, full test-suite green, end-to-end validation.

- [X] T018 [P] Run `dotnet build ProjGraph.slnx` from repo root — verify zero errors and zero warnings across all projects including `src/ProjGraph.Mcp/`, `tests/ProjGraph.Tests.Contract/`, and `tests/ProjGraph.Tests.Integration.Mcp/`; fix any build issues
- [X] T019 [P] Run contract and integration test suites — `dotnet test tests/ProjGraph.Tests.Contract/` and `dotnet test tests/ProjGraph.Tests.Integration.Mcp/` — verify all new tests pass and no pre-existing tests regress
- [X] T020 Validate `specs/012-mcp-integration-primitives/quickstart.md` scenarios end-to-end against a running server: (1) list prompts and get `architecture_review` with a real solution path; (2) call `get_project_graph`, then `resources/list` and `resources/read` the resulting URI; (3) configure a client root and call a tool with a relative filename; (4) confirm progress notifications appear in the client during a tool call

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **User Story 1 (Phase 2)**: Depends only on existing codebase (no Foundational phase needed for additive-only changes)
- **User Story 2 (Phase 3)**: Depends only on existing codebase; `ProjGraphResources` depends on `DiagramResourceCache` within the phase
- **User Story 3 (Phase 4)**: Depends only on existing codebase; `ProjGraphTools` update depends on `WorkspaceRootService` within the phase
- **User Story 4 (Phase 5)**: Depends on existing tool structure; can begin after US2/US3 modifications to `ProjGraphTools.cs` are complete to avoid conflicts
- **Polish (Phase 6)**: Depends on all user story phases completing

### User Story Dependencies

- **US1 (P1)**: Fully independent — no cross-story dependencies
- **US2 (P2)**: Fully independent — modifies `ProjGraphTools.cs` and `Program.cs` additively
- **US3 (P3)**: Fully independent — modifies `ProjGraphTools.cs` and `Program.cs` additively; may be worked in parallel with US2 on different branches then merged
- **US4 (P4)**: Independent — only adds `IProgress` parameter to tool methods; best applied after US2+US3 `ProjGraphTools.cs` changes are merged to avoid conflicts

### Within Each User Story

- New files within the same story marked `[P]` can be created simultaneously
- `ProjGraphTools.cs` and `Program.cs` updates are sequential within each story (single file edits)
- Contract tests can be written in parallel with implementation (different files)
- Integration tests come after `Program.cs` registration (need full wire-up to run)

### Parallel Opportunities Per Story

```bash
# User Story 1 — create in parallel (different new files):
T002  McpPromptContractTests.cs
T003  ProjGraphPrompts.cs

# User Story 2 — sequential (T007 depends on T006), T008 parallel:
T006  DiagramResourceCache.cs  ← first
T007  ProjGraphResources.cs    ← after T006
T008  McpResourceContractTests.cs  ← [P] with T006/T007 (new file)
T009  ProjGraphTools.cs update ← after T006
T010  Program.cs update        ← after T007 + T009
T011  McpResourcesTests.cs     ← after T010

# User Story 3:
T012  WorkspaceRootService.cs  ← first
T013  Program.cs update        ← after T012
T014  ProjGraphTools.cs update  ← after T012
T015  McpRootsTests.cs         ← after T013 + T014

# User Story 4 — T017 parallel with T016:
T016  ProjGraphTools.cs update (progress) ← implementation
T017  McpProgressTests.cs               ← [P] (new file)

# Polish — T018 and T019 parallel:
T018  dotnet build
T019  dotnet test
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: User Story 1 (T002–T005)
3. **STOP and VALIDATE**: `prompts/list` returns 4 prompts; `prompts/get` resolves correctly
4. Demo to stakeholders — prompts alone deliver immediate value

### Incremental Delivery

1. **T001** → Baseline verified
2. **T002–T005** → Prompts (MVP) — deploy/demo
3. **T006–T011** → Resources — deploy/demo
4. **T012–T015** → Roots — deploy/demo
5. **T016–T017** → Progress — deploy/demo
6. **T018–T020** → Polish + validate

### Parallel Team Strategy

Once Phase 1 is complete, all four user stories can proceed in parallel on separate branches:

- **Developer A**: US1 (T002–T005) — no file conflicts
- **Developer B**: US2 (T006–T011) — touches `ProjGraphTools.cs` and `Program.cs`
- **Developer C**: US3 (T012–T015) — touches `ProjGraphTools.cs` and `Program.cs`
- **Developer D**: US4 (T016–T017) — touches `ProjGraphTools.cs`

> **Warning**: Developers A, B, C, D all create new files or modify the same shared files (`ProjGraphTools.cs`, `Program.cs`). Coordinate merge order: apply US1 first (no shared-file conflict), then merge US2 and US3 sequentially against `ProjGraphTools.cs`, then US4.

---

## Notes

- `[P]` = different files, no conflicting dependencies — can be created in a single parallel batch
- `[USx]` label maps each task to its user story for independent traceability
- All production code changes are **additive only** to `src/ProjGraph.Mcp/` — no library changes
- Existing tool signatures (`get_project_graph`, `get_class_diagram`, `get_erd`, `get_project_stats`) must remain unchanged externally; only internal bodies and constructor parameters change
- All test tasks belong to existing test projects — no new `.csproj` files needed
- Zero-warning policy applies: `<TreatWarningsAsErrors>` is already set in `Directory.Build.props`
