# MCP error surfacing — McpException conversions, warning placement, multi-DbContext

Date: 2026-07-16
Status: Approved (design)
Program: EF rewrite / Phase 2 queue item 3 (audit §6)

## Problem

ModelContextProtocol 1.4.1 (verified against the shipped DLL during the 2026-07-14 audit)
replaces the message of every non-`McpException` thrown by a tool with the generic
"An error occurred invoking '…'". Four defects hide behind that behavior:

1. **[High residual, audit §3 #4]** `WorkspaceRootService` throws BCL exceptions
   (`InvalidOperationException` roots-unsupported, `FileNotFoundException` not-found,
   `AmbiguousMatchException` multi-root, `ArgumentException` wildcard). The exact scenario
   that made the original finding High — relative path + roots-unsupported client — produces
   guidance ("Client does not support workspace roots. Please provide an absolute path.")
   that **never reaches the client**. Library exceptions (`AnalysisException`,
   `ParsingException`) thrown during analysis are likewise stripped: "DbContext not found in
   file" becomes the generic message.
2. **[Medium, audit §4.4 #5 residual]** `get_class_diagram` *prepends* its >50-files warning
   ahead of the YAML front-matter (`ProjGraphTools.GetClassDiagramAsync`), which strict Mermaid
   parsers reject; `get_project_graph` already appends. The cached resource stores the same
   prepended form.
3. **[Medium, audit §4.4 #6 residual]** `get_erd` on a file with multiple DbContexts silently
   analyzes the first (`DbContextIdentifier.FindContextClass` → `FirstOrDefault`). The snapshot
   branch already errors with the candidate list; the DbContext branch does not.
4. **[Low, audit §5.4]** The snapshot branch does not validate a caller-supplied `contextName`
   against the discovered snapshot list — a typo falls through to `AnalyzeSnapshotAsync` and
   surfaces as a stripped generic error even though the code already holds the candidate list.

A fifth, structural gap keeps all of these invisible: the MCP "integration" suite hand-wires
`ProjGraphTools` with a `null!` server (audit §4.7 #18), so no test ever crosses the real SDK
boundary where the stripping happens.

## Design

### 1. `WorkspaceRootService` → `McpException`

All four throw sites in `TryResolveAsync`/`ResolveMatches` become `McpException` with the same
messages (the not-found message additionally gains "Provide an absolute path.", matching its
ambiguous-match sibling). The service lives in `ProjGraph.Mcp`, so the dependency already exists. The existing
`McpRootsTests` assertions on BCL exception types are updated to `McpException` — that is the
point of the change, not collateral damage.

### 2. Tool-boundary wrap for library exceptions

`ProjGraphTools` gains one private helper:

```csharp
private static async Task<T> RunAnalysisAsync<T>(Func<Task<T>> analysis)
{
    try { return await analysis(); }
    catch (ProjGraphException ex) { throw new McpException(ex.Message); }
}
```

Every call into `AnalysisServices` (graph, stats, class file/directory, EF context/snapshot,
snapshot/context discovery) goes through it. `ProjGraphException` is the library's base type,
so `AnalysisException`/`ParsingException` are both covered; unexpected BCL exceptions still
propagate unwrapped (they carry no user guidance worth preserving, and masking genuine bugs
as protocol errors would hide stack traces from the server log).

### 3. `get_erd` DbContext branch mirrors the snapshot branch

Before analyzing, the DbContext branch calls `EfService.DiscoverContextsAsync(path)`:

- 0 contexts → `McpException` "No DbContext found in '{path}'."
- `contextName` supplied but not in the list → `McpException` naming it and listing candidates.
- no `contextName` and ≥2 → `McpException` listing candidates: "Specify one using the
  contextName parameter."
- otherwise analyze the single/named context.

The snapshot branch gains the same supplied-name validation against the discovered list.
Behavior change: multiple contexts without `contextName` was silent-first, now an error —
deliberate; MCP callers are typically LLMs that need the signal (same rationale recorded for
`ownedMode` validation in PR #161).

### 4. `get_class_diagram` warning appended

The >50-files warning is collected as a plain string and run through the existing
`AppendWarningComments` (which trims and appends `%% WARNING:` lines after the diagram body),
identical to `get_project_graph`. The cached resource stores the appended form. Message text
unchanged ("Scanning {N} files. Large diagrams may be hard to read.").

### 5. One true end-to-end transport test

`Tests.Integration.Mcp` gains the `ModelContextProtocol` package and a test fixture that
launches the real server (`ProjGraph.Mcp.dll` from the test output, where the ProjectReference
copies it) over `StdioClientTransport` via `McpClient`. Minimum assertions:

- `ListToolsAsync` returns the four tools (transport + DI wiring smoke).
- `get_erd` with a **relative path** from a client **without roots capability** returns an
  error result whose text contains "provide an absolute path" — the exact High scenario.
  This test FAILS before fix 1 (message stripped to "An error occurred invoking…") and passes
  after: it is the red/green pin proving the conversion matters at the protocol level, which
  no hand-wired test can do.

## Out of scope

- Absolute-path root confinement (audit §4.4 #8, accepted risk).
- `WorkspaceRootService` negative caching for bare-name scans (audit §5.4 L, perf not errors).
- `CollectingOutputConsole.PromptSelectionAsync` booby trap (audit §5.4 L; unreachable today).
- CLI parity for multi-context (CLI already prompts interactively).
- CancellationToken plumbing (Phase 2 themed PR).
