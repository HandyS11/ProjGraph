# Plan — MCP error surfacing

Spec: docs/superpowers/specs/2026-07-16-mcp-error-surfacing-design.md
Branch: revamp/mcp-error-surfacing (off develop @ 7f4edc6)

## Global constraints

- TDD per task: failing test first, then the fix, then full suite.
- Prefer `dtk` over raw `dotnet`. Full suite: `dtk test ProjGraph.slnx`. Format gate:
  `dotnet format ProjGraph.slnx --verify-no-changes` (CI enforces it).
- No EF golden may move — nothing here touches rendering of entities.
- MCP stdout invariant: no code path may write to stdout outside JSON-RPC.

## Task A — e2e transport harness + the High's red/green pin

1. Add `<PackageReference Include="ModelContextProtocol"/>` to
   `tests/ProjGraph.Tests.Integration.Mcp/ProjGraph.Tests.Integration.Mcp.csproj` (version is
   centrally pinned at 1.4.1 in Directory.Packages.props).
2. New `Helpers/McpServerFixture.cs` (or per-test helper): create `McpClient` over
   `StdioClientTransport` running `dotnet ProjGraph.Mcp.dll` from `AppContext.BaseDirectory`
   (ProjectReference copies the exe + runtimeconfig there; verify — if runtimeconfig is
   missing, fall back to the source bin path via `TestPathHelper`).
3. New `McpTransportTests.cs`:
   - `ListTools_OverStdio_ReturnsAllFourTools`.
   - `GetErd_RelativePath_NoRootsCapability_ErrorMentionsAbsolutePath`: call `get_erd` with a
     relative path; assert error result text contains "provide an absolute path".
4. RED: second test fails with the generic stripped message. Commit test-first only if the
   suite stays runnable; otherwise commit together with Task B's fix noting the observed RED.

## Task B — `McpException` conversions (makes A green)

1. `WorkspaceRootService`: four throw sites → `McpException`, messages unchanged.
2. `ProjGraphTools`: add `RunAnalysisAsync<T>` wrapping `ProjGraphException`; route every
   `analysisServices.*` / `EfService.Discover*` call through it.
3. Update `McpRootsTests` BCL-type assertions → `McpException` (message assertions unchanged).
4. Unit-level pin: hand-wired `GetErdAsync` on a .cs file with no DbContext now surfaces
   `McpException` "DbContext not found in file" (was `AnalysisException`).
5. Full suite + confirm Task A's pin is green over the real transport.

## Task C — multi-DbContext candidates + snapshot name validation

1. Tests (hand-wired, McpErdTests):
   - two DbContexts, no contextName → `McpException` listing both names.
   - two DbContexts, contextName = second → analyzes it (diagram contains its entity).
   - contextName typo (context file) → `McpException` with candidates.
   - snapshot file, contextName typo → `McpException` with candidates (was stripped generic).
   - zero DbContexts covered by Task B's pin.
2. Implement per spec §3 in `GetErdAsync` only — `DbContextIdentifier` stays untouched (the
   CLI's interactive prompt path also uses it).

## Task D — class-diagram warning placement

1. Test (hand-wired, directory with >50 .cs files via `TestDirectory`): output starts with
   front-matter/diagram, warning appears as a trailing `%% WARNING:` line; cached resource
   matches the returned string.
2. Reuse `AppendWarningComments`; delete the prepend.

## Wrap-up

- `dotnet format` clean, full suite green, no golden drift.
- Update CLAUDE.md only if behavior documented there changed (it isn't).
- PR to develop titled `fix(mcp): surface actionable errors through the MCP boundary`.
