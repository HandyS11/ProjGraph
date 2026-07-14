# ProjGraph — Phase 0 Re-Audit Report

**Date:** 2026-07-14 · **Branch:** `develop` (`3ddfe5c`) · **Scope:** full product (all `src/`, `tests/`, CI/CD, packaging, docs)

**Method:** central clean build + full test run + SonarQube review, then five parallel read-only deep-audit agents (dependency graph, class diagram, EF/ERD, CLI+MCP entry points, tests/CI/packaging/docs), per the [remaining-work design](2026-07-13-remaining-work-design.md) Phase 0. Each agent triaged its area's findings from the 2026-07-13 audit against current source and hunted for new defects. Both new High findings were independently re-verified against source by the coordinating session before inclusion.

This report supersedes the 2026-07-13 audit report (removed in PR #131; recoverable at `git show fe02010^:REVAMP_REPORT.md`). Original findings are referenced below by their old section numbers (§4.1–§4.8).

---

## 1. Executive summary

The revamp program (PRs #119–#150) delivered what it promised. Of the original 90 findings, roughly two-thirds are now verified fixed or structurally eliminated — including the entire §4.3 regex-fragility class, which is gone with the ~1,900-LOC regex layer the EF rewrite deleted. Of the 9 original Highs, 5 are cleanly fixed, 3 are partially fixed with narrow residuals, and 1 is untouched. No functional regressions were found in the fixed areas.

The audit surfaced **2 new High findings**, both in the freshly rewritten EF fluent-walker layer, plus **2 carried-forward High-grade items** from the original report:

1. **[new-H]** ModelSnapshot 1:1 relationships fabricate a phantom FK column (the two-string `HasForeignKey` overload is misparsed).
2. **[new-H]** The chained owned-type form (`.OwnsOne(c => c.Address).Property(...)` without a builder lambda) leaks owned properties onto the owner entity.
3. **[carried-H]** `publish.yml` still pins all actions by mutable tag in the one workflow holding `NUGET_API_KEY` (§4.6 #1, untouched).
4. **[carried-H]** `WorkspaceRootService` errors are still BCL exceptions that the MCP SDK strips to a generic message, so its guidance ("provide an absolute path…") never reaches clients (the surviving residual of §4.4 #1; tool-level guards were fixed).

One program-level regression: the **SonarQube quality gate on `develop` is now failing** (it was green at the last audit) — 12 new violations and 5.09% duplicated new lines, almost entirely from the new EF walker files. Nothing in it is severe (1 cognitive-complexity CRITICAL, 1 constructor-arity MAJOR, 10 INFO style hints), but the gate should be brought back to green before it normalizes being red.

Everything else new is Medium/Low: the themes are edge-case inputs the walkers don't cover yet (primitive collections, qualified/nullable `DbSet` forms), long-running-server staleness (MSBuild's global csproj cache), Mermaid output robustness (unsanitized identifiers, unquoted YAML titles), and docs/CI drift (library README quickstarts that throw at runtime, no PR-time docs build).

---

## 2. Baseline health check

| Check | Result |
| --- | --- |
| `dotnet build` (16 projects, warnings-as-errors) | ✅ clean |
| `dotnet test` (7 test projects) | ✅ 775/775 passed (was 688) |
| SonarQube quality gate (`develop`) | ❌ **ERROR** — new-code coverage OK (87.9%), but 12 new violations (threshold 0) and 5.09% duplicated new lines (threshold 3%) |
| SonarQube overall | 89.2% coverage · 2.4% duplication · 7,321 LOC |
| MCP stdout invariant (JSON-RPC only) | ✅ holds everywhere, re-verified incl. new code paths |
| EF golden harness | ✅ sound; 10 goldens map 1:1 to 10 cases, missing goldens fail loudly |

SonarQube gate detail: CRITICAL S3776 cognitive complexity 25 (`src/ProjGraph.Lib.Core/Parsers/ProjectParser.cs:135`), MAJOR S107 8-parameter constructor (`src/ProjGraph.Mcp/ProjGraphTools.cs:18`), 10 INFO IDE hints (IDE0007/0028/0042/0066/0305). Duplication by file: `FluentEntityWalker.cs` 47.1%, `FluentPropertyWalker.cs` 27.2%, `EntityConfigurationWalker.cs` 9.6%, `FluentRelationshipWalker.cs` 6.6% — the walkers share near-identical chain-walking/owner-resolution scaffolding.

---

## 3. High findings (queue ahead of Phase 2)

Every item here was verified against current source.

1. **[new-H]** `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs:323-342` — `ForeignKeyPropertyNames` treats *every* string-literal argument of `HasForeignKey` as an FK property name, but the two-string overload `HasForeignKey("Ns.Dependent", "FkId")` — the form the EF snapshot generator always emits for one-to-one relationships — passes the dependent *entity type name* first. `MarkForeignKeys` (`:347-359`) then creates the missing property, so any ModelSnapshot containing a 1:1 relationship renders a fabricated column (e.g. `string Blogging.BlogHeader FK`) on the dependent entity. The golden suite only exercises the single-string form. **Fix:** when the invocation has ≥2 string arguments, skip the first if it names a known entity (after last-segment stripping) or contains a dot.

2. **[new-H]** `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs:85-92,215-234` (same pattern in `FluentEntityWalker.cs:109-116,215-234`) — nested-builder exclusion is lexical (argument-list span only), but owner resolution walks the receiver chain *through* builder-changing calls. For the chained owned-type form used throughout Microsoft's docs — `modelBuilder.Entity<Customer>().OwnsOne(c => c.Address).Property(a => a.City).HasMaxLength(50);` — the `Property` call is not inside any `OwnsOne` argument list, and `ResolveOwningEntity` steps receiver→`OwnsOne`→`Entity<Customer>` and returns `Customer`: a phantom `string City "max:50"` column lands on Customer. Same root cause leaks chained `.OwnsOne(...).ToTable(...)` and `.OwnsMany(...).HasKey(...)`. Tests/goldens cover only the lambda form. **Fix:** in the receiver walk, return `null` (or the owned-type scope) when a `NestedBuilderScopes` call is crossed before reaching `Entity`.

3. **[carried-H, §4.6 #1 — still-present]** `.github/workflows/publish.yml:17,41,67,83` — every action (`actions/checkout@v7`, `setup-dotnet@v4`, `softprops/action-gh-release@v2`, `nick-fields/retry@v3`) is pinned by mutable tag in the workflow holding `NUGET_API_KEY`, `id-token: write` and `contents/packages: write`; a hijacked tag is a full package-supply-chain compromise. Dependabot now bumps these tags (§4.6 #7 fixed) but bumps tags, not SHAs. **Fix:** pin to commit SHAs (Dependabot updates SHA pins too).

4. **[carried-H, §4.4 #1 — partial; residual is High-grade]** `src/ProjGraph.Mcp/WorkspaceRootService.cs:35-36,43-44,46-47,67-69` + `ProjGraphTools.cs:361` + library `AnalysisException`/`ParsingException` — tool-level guards now correctly throw `McpException`, but roots-resolution and library errors are still BCL exceptions, and ModelContextProtocol.Core **1.4.1** (verified against the shipped DLL) still replaces every non-`McpException` message with "An error occurred invoking '…'". The exact scenario that made this High — relative path + roots-unsupported client → guidance never reaches the client — is still live. **Fix:** convert the `WorkspaceRootService` throws to `McpException` and wrap library exceptions at the tool boundary.

---

## 4. Triage of original findings

Statuses: **fixed** · **partial** (fixed with residual, residual described) · **still-present** · **deferred** (deliberately deferred to Phase 2 by the remaining-work design, confirmed still present) · **obsolete** (code deleted by the EF rewrite).

### 4.1 Dependency graph & core parsing (18 findings: 12 fixed, 2 partial, 4 still-present)

| # | Sev | Original finding | Status | Evidence (current) |
|---|---|---|---|---|
| 1 | H | Skip-filter misses `InvalidProjectFileException`/`ParsingException` | **fixed** | `ProjectParser.cs:46-50` wraps into `ParsingException`; `BuildGraphUseCase.cs:128-133`, `ProjectDiscoveryService.cs:65-69` catch+warn+skip. Residual: `UnauthorizedAccessException` missing from both lists (new L below) |
| 2 | H | Duplicate solution entries → duplicate GUIDs → `ToDictionary` throws | **fixed** | `BuildGraphUseCase.cs:63,77-80,90-93` — OS-aware key dedupe + Id dedupe |
| 3 | M | Three inconsistent path-normalization policies | **partial** | `BuildGraphUseCase.cs:60-63` now matches `ProjectParser` Id semantics; `ProjectDiscoveryService.cs:126,136` `PathEqualityComparer` still always `OrdinalIgnoreCase` — on Linux two case-differing csproj files dedupe wrongly |
| 4 | M | Self-loop SCCs not counted as cycles | **fixed** | `ComputeStatsUseCase.cs:46-48`; `SolutionGraphRendererBase.cs:122-129` |
| 5 | M | Singleton renderers with mutable per-render state | **fixed** | Per-call `RenderContext` (`SolutionGraphRendererBase.cs:22-52`) |
| 6 | M | `Directory.Build.props` ignored → Framework "unknown" | **fixed** | `ProjectParser.cs:57-75,135-179`. Residual approximation: merges *all* ancestor props files and ignores `Condition` attributes |
| 7 | M | `ILogger<>` registration overrides host logging | **fixed** | `Lib.Core/DependencyInjection.cs:25` uses `TryAdd` |
| 8 | M | Tree renderer O(2^k) re-expansion / stack overflow | **fixed** | `TreeGraphRenderer.cs:137-141` collapsed "(see above)" references, one expansion per node |
| 9 | M | Mermaid node identity by sanitized name; labels unescaped | **fixed** | `MermaidGraphRenderer.cs:103-123` Guid-suffix disambiguation; `:145-148` quote escaping. Residual sanitization gap → new M below |
| 10 | L | `SlnParser` throws raw `InvalidProjectFileException` | **fixed** | `SlnParser.cs:34-42` |
| 11 | L | `SlnxParser` bypasses `IFileSystem` | **fixed** | `SlnxParser.cs:30-40` |
| 12 | L | Missing files / out-of-solution edges silently dropped | **still-present** | `BuildGraphUseCase.cs:70` silent `FileExists` filter; `:139-144` silent edge drop |
| 13 | L | `SolutionStats` docs say −1, impl emits null | **fixed** | `SolutionStats.cs:12-16,39-46` document `null` |
| 14 | L | `IsTempPath` bare `StartsWith`; `OrdinalIgnoreCase` on Linux | **partial** | `WorkspaceRootResolver.cs:97-100` separator-boundary fixed; still case-insensitive on Linux (negligible) |
| 15 | L | BCL metadata references rebuilt per call | **fixed** | `CompilationFactory.cs:17-18` static `Lazy` |
| 16 | L | CPM resolution walks to fs root, re-probes, ignores `VersionOverride` | **still-present** | `ProjectParser.cs:189-224`; also `m.Name == "Version"` at `:205` is case-sensitive |
| 17 | L | `GraphService` fake async cancellation | **still-present** | `GraphService.cs:16` — folds into the Phase 2 CancellationToken PR |
| 18 | L | `RelativePath` computed against process CWD | **still-present** | `ProjectParser.cs:53` |

### 4.2 Class diagrams (15 findings: 7 fixed, 3 partial, 1 still-present, 4 deferred)

| # | Sev | Original finding | Status | Evidence (current) |
|---|---|---|---|---|
| 1 | H | Bare-name `WellKnownTypes` match drops user `Task`/`Exception`/… | **partial** | `TypeFilter.cs:99-106` — list now applies only to error symbols / global-namespace types, so declared user types survive. Residual: an *unresolved* cross-file reference to a user type with a BCL-colliding name, and global-namespace declarations of such names, are still dropped |
| 2 | H | Cross-namespace name collisions bind to wrong type | **partial** | In-source/metadata symbols never hit discovery (`SymbolResolver.cs:38-51`); multi-match resolution deterministic (`WorkspaceTypeDiscovery.cs:70-77`). Residual: error-symbol lookups still keyed/matched by simple name only, no namespace/arity (`SymbolResolver.cs:53-57,103`) |
| 3 | H | Unmemoized full repo scans; resolution before system check | **fixed** | System check first (`TypeProcessor.cs:134-140`); memoization incl. negatives (`AnalysisContext.cs:52-56`) |
| 4 | M | `AddExternalType` duplicate nodes | **fixed** | `SymbolResolver.cs:130-137` |
| 5 | M | Node under `OriginalDefinition`, edge to constructed symbol | **fixed** | `TypeProcessor.cs:149-152` |
| 6 | M | Unresolved interface rendered as inheritance | **fixed** | `TypeProcessor.cs:168-176` reclassifies to Realization |
| 7 | M | Cardinality on wrong (source) side | **fixed** | `MermaidClassDiagramRenderer.cs:149-166` |
| 8 | M | Array-typed members produce no relationship | **partial** | Properties/fields fixed (`RelationshipAnalyzer.cs:116-133` unwraps arrays); **method return/parameter types still skip arrays** (`RelationshipAnalyzer.cs:83-88`) — `Order[] GetAll()` yields no edge while `List<Order>` does |
| 9 | M | Include flags default false → default run renders disconnected boxes | **still-present** | `AnalysisOptions.cs:17,19`; `TypeProcessor.cs:99-107`; CLI defaults false too (`ClassDiagramCommand.cs:51,60`) |
| 10 | L | Generic outer types discarded (`Result<Order>` loses `Result` edge) | **deferred** | `RelationshipAnalyzer.cs:168-201` (Phase 2 themed PR) |
| 11 | L | Collection detection by name substring | **deferred** | `RelationshipAnalyzer.cs:136-144` (Phase 2) |
| 12 | L | `StartsWith("System")` without dot boundary | **fixed** | `TypeFilter.cs:86-91,116-120` |
| 13 | L | Dedupe keys use simple names | **deferred** | `RelationshipAnalyzer.cs:53,56,93,155` (Phase 2) |
| 14 | L | `Sanitize` merges `Ns.Foo_Bar` / `Ns.Foo.Bar` | **deferred** | `MermaidClassDiagramRenderer.cs:198-212` (Phase 2) |
| 15 | L | One unreadable file aborts directory analysis | **fixed** | `AnalyzeDirectoryUseCase.cs:50-61`; `WorkspaceTypeDiscovery.cs:99-108` |

### 4.3 EF Core / ERD — rewrite intent check

The regex layer is deleted; §4.3 line-items are **obsolete by deletion** except where the rewrite had to preserve their intent. Intent check against the new walker layer:

| Old §4.3 concern | Outcome | Evidence (current) |
|---|---|---|
| #1 `IsRequired` honored (optional vs required) | **preserved** for explicit `.IsRequired(...)` (`FluentRelationshipWalker.cs:147,275-285`; golden `fixture-relationships.mmd`). Convention default still ignores FK nullability — see new L |
| #2/#3 no config leakage between chains (window scans) | **structurally eliminated** for sibling statements (receiver-spine owner resolution) — but a **new leak exists for chained owned-type builders** (new-H #2 above) |
| #4 `GetDirectoryName("")` crash | **corrected** — `EfModelAnalyzer.cs:135-139` `ResolveDirectory` fallback |
| #5 base-class `DbSet`s invisible | **corrected** (Slice 5) — base-type chain walked, derived-first-wins, cycle-safe |
| #6 `DbSet<Models.Blog>` / `DbSet<Blog>?` mishandled | **partial** — semantic discovery handles both (`EfModelAnalyzer.cs:393-410`); syntax-level *file* discovery still misses both forms (new M below) |
| #7 expression-bodied `OnModelCreating` ignored | **corrected** — `FluentApiConfigurationParser.cs:29`, `ModelSnapshotParser.cs:33` |
| #8 owned-type lambdas create phantom owner properties | **preserved for the lambda form** (`NestedBuilderScopes` span check; golden `fixture-owned-join.mmd`); **not for the chained form** (new-H #2) |
| #9 `nameof(...)` fabricated as property name | **preserved** — only lambdas and string literals accepted; `nameof` degrades silently, never fabricates |
| #10 `IEntityTypeConfiguration<T>` unsupported | **corrected** (Slice 4) — `EntityConfigurationWalker` + cross-file discovery |
| #11 culture-sensitive default values | **corrected** — `DefaultValueResolver.cs:143-146` invariant culture |
| #12 join-table cleanup over-removes | **not corrected** — carried forward as M (below) |
| #13 `ToTable` schema overload / dead `TableName` | **partial** — schema overload parsed (`FluentEntityWalker.cs:154-206`); `TableName` still write-only (new L) |
| #14 dead truncation branch / whitespace split | **obsolete** (deleted) |
| #15 `DbContextIdentifier` name match without kind check | **corrected** — `DbContextIdentifier.cs:47-53,63-69` |
| #16 `IsInsideUsingEntityBlock` O(n²) | **obsolete** (deleted) |
| #17 static/indexer/computed properties emitted | **corrected** — `EntityAnalyzer.cs:44-47` |

### 4.4/4.5 MCP server & CLI (15 findings: 9 fixed, 4 partial, 1 still-present, 1 deferred)

| # | Sev | Original finding | Status | Evidence (current) |
|---|---|---|---|---|
| 1 | H | Validation errors stripped by MCP SDK | **partial** | Tool-level guards now `McpException` (`ProjGraphTools.cs:49,55,183,375,385,394`). Residual is High-grade — see §3 #4 |
| 2 | M | Skipped projects silent in MCP | **fixed** | `CollectingOutputConsole` (`Program.cs:51-52`), warnings surfaced in graph output (`ProjGraphTools.cs:147-149`) and stats JSON (`:205-207,336-356`); `AsyncLocal` isolation |
| 3 | M | Roots resolution files-only / raw search pattern | **fixed** | `WorkspaceRootService.cs:85-88` combine-first + traversal guard (`:102-117`), directories matched, wildcards rejected (`:65-70`) |
| 4 | M | No `roots/list_changed` handler | **fixed** | `WorkspaceRootService.cs:133-150` → invalidate + refetch |
| 5 | M | >50-files warning prepended breaks front-matter | **partial** | `get_project_graph` appends (`ProjGraphTools.cs:312-326`); **`get_class_diagram` still prepends** (`:71-73,106`) ahead of YAML front-matter (mermaid ≥10.2 tolerates; strict parsers break; cached resource affected too) |
| 6 | M | `get_erd` multi-DbContext silently analyzes first | **partial** | Snapshot branch errors with candidates (`ProjGraphTools.cs:259-265`); **DbContext branch unchanged** (`:285` → `DbContextIdentifier.cs:51-52` `FirstOrDefault`) |
| 7 | L | No `topN`/`maxDepth` validation | **fixed** | `ProjGraphTools.cs:46-50,181-184` |
| 8 | L | Absolute paths unconfined to roots | **still-present** | `WorkspaceRootService.cs:26-29` (accepted risk for a stdio-local tool; re-affirm or implement opt-in confinement) |
| 9 | L | No `CancellationToken` into class/EF analysis | **deferred** | Confirmed absent; Phase 2 plumbing PR |
| 10 | L | `DiagramResourceCache` race / unconditional notification | **partial (largely fixed)** | Notification only on update, `McpException`-guarded (`DiagramResourceCache.cs:101-118`); Add/Remove outside lock deliberate with rationale (`:90-99`); residual negligible |
| 11 | L | README documents `depth` vs schema `maxDepth` | **fixed** | `src/ProjGraph.Mcp/README.md:143` |
| 12 | M | CLI multi-file prompt resolves same-named files to first | **fixed** | `ErdCommand.cs:240-285` unique relative-path labels → dictionary |
| 13 | M | Unescaped user paths in Spectre markup | **fixed** | `Markup.Escape` at all five sites |
| 14 | L | erd auto-discovery descends bin/obj/.git, aborts on unreadable dir | **fixed** | `ErdCommand.cs:171-182,223-231` |
| 15 | L | CLI README default-format / `.mmd` redirect wrong | **fixed** | `src/ProjGraph.Cli/README.md:21-29` |

### 4.6/4.7/4.8 Build, CI, tests, docs (25 findings: 13 fixed, 3 partial, 6 still-present, 3 deferred)

| # | Sev | Original finding | Status | Evidence (current) |
|---|---|---|---|---|
| 1 | H | publish.yml mutable-tag pins with `NUGET_API_KEY` | **still-present** | See §3 #3 |
| 2 | M | `NoWarn` suppresses NU1901–NU1904 | **fixed** | `Directory.Build.props:18-19` — NU1901/NU1902 only + `NuGetAuditLevel=high` |
| 3 | M | Any `v*` tag publishes, no CI/branch gate | **still-present** | `publish.yml:3-6`; no `environment:` protection |
| 4 | M | NuGet push first, no rollback; prerelease hardcoded | **partial** | Ordering unchanged; prerelease now derived (`publish.yml:73`) |
| 5 | M | `setup-dotnet` without `global-json-file` | **fixed** | All 5 workflows pass it |
| 6 | M | docs-publish unfiltered trigger + cancel-in-progress | **fixed** | `docs-publish.yml:3-10,18-20,51`. But the fix removed all PR-time docs validation — new M below |
| 7 | M | No github-actions Dependabot ecosystem | **fixed** | `.github/dependabot.yml:13-16` |
| 8 | M | Coverlet filter can't match; no `--settings` | **partial** | Filter fixed (`.runsettings:8`); **`.runsettings` still never applied** — `ci.yml:49` and `sonarqube.yml` run `dotnet test` without `--settings`, and the SDK does not autodetect it (verified against `Microsoft.TestPlatform.targets`). The whole file is dead config in CI |
| 9 | L | No CI concurrency; unused coverage artifact | **partial** | Concurrency added (`ci.yml:12-14`); artifact still consumer-less (`:52-58`) |
| 10 | L | CodeQL no PR trigger | **fixed** | `codeql.yml:10-12` |
| 11 | L | Sonar: no PR gate; frozen scanner cache | **deferred** | `sonarqube.yml:5-8,33-37` (Phase 2 CI PR) |
| 12 | L | Blind `sleep 300` for NuGet validation | **deferred** | `publish.yml:79-80` (Phase 2 CI PR) |
| 13 | L | Matrix lacks macOS | **fixed** | `ci.yml:21` + `fail-fast: false` |
| 14 | L | Committed version 0.4.0 behind tags; fragile sed | **deferred** | `Directory.Build.props:22`, `server.json:4,14` (Phase 2 CI PR) |
| 15 | L | EFCore/Design version skew | **fixed** | Both 10.0.9 |
| 16 | L | No `RollForward` on global tool | **fixed** | `ProjGraph.Cli.csproj:6` |
| 17 | L | BOMs in csproj/props | **fixed** | Zero BOMs (scanned); the pre-commit hook itself is gone |
| 18 | M | MCP "integration" suite hand-wires tools, `null!` server, no e2e | **still-present** | `McpTestHelper.cs:62`; no `McpClient`/`StdioClientTransport` anywhere |
| 19 | M | `TestPathHelper` CWD climb; skip guards green-wash | **still-present** | `TestPathHelper.cs:16-17,29-31`; `GraphServiceTests.cs:40-44` |
| 20 | L | `SkipTestException` overload missing skip token | **fixed** | `SkipTestException.cs:31-33` |
| 21 | L | `Task.Delay(10)`+`BeAfter` clock race | **still-present** | `McpResourcesTests.cs:241,248` (low practical risk) |
| 22 | L | Delegation tests pin NSubstitute call shapes | **still-present** | `AnalyzeFileUseCaseTests.cs:49-61,77-79` |
| 23 | L | Fixed filename in shared machine temp | **fixed** | `McpRootsTests.cs` — per-test `TestDirectory` |
| 24 | H | CLAUDE.md phantom project/dependency | **fixed** | Architecture section matches reality; zero Buildalyzer mentions |
| 25 | L | Ownership-comment casing drift | **fixed** | All three locations agree on `io.github.HandyS11/projgraph` |

---

## 5. New findings

New Highs are in §3. Severity: **[M]** Medium · **[L]** Low. Paths relative to repo root.

### 5.1 Dependency graph & core parsing

- **[M]** `src/ProjGraph.Lib.Core/Parsers/ProjectParser.cs:43,150,200` — `ProjectRootElement.Open(path)` uses MSBuild's global `ProjectRootElementCache`, which does not auto-reload explicitly-loaded entries from disk. In the long-running MCP server, an agent edits a csproj (adds a ProjectReference) and re-invokes the graph tool → the *stale cached* XML is returned and the new edge never appears until process restart; entries are also strongly rooted, so memory grows across many analyzed solutions. Fix: per-call `ProjectCollection` (+ `UnloadAllProjects()` in `finally`) or `root.Reload(false)` after `Open`.
- **[M]** `src/ProjGraph.Lib.Dependencies/Rendering/MermaidGraphRenderer.cs:131-137` — `SanitizeId` only replaces `.`, `-`, space; parentheses (`Legacy (old).csproj` → `Legacy_(old)[...]`) or a project named `end` (Mermaid reserved word) break the whole diagram. Fix: whitelist sanitize (`[^A-Za-z0-9_]` → `_`) + reserved-word prefix; existing Guid-suffix collision logic absorbs the merges.
- **[L]** `src/ProjGraph.Lib.Dependencies/Application/UseCases/BuildGraphUseCase.cs:97-100,124,139-144` — dependency edges never deduplicated; a repeated `ProjectReference` (bad merge) yields duplicate arrows and inflated hotspot in-degrees. Fix: `HashSet<(Guid,Guid,DependencyType)>`.
- **[L]** `BuildGraphUseCase.cs:65,106` — `(pkg.Name, pkg.Version)` keys compare case-sensitively but NuGet IDs are case-insensitive; `Newtonsoft.Json` vs `newtonsoft.json` yields two package nodes. Fix: normalize the key/id hash.
- **[L]** `src/ProjGraph.Lib.Core/Abstractions/MermaidFenceHelper.cs:29-31` — YAML front-matter `title:` unquoted; a filename containing `: ` produces invalid YAML and Mermaid rejects the diagram. Fix: quote + escape.
- **[L]** `ProjectParser.cs:46-47` + `BuildGraphUseCase.cs:128` — `UnauthorizedAccessException` missing from both skip-filter catch lists (inconsistent with `SlnParser.cs:38-39`, which catches it); an unreadable csproj surfacing it unwrapped aborts the whole graph. Fix: add to both lists.
- **[L]** `src/ProjGraph.Lib.Core/Infrastructure/WorkspaceRootResolver.cs:72-75` — `GetFiles`/`GetDirectories` on an unreadable ancestor throws during the upward walk. Fix: catch-and-continue per level.
- **[L]** `src/ProjGraph.Lib.Core/Infrastructure/CompilationFactory.cs:87-97,139-144` — `TryAddReference` catches only `FileNotFoundException`; `FileLoadException`/`BadImageFormatException`/empty `assembly.Location` poison the static `Lazy` for the process lifetime. Fix: broaden the catch.

### 5.2 Class diagrams

- **[M]** `src/ProjGraph.Lib.ClassDiagram/Application/UseCases/AnalyzeFileUseCase.cs:38` — `GetDirectoryName(filePath) ?? Environment.CurrentDirectory` guards `null` but not `""` (bare relative filename): `projgraph class Foo.cs -i` with any unresolved non-BCL reference → `new DirectoryInfo("")` (`WorkspaceRootResolver.cs:17`) throws `ArgumentException` and aborts the analysis. Fix: `GetDirectoryName(GetFullPath(filePath))` or fall back on empty too.
- **[M]** `src/ProjGraph.Lib.ClassDiagram/Infrastructure/RelationshipAnalyzer.cs:83-88` — the array unwrap added for members was not mirrored in the method-dependency loop (`INamedTypeSymbol` guard): `Order[] GetAll()` / `void Save(Order[] batch)` produce no edge. Fix: unwrap `IArrayTypeSymbol` before the guard. *(Residual of original §4.2 #8.)*
- **[L]** `WorkspaceTypeDiscovery.cs:95,133` — directory *enumeration* unguarded (only per-file reads are wrapped); a mid-scan `IOException` (deleted dir, symlink cycle) aborts the analysis. Fix: same catch-and-skip as `DiscoverCsFilesUseCase.cs:36-54`.
- **[L]** `SymbolResolver.cs:139-145` — external-type nodes use bare `Name`, dropping generic arity (`AbstractValidator` vs in-source `Bar~T~`). Fix: short display format.
- **[L]** `AnalyzeDirectoryUseCase.cs:40,95` — trailing separator in input (`./src/`) → `Path.GetFileName` returns `""` → diagram title silently omitted. Fix: `TrimEndingDirectorySeparator`.
- **[L]** `WorkspaceTypeDiscovery.cs:33-52` — the optimistic common-dir pass overrides the deterministic tie-break and is fully re-scanned by the root pass on miss (double IO). Fix: one ranked root scan or skip visited dirs.

### 5.3 EF Core / ERD

- **[M]** `src/ProjGraph.Lib.EntityFramework/Infrastructure/RelationshipAnalyzer.cs:353-385` — join-table cleanup removes *all* relationships between the joined pair, not just the replaced many-to-many: `User ⇄ Group` M2M plus a legitimate `Group.Owner → User` OneToMany → the OneToMany is silently dropped. Fix: remove only ManyToMany-typed (or the exact converted) relationships. *(Carried from old §4.3 #12 — the rewrite did not correct it.)*
- **[M]** `Infrastructure/NavigationPropertyAnalyzer.cs:48-54` — generic collections classified as navigations without element-type entity candidacy: EF 8+ primitive collections (`List<string> Tags`) are skipped as columns *and* produce no relationship — the property vanishes from the ERD. Fix: require `IsEntityCandidate(elementType)`.
- **[M]** `Infrastructure/EntityFileDiscovery.cs:176-188` — file-level DbSet extraction requires `GenericNameSyntax`: `DbSet<Blog>?` (nullable wrapper) skipped, `DbSet<Models.Blog>` stored as unmatched `"Models.Blog"` — the entity's file never joins the compilation and the entity renders column-less. Fix: unwrap `NullableTypeSyntax`, take the last identifier segment. *(Residual of old §4.3 #6.)*
- **[L]** `Infrastructure/DefaultValueResolver.cs:37` — `Trim('"', '\'', ' ')` strips quotes asymmetrically: `HasDefaultValueSql("N'unrated'")` renders `default:N'unrated` (mangled form currently blessed in `goldens/fixture-snapshot.mmd`).
- **[L]** `src/ProjGraph.Core/Models/EfModel.cs:49` — `EfEntity.TableName` is write-only: populated by `ApplyTableName`, read by nothing. Render it or delete it. *(Carried from old §4.3 #13.)*
- **[L]** `Infrastructure/FluentRelationshipWalker.cs:377-390` — convention default `IsRequired ?? true` ignores FK nullability (`int? OwnerId` with no explicit `IsRequired` renders required); semantic drift from EF conventions, currently golden-blessed.
- **[L]** `Infrastructure/EntityFileDiscovery.cs:277,367,595` + `EfModelAnalyzer.cs:292,512` — degradation catches only `IOException`; `UnauthorizedAccessException` escapes and fails the analysis, contradicting the degrade-never-throw intent.
- **[L]** `Infrastructure/FluentEntityWalker.cs:139-141`, `EntityConfigurationWalker.cs:162-164`, `EntityAnalyzer.cs:79-82` — `GetSymbolsWithName(name).FirstOrDefault()` picks an arbitrary type on cross-namespace simple-name collision (snapshot `Entity("Ns.T")` strips the namespace before lookup).
- **[L]** `Infrastructure/DbContextIdentifier.cs:18-23` — `Contains("DbContext")` on base-type text misidentifies `IDesignTimeDbContextFactory<T>`/`IDbContextFactory<T>` implementers as contexts; factory-before-context file order yields an empty model.
- **[L]** `Infrastructure/FluentPropertyWalker.cs:352-361` — text-based arg parsing: `IsRequired(required: true)` compares unequal to `"true"` → marked optional; constant args to `HasMaxLength` silently ignored (while `HasDefaultValue` resolves constants).
- **[L]** `Infrastructure/EfModelAnalyzer.cs:86-92` — snapshot path skips `DeduplicateModelContent` (context path runs it at `:322`): OneToOne + OneToMany between the same pair draws two edges in snapshot ERDs.
- **[L]** `Infrastructure/EntityConfigurationWalker.cs:44-53` — config classes deduped by simple `ClassName`; with `ApplyConfigurationsFromAssembly`, two same-named config classes in different namespaces apply only the first.

### 5.4 MCP server & CLI

- **[L]** `src/ProjGraph.Mcp/ProjGraphTools.cs:257-258` — snapshot-branch `contextName` not validated against the discovered snapshot list; a typo yields the generic stripped error instead of the candidate list the code already holds. Fix: validate + `McpException` with candidates.
- **[L]** `src/ProjGraph.Cli/Commands/ClassDiagramCommand.cs:83-86` — CLI accepts negative `--depth` (MCP rejects it); `--depth -5` silently behaves as depth 0. Fix: `Settings.Validate()` check.
- **[L]** `src/ProjGraph.Mcp/WorkspaceRootService.cs:198-238` — unresolvable bare names trigger a full recursive scan of every workspace root on every call, no negative caching; each mistyped call pays a multi-second walk on a monorepo. Fix: cache misses per roots-generation.
- **[L]** `src/ProjGraph.Mcp/CollectingOutputConsole.cs:47-53` — `PromptSelectionAsync` silently returns the first choice; unreachable today but a booby trap for any future prompting library path. Fix: throw `McpException` requesting an explicit parameter.

### 5.5 Build, CI, tests, docs

- **[M]** SonarQube quality gate on `develop` is failing (see §2): 12 new violations + 5.09% duplicated new lines from the EF walker PRs. Fix: `dotnet format`-style IDE hints (10 INFO), refactor `ProjectParser` complexity (S3776) and `ProjGraphTools` ctor arity (S107), and extract the shared walker scaffolding (chain walking / owner resolution duplicated across `FluentEntityWalker`/`FluentPropertyWalker`/`EntityConfigurationWalker`).
- **[M]** `src/ProjGraph.Lib.EntityFramework/README.md:48`, `src/ProjGraph.Lib.ClassDiagram/README.md:51`, `src/ProjGraph.Lib/README.md:64,77` — quickstart code resolves concrete renderers (`GetRequiredService<MermaidErdRenderer>()`) that are registered only as `IDiagramRenderer<T>`; a user copying the shipped NuGet README gets `InvalidOperationException` at runtime. Fix: resolve the interface in the examples (or register concretes).
- **[M]** `.github/workflows/docs-publish.yml:3-11` + `CONTRIBUTING.md:116` — no `pull_request` trigger builds DocFX, yet CONTRIBUTING promises PR-time docs validation; a broken cross-doc link merges green and stalls Pages. The §4.6 #6 fix removed the only pre-merge docs signal. Fix: `pull_request` trigger running the build job only.
- **[L]** `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs:103-110` — with `UPDATE_EF_GOLDENS=1` all golden tests pass unconditionally and orphaned goldens go undetected; the env var leaking into CI would green-wash every ERD regression. Fix: assert `actual == written` after writing + one goldens-set-equals-cases test.
- **[L]** `publish.yml:76-77` — `mcp-publisher` fetched via `curl .../latest | tar`: unpinned, no checksum, in the release workflow. Fix: pin version + verify sha256.
- **[L]** `docs-publish.yml:6-8` — path filter omits `src/**`, but DocFX generates API reference from source XML docs → published API docs go stale until an unrelated markdown edit. Fix: add `src/**`.
- **[L]** `CLAUDE.md:66,68` — stale: renderers are *not* "resolved by keyed DI" (plain multi-registration filtered by `Format`), and MCP overrides `IOutputConsole` with `CollectingOutputConsole` (since PR #137), not `NullOutputConsole`. One-paragraph fix.
- **[L]** `UPDATE_EF_GOLDENS` and the golden harness are undocumented outside the test file; a contributor's first contact is a red test. Fix: a paragraph in CONTRIBUTING.

### Coverage gaps (aligned with the defects above; each untested today)

`ModelSnapshotParser` direct units (attribute-less context-name fallback; missing-`BuildModel` → silently-empty model); snapshot shapes beyond the single golden (owned types in snapshots, `HasIndex`/`HasAnnotation` tolerance, TPH discriminators, composite string-array `HasForeignKey` — the new-H #1 case); EF `RelationshipAnalyzer` (join synthesis + cleanup) has no dedicated test class; Slice-4 negative tests (config for unknown entity, duplicate config classes); chained (non-lambda) `OwnsOne` forms (new-H #2); the two-string `HasForeignKey` overload (new-H #1); one true end-to-end MCP test over a real transport (§4.7 #18, still open); primitive-collection properties; `DbSet<Blog>?` / qualified `DbSet` fixtures.

---

## 6. Updated Phase 2 queue

Per the remaining-work design, new Highs jump the queue. Recommended sequencing:

**Immediately (before themed Lows):**

1. **EF walker Highs** — new-H #1 (two-string `HasForeignKey`) + new-H #2 (chained owned-type leak), one PR with fixtures/goldens for both forms; fold in the three EF Mediums (§5.3: join-table over-removal, primitive collections, `EntityFileDiscovery` DbSet forms) which touch the same files and gaps.
2. **Release-pipeline High** — SHA-pin `publish.yml` actions (§3 #3); optionally fold the deferred CI items (§4.6 #11/#12/#14) into the same themed CI PR as originally planned, plus the new docs-PR-trigger and mcp-publisher-pinning items.
3. **MCP error-surfacing residual** — convert `WorkspaceRootService`/tool-boundary exceptions to `McpException` (§3 #4); fold in the two MCP Medium residuals (class-diagram warning placement, multi-DbContext silent-first) — same file, same theme.
4. **Sonar gate back to green** — style fixes + walker dedup extraction (§5.5 first item). Small, unblocks trusting the gate again.

**Then the planned Phase 2 themed PRs (unchanged from the design):**

5. Class-diagram output Lows (§4.2 #10/#11/#13/#14, confirmed still present) — plus the two new class-diagram Mediums (§5.2) which are the same theme.
6. CancellationToken plumbing (§4.1 #17 + §4.4 #9, confirmed still present).
7. Remaining new Lows batched opportunistically (good first issues); none urgent.

**Explicitly re-affirmed as accepted/deferred:** §4.4 #8 (absolute-path confinement — stdio-local tool), §4.1 #12/#16/#18 (parser Lows), test-quality items §4.7 #19/#21/#22 (batch when convenient), original §4.7 #18 (true e2e MCP test — recommend folding into the MCP error-surfacing PR since it is the only way to regression-test SDK error stripping).

---

*Generated by a five-track parallel re-audit (Fable 5) on 2026-07-14. Both new High findings independently re-verified against source by the coordinating session; line numbers valid as of commit `3ddfe5c`.*
