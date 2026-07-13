# ProjGraph — Revamp Audit Report

**Date:** 2026-07-13 · **Branch:** `develop` (`52c4632`) · **Scope:** full product (all `src/`, `tests/`, CI/CD, packaging, docs)

**Method:** clean build + full test run, SonarQube review, then five parallel deep-audit passes (dependency graph, class diagram, EF/ERD, CLI+MCP entry points, tests/CI/packaging/docs). Every High-severity finding was re-verified against the source before inclusion.

---

## 1. Executive summary

The foundation is genuinely good: the build is clean under `TreatWarningsAsErrors` with five analyzer packs, all **688 tests pass**, the SonarQube quality gate is green with **zero open issues**, packaging metadata is correctly wired, and the critical MCP invariant (nothing but JSON-RPC on stdout) holds everywhere. Dependencies are kept current by Dependabot.

The audit still surfaced **90 findings (9 High / 38 Medium / 43 Low)**. That is not a contradiction: the existing gates measure style, coverage and hygiene — they cannot see *semantic* correctness of the diagrams the tool produces. Almost every defect sits in exactly the places the test suite doesn't reach, and most share four root causes:

1. **Type/project identity by bare string name.** The class-diagram pipeline filters, resolves and renders types by simple name; the graph renderer keys Mermaid nodes on sanitized file names. Result: user types named `Task`/`Exception` silently vanish, same-named types across namespaces bind to the wrong node, and distinct projects can merge into one diagram node.
2. **Regex-over-text parsing of EF Fluent API.** The regex layer scans flat match lists without statement boundaries, so configuration leaks between unrelated chains (wrong cardinality, phantom relationships, fabricated properties). Common real-world patterns (`IEntityTypeConfiguration`, expression-bodied `OnModelCreating`, base-class `DbSet`s) are silently ignored.
3. **Errors that never reach the user.** Exception filters don't catch the exceptions MSBuild actually throws (one malformed csproj aborts a 100-project graph); in the MCP server every carefully-worded validation message is stripped to a generic error by the SDK, and "project skipped" warnings go to a `NullOutputConsole` + `NullLogger` void — clients get confidently wrong, partial output with no signal.
4. **Release-pipeline trust gaps.** The one workflow holding the NuGet API key uses mutable-tag action pins, publishes on any `v*` tag with no CI gate, floats the SDK version, and the repo globally suppresses NuGet vulnerability-audit warnings (NU1901–NU1904).

A "little revamp" is the right call — no rewrite is needed. Section 3 is the recommended revamp scope (~2 focused batches); Section 4 has every finding with file:line.

---

## 2. Baseline health check

| Check | Result |
| --- | --- |
| `dotnet build` (16 projects, warnings-as-errors) | ✅ clean |
| `dotnet test` (7 test projects) | ✅ 688/688 passed |
| SonarQube quality gate (`develop`) | ✅ OK — 0 open issues, 100% new-code coverage, 0% duplication |
| TODO/FIXME markers in source | none |
| Recent history | Dependabot bumps only — deps current, product in maintenance mode |

**Verified non-issues** (checked and confirmed healthy): MCP stdout safety (renderers write to `StringWriter`, logging forced to stderr, `NullOutputConsole` wins registration order); iterative Tarjan SCC implementation is correct incl. self-loops at the algorithm level; `DiagramResourceCache` LRU bounded at 50 and mostly correctly locked; class-diagram depth limiting has no off-by-one; CLI test global-state mutation properly serialized via `[Collection]`; root README CLI examples match actual command options; `mcp-name` ownership comment is the last line of the MCP README and matches `server.json`.

---

## 3. Recommended revamp scope (priority shortlist)

**Batch 1 — correctness & trust (High severity, small diffs):**

1. **Crash-proof graph building** — add `InvalidProjectFileException` + `ParsingException` to the skip-and-warn filters (`BuildGraphUseCase`, `ProjectDiscoveryService`, `ProjectParser`) and dedupe solution entries before parsing (duplicate paths currently produce duplicate GUIDs that crash every downstream `ToDictionary`). *(§4.1 #1–2)*
2. **Fix class-diagram type identity** — stop dropping user types whose simple name collides with `WellKnownTypes`; resolve related types by namespace + arity, preferring symbols already in the compilation. *(§4.2 #1–2)*
3. **Kill the workspace-scan blowup** — memoize `WorkspaceTypeDiscovery` lookups and skip resolution for system types / already-resolved symbols (today: 100 structs = 100 full-repo scans). *(§4.2 #3)*
4. **Fix EF relationship parsing** — honor the `isRequired` parameter in `CreateShadowRelationship` and stop match-window scans at chain boundaries (`FindWithMethodInfo`, `IsRelationshipRequired`). *(§4.3 #1–3)*
5. **Make MCP errors reach clients** — throw `McpException` on all tool guard paths (SDK strips every other exception to a generic message), and surface "project skipped" warnings in tool results. *(§4.4 #1–2; also fix the `TryAdd` `NullLogger<>` registration, §4.1 #7)*
6. **Pin the release pipeline** — SHA-pin actions in `publish.yml`, add a `github-actions` Dependabot ecosystem, pin the SDK via `global-json-file` in all workflows, gate tag publishes on CI, and stop suppressing NU1903/NU1904 vulnerability audits. *(§4.6 #1–5)*
7. **Fix CLAUDE.md** — the architecture section names a nonexistent project (`Lib.ProjectGraph` → actual `Lib.Dependencies`) and a nonexistent dependency (Buildalyzer). *(§4.8 #1)*

**Batch 2 — product polish (Medium severity, high user impact):**

8. Render relationships between in-compilation types by default (today a plain `class-diagram <dir>` outputs disconnected boxes). *(§4.2 #9)*
9. Fix reversed UML cardinality and array/dictionary association handling in the class-diagram renderer. *(§4.2 #7–8)*
10. Fix the `erd` file-selection prompt (same-named files always resolve to the first) and escape Spectre markup in CLI output paths. *(§4.5 #1–2)*
11. Support directories in MCP roots resolution and refresh roots on `list_changed`. *(§4.4 #3–4)*
12. Add one true end-to-end MCP test that boots the real host over the SDK client. *(§4.7 #1)*

**Strategic (larger, schedule separately):** replace the EF regex-over-text layer with Roslyn syntax-tree walking (the compilation already exists — this eliminates the whole §4.3 fragile-window bug class at once) and add `IEntityTypeConfiguration` support, which is the dominant configuration pattern in real codebases.

---

## 4. Detailed findings

Severity: **[H]** High · **[M]** Medium · **[L]** Low. Paths relative to repo root.

### 4.1 Dependency graph & core parsing (`Lib.Core`, `Lib.Dependencies`) — 2H / 7M / 9L

1. **[H]** `src/ProjGraph.Lib.Dependencies/Application/UseCases/BuildGraphUseCase.cs:104` — skip-and-warn filter (`IOException or InvalidOperationException or XmlException`) doesn't catch `Microsoft.Build.Exceptions.InvalidProjectFileException` (what `ProjectRootElement.Open` actually throws) nor the library's own `ParsingException`. One malformed csproj aborts the entire graph instead of being skipped. Same gap at `ProjectDiscoveryService.cs:64` and `ProjectParser.cs:118`.
2. **[H]** `BuildGraphUseCase.cs:60-71` — project paths are never deduplicated and IDs are deterministic per path, so a solution listing the same project twice produces two `Project` records with the same GUID; every downstream `ToDictionary(p => p.Id)` (Tarjan, stats, tree renderer) throws `ArgumentException`.
3. **[M]** `BuildGraphUseCase.cs:56,116` + `ProjectDiscoveryService.cs:125` — `pathToProject` is case-sensitive (drops edges on Windows when reference casing differs) while `PathEqualityComparer` is always case-insensitive (can merge distinct projects on Linux). Three mutually inconsistent path-normalization policies exist; unify into one platform-aware comparer.
4. **[M]** `ComputeStatsUseCase.cs:45` + `Rendering/SolutionGraphRendererBase.cs:115` — cycle detection only counts SCCs with >1 node; a self-referencing project yields `HasCycles = false` and silently understated depths (Kahn's runs on a cyclic graph).
5. **[M]** `Rendering/SolutionGraphRendererBase.cs:20-46` + `DependencyInjection.cs:26-33` — renderers hold mutable per-render instance state (`RenderConsole`, `OutputWriter`) yet are registered as singletons with a "stateless" comment; concurrent renders interleave output. Pass a render context down the call chain instead.
6. **[M]** `src/ProjGraph.Lib.Core/Parsers/ProjectParser.cs:46-59` — only literal csproj properties are read; `Directory.Build.props` imports are ignored, so any repo with a central `<TargetFramework>` (including ProjGraph itself) reports `Framework = "unknown"` for every project.
7. **[M]** `src/ProjGraph.Lib.Core/DependencyInjection.cs:23` — `AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))` is not `TryAdd`, so it *overrides* host logging: in the MCP server all `ILogger<T>` resolve to `NullLogger` and the stderr logging config is dead code. Combined with `NullOutputConsole`, skip warnings are completely invisible in MCP mode.
8. **[M]** `Rendering/TreeGraphRenderer.cs:99-150` — shared subtrees are fully re-expanded per referrer (O(2^k) nodes on layered graphs → effective hang at ~30 projects) and the recursion can overflow the stack on deep chains. Render revisited nodes as collapsed references.
9. **[M]** `Rendering/MermaidGraphRenderer.cs:98-104,44,54` — Mermaid node identity is the sanitized *name only*: same-named projects in different directories (two `Benchmarks.csproj`) merge into one node; labels don't escape `"` or Mermaid-significant chars. Derive IDs from the project GUID.
10. **[L]** `Parsers/SlnParser.cs:28` — malformed `.sln` throws raw `InvalidProjectFileException` while `SlnxParser` wraps into `ParsingException`; inconsistent error contract.
11. **[L]** `Parsers/SlnxParser.cs:33` — `XDocument.Load(path)` bypasses the injected `IFileSystem`; only `XmlException` handled (`IOException` escapes unwrapped).
12. **[L]** `BuildGraphUseCase.cs:62,114-119` — missing project files and edges to out-of-solution projects are silently dropped; emit warnings.
13. **[L]** `src/ProjGraph.Core/Models/SolutionStats.cs:12-16` — XML docs say depths are `-1` when cyclic; implementation emits `null`. Docs contradict the shipped MCP JSON contract.
14. **[L]** `Infrastructure/WorkspaceRootResolver.cs:93-97` — `IsTempPath` bare `StartsWith` treats `/tmpfoo/project` as under `/tmp`; also `OrdinalIgnoreCase` on case-sensitive Linux.
15. **[L]** `Infrastructure/CompilationFactory.cs:55-70` — BCL metadata references rebuilt (full PE reads) on every `CreateCompilation` call; cache in a static `Lazy<>`.
16. **[L]** `Parsers/ProjectParser.cs:94-128` — CPM version resolution walks to filesystem root (can pick up unrelated `Directory.Packages.props` above the repo), re-probes per package per level, ignores `VersionOverride`.
17. **[L]** `Application/GraphService.cs:16` — `Task.Run(..., ct)` is fake async cancellation; the token never reaches the parse loop, so MCP aborts/Ctrl-C don't stop long parses.
18. **[L]** `Parsers/ProjectParser.cs:43` — `RelativePath` computed against process CWD; in MCP mode this degrades into `../../..` chains leaking the server's environment. Compute against the solution directory.

### 4.2 Class diagrams (`Lib.ClassDiagram`) — 3H / 6M / 6L

1. **[H]** `Infrastructure/TypeFilter.cs:93` — `IsSystemType` matches `WellKnownTypes` by bare name regardless of namespace: user domain types named `Task`, `Queue`, `Stack`, `Exception`, `Uri`… are silently dropped from the diagram along with all their edges. Only consult the name list for namespace-less/error symbols.
2. **[H]** `Infrastructure/WorkspaceTypeDiscovery.cs:77` + `Infrastructure/SymbolResolver.cs:81` — cross-namespace name collisions bind to the wrong type: discovery picks the alphabetically-first file declaring *any* type with that simple name; declaration matching ignores namespace and generic arity (`Result` matches `Result<T>`). Prefer symbols already in the compilation; match by namespace + arity.
3. **[H]** `Infrastructure/WorkspaceTypeDiscovery.cs:28-53` + `Infrastructure/TypeProcessor.cs:137,144` + `Infrastructure/RelationshipAnalyzer.cs:24` — every unresolved symbol triggers a full recursive repo scan with no memoization, and resolution runs *before* the system-type check, so every struct (`System.ValueType`) and enum (`System.Enum`) fires a pointless scan: 100 structs = 100 repo scans.
4. **[M]** `Infrastructure/SymbolResolver.cs:98-117` — `AddExternalType` never checks `AnalyzedTypeFullNames` first (the `.Add` result is ignored), so an external type referenced by N types produces N duplicate nodes.
5. **[M]** `Infrastructure/TypeProcessor.cs:141` — node registered under `OriginalDefinition` (`BaseRepo<T>`) but edge targets the *constructed* symbol (`BaseRepo<Customer>`): edge points at an auto-created empty node while the declared node dangles. Fall back to `relatedSymbol.OriginalDefinition`.
6. **[M]** `Infrastructure/RelationshipAnalyzer.cs:24-27` — relationship kind decided from the pre-resolution symbol: an unresolved first interface in a base list renders as solid inheritance (`<|--`) instead of realization. Re-classify after resolution when the resolved symbol is an interface.
7. **[M]** `Rendering/MermaidClassDiagramRenderer.cs:151-159` — cardinality emitted on the source side: a `List<Address>` property reads as "many Users have one Address" — UML semantics reversed (and a test pins the wrong output).
8. **[M]** `Infrastructure/RelationshipAnalyzer.cs:113-116,85` — array-typed members (`Order[]`) produce no relationship at all (fails the `INamedTypeSymbol` guard); unwrap `IArrayTypeSymbol.ElementType`.
9. **[M]** `Application/AnalysisOptions.cs:17-19` — `IncludeInheritance`/`IncludeDependencies` default to `false` *and* gate in-compilation relationship extraction, so a default `class-diagram <dir>` run renders zero edges — just disconnected boxes. Always emit relationships between types already in the compilation; let flags gate only workspace discovery. *(Agent-rated Low; bumped — this is the default UX of the feature.)*
10. **[L]** `Infrastructure/RelationshipAnalyzer.cs:146-171` — user-defined generic outer types are discarded (`Result<Order>` keeps only the `Order` edge); keep non-system outers.
11. **[L]** `Infrastructure/RelationshipAnalyzer.cs:119-126` — collection detection by name substring: `Dictionary<,>`/`IAsyncEnumerable<>` get cardinality "1"; any user generic containing "Set"/"List" gets "*". Test for `IEnumerable` implementation instead.
12. **[L]** `Infrastructure/TypeFilter.cs:86` — `StartsWith("System")` without dot boundary: namespaces like `Systems.Combat` are excluded wholesale.
13. **[L]** `Infrastructure/RelationshipAnalyzer.cs:53-56,93,133` — dedupe keys use simple names (`A.Config` vs `B.Config` → only first gets an edge); a type used as property and parameter gets duplicate parallel edges.
14. **[L]** `Rendering/MermaidClassDiagramRenderer.cs:197-211` — `Sanitize` maps `Ns.Foo_Bar` and `Ns.Foo.Bar` to the same node ID, silently merging distinct types.
15. **[L]** `Application/UseCases/AnalyzeDirectoryUseCase.cs:49-53` + `WorkspaceTypeDiscovery.cs:99` — per-file reads have no error handling; one unreadable/deleted-in-flight file aborts the whole directory analysis.

### 4.3 EF Core / ERD (`Lib.EntityFramework`) — 1H / 9M / 7L

1. **[H]** `Infrastructure/RelationshipConfigParser.cs:338-351` — `CreateShadowRelationship` hard-codes `IsRequired = true` in both OneToMany arms, discarding the computed `isRequired` parameter; optional relationships render as required (`||--o{` instead of `|o--o{`), and the correct convention-derived relationship is deduped away because fluent results are added first.
2. **[M]** `Infrastructure/RelationshipConfigParser.cs:310-323` — `FindWithMethodInfo` scans up to 9 following matches with no stop at the next `HasOne`/`HasMany`/`Entity` boundary: an unterminated `HasOne` pairs with the *next* chain's `WithOne`, emitting a spurious relationship between the wrong entities.
3. **[M]** `Infrastructure/RelationshipConfigParser.cs:255-270` — `IsRelationshipRequired` has the same unbounded window: a later property-chain `.IsRequired()` marks an unrelated relationship required.
4. **[M]** `Infrastructure/EfModelAnalyzer.cs:77,116` + `Infrastructure/EntityFileDiscovery.cs:117` — `GetDirectoryName` returning `""` (bare relative filename input) flows into `DirectoryInfo("")`/`Directory.GetParent("")` → `ArgumentException` aborts analysis with a cryptic error.
5. **[M]** `Infrastructure/EfModelAnalyzer.cs:254-276` + `Infrastructure/EntityFileDiscovery.cs:149-168` — `DbSet`s declared on a base context class are invisible (`GetMembers()` is non-inherited; syntax walk only covers the leaf class) → near-empty ERD with no warning.
6. **[M]** `Infrastructure/EntityFileDiscovery.cs:155,163` — `DbSet<Models.Blog>` stores the raw qualified text (never matches a class identifier) and `DbSet<Blog>?` (nullable) is skipped entirely → entity renders with zero properties.
7. **[M]** `Infrastructure/FluentApiConfigurationParser.cs:32,54` — expression-bodied `OnModelCreating` (`=> b.Entity<User>()...`) is silently ignored (`Body is null` returns without checking `ExpressionBody`).
8. **[M]** `Infrastructure/PropertyConfigParser.cs:19-64` — `OwnsOne`/`OwnsMany` builder lambdas aren't excluded (only `UsingEntity` is), so owned-type property config creates phantom top-level properties on the owner entity.
9. **[M]** `Infrastructure/FluentApiParsingUtilities.cs:56-60` + `PropertyConfigParser.cs:128` — unparseable arguments fall back to `args.Trim('"', ' ')` used verbatim as property names: `HasKey(nameof(User.Email))` fabricates a property literally named `nameof(User.Email)`. Validate the fallback is a plain identifier.
10. **[M]** `Infrastructure/FluentApiConfigurationParser.cs:69-77` — `IEntityTypeConfiguration<T>` via `ApplyConfiguration`/`ApplyConfigurationsFromAssembly` (the dominant pattern in real codebases) is entirely unsupported and silently produces diagrams without keys/constraints/relationships. At minimum, warn when detected.
11. **[L]** `Infrastructure/DefaultValueResolver.cs:112,129` — `ConstantValue?.ToString()` uses current culture (`0,5` on fr-FR); use `CultureInfo.InvariantCulture`.
12. **[L]** `Infrastructure/RelationshipAnalyzer.cs:353-385` — join-table cleanup removes *every* relationship between the joined pair (legitimate extra 1:N deleted); `fk.Name[..^2]` after case-insensitive `EndsWith("Id")` mis-strips `CategoryGuid` → `CategoryGu`.
13. **[L]** `Infrastructure/Constants/EfAnalysisConstants.cs:221` — `ToTablePattern` misses the schema overload `.ToTable("Users", "dbo")`; and `EfEntity.TableName` is never consumed anywhere — the whole capture is write-only dead weight. Render it or delete it.
14. **[L]** `Infrastructure/FluentApiConfigurationParser.cs:88-93` — dead truncation branch (can never match post-split); the split regex and `EntityMatchPattern` also disagree on whitespace handling, which can merge one entity's config into the previous section.
15. **[L]** `Infrastructure/DbContextIdentifier.cs:51-52` — name match doesn't verify `IsDbContext`/`IsModelSnapshot`: passing a POCO's name analyzes it as a context and yields an empty model instead of an error.
16. **[L]** `Infrastructure/FluentApiParsingUtilities.cs:171-191` — `IsInsideUsingEntityBlock` re-slices/regex-strips per match → O(n²) per section, and `MethodCallRegex().Matches` runs twice per section across parsers; noticeable on large scaffolded snapshots.
17. **[L]** `Infrastructure/EntityAnalyzer.cs:40` — static properties, indexers, and get-only computed properties are emitted as entity attributes (EF never maps them; `this[]` is invalid Mermaid).

### 4.4 MCP server (`ProjGraph.Mcp`) — 1H / 5M / 5L

1. **[H]** `ProjGraphTools.cs:46,288,296,305` + `WorkspaceRootService.cs:33,41,44` — all validation/guidance errors are BCL exceptions, but ModelContextProtocol 1.4.0 only propagates `McpException.Message` to clients; everything else becomes "An error occurred invoking 'get_erd'." The carefully-worded messages ("Multiple ModelSnapshots found… specify contextName") never reach any client, so clients can't self-correct. Contract tests invoke methods directly and bypass the SDK pipeline, so nothing catches this.
2. **[M]** `Program.cs:49` + §4.1 #7 — skipped/unparseable projects are reported only via `IOutputConsole`/`ILogger`, both null objects in the MCP host: `get_project_graph`/`get_project_stats` silently return partial graphs and wrong stats. Surface warnings in the tool result (as `get_class_diagram` already does) or via MCP logging notifications.
3. **[M]** `WorkspaceRootService.cs:37,94-128` — roots resolution only matches *files*: a relative directory path (advertised by the README/prompts, accepted by `get_class_diagram`) can never resolve. `Path.Combine(root, path)` is never tried first; raw input is used as a search pattern so `"../x"` throws unhandled and `"*.slnx"` resolves to an arbitrary match.
4. **[M]** `WorkspaceRootService.cs:49-60` + `Program.cs:30-39` — roots are fetched once on first use; no `notifications/roots/list_changed` handler, so workspace changes mid-session leave stale roots for the rest of the session.
5. **[M]** `ProjGraphTools.cs:63,96-97` — the >50-files warning is *prepended* before the diagram, but with `showTitle=true` (default) the diagram starts with YAML front-matter, which Mermaid only accepts at document start → the returned diagram fails to parse. Append the warning instead.
6. **[M]** `ProjGraphTools.cs:266` — `get_erd` on a file with multiple `DbContext` classes and no `contextName` silently analyzes the first; the snapshot branch of the same tool errors with a candidate list, and the CLI prompts. Mirror the snapshot behavior.
7. **[L]** `ProjGraphTools.cs:161-167` — no `topN`/`MaxDepth` validation (CLI validates `--top >= 1`); `topN=0` silently yields an empty hotspot list.
8. **[L]** `ProjGraphTools.cs:43-46` — absolute paths are accepted with no confinement to declared workspace roots; acceptable for a stdio-local dev tool, but since the server is on the MCP Registry, consider an opt-in restrict-to-roots mode.
9. **[L]** `IClassAnalysisService.cs:16,24` (+ EF equivalent) — no `CancellationToken` flows into class/EF analysis; client cancellation can't abort a long Roslyn pass (graph/stats already do this correctly).
10. **[L]** `DiagramResourceCache.cs:92-119` — `resourceCollection.Remove/Add` outside the lock (improbable but real phantom-resource race); `resources/updated` notification sent unconditionally rather than to subscribers; a transport exception there fails the tool call after the diagram was generated.
11. **[L]** `src/ProjGraph.Mcp/README.md:143` — documents option `depth` but the JSON schema property is `maxDepth`; clients following the doc get their option silently ignored (depth stays 1).

### 4.5 CLI (`ProjGraph.Cli`) — 0H / 2M / 2L

1. **[M]** `Commands/ErdCommand.cs:213-221` — the multi-file selection prompt shows file *names* and resolves via `files.First(f => GetFileName(f) == selected)`: two `AppDbContext.cs` in different projects render identical entries and the user's choice of the second is silently ignored (always picks the first). Prompt with paths or select by index.
2. **[M]** `Commands/StatsCommand.cs:88`, `Commands/VisualizeCommand.cs:166`, `Commands/ErdCommand.cs:202` — user paths interpolated into Spectre markup unescaped: a legal path containing `[`/`]` crashes with a baffling "Could not find color or style" error. The library layer escapes correctly; the commands don't.
3. **[L]** `Commands/ErdCommand.cs:167-172` — auto-discovery scans the entire CWD recursively with no `DirectoryFilters` (descends `bin`/`obj`/`.git`, offering generated duplicates) and eager `Directory.GetFiles` aborts on the first unreadable subdirectory.
4. **[L]** `src/ProjGraph.Cli/README.md:22-26` — first `visualize` example labeled "ASCII tree (default)" but the default format is `mermaid`; the documented `--format mermaid > graph.mmd` redirect produces a fenced document that is not a valid `.mmd` file (only `--output` yields raw Mermaid).

### 4.6 Build, packaging & release pipeline — 1H / 7M / 9L

1. **[H]** `.github/workflows/publish.yml:66,81` — `softprops/action-gh-release@v2`, `nick-fields/retry@v3` (and all actions) pinned by mutable tag in the one workflow holding `NUGET_API_KEY`, `id-token: write`, and `contents/packages: write`; a hijacked tag is a full package-supply-chain compromise. Pin to commit SHAs.
2. **[M]** `Directory.Build.props:16` — `NoWarn` includes NU1901–NU1904, suppressing *all* NuGet vulnerability-audit warnings (including high/critical) in every build. Drop NU1903/NU1904 or set `NuGetAuditLevel=high`.
3. **[M]** `publish.yml:3-6` — any `v*` tag on any commit triggers an irreversible NuGet publish with no check that the commit passed CI or is on `develop`; publish's own test leg is ubuntu-only. Add a protected environment or a tag-ancestry/CI-status gate.
4. **[M]** `publish.yml:58-71` — NuGet.org push happens before GitHub Packages/Release/MCP-registry with no rollback path; `prerelease: false` is hardcoded, so a `v1.0.0-rc.1` tag would ship marked as a full release.
5. **[M]** `ci.yml:28-32`, `publish.yml:40-44`, `codeql.yml:21-25`, `sonarqube.yml:38-39` — `setup-dotnet` without `global-json-file`, while `global.json` says `rollForward: latestMajor, allowPrerelease: true`: every build, including releases, floats on the runner's SDK of the week.
6. **[M]** `docs-publish.yml:3-7,15-17` — push trigger has no branch filter while concurrency group is static `"pages"` with `cancel-in-progress: true`: a `*.md` push on a feature branch can cancel an in-flight production Pages deployment.
7. **[M]** `.github/dependabot.yml` — no `github-actions` ecosystem, so action pins never receive updates or security bumps.
8. **[M]** `.runsettings:7` — coverlet filter `[ProjGraph.Cli]Program` can't match the namespaced `ProjGraph.Cli.Program` (silently no-ops; coverage deflated), and `ci.yml:44` never passes `--settings .runsettings`.
9. **[L]** `ci.yml` — no `concurrency` group (superseded PR pushes keep running); the uploaded coverage artifact has no consumer.
10. **[L]** `codeql.yml:7-12` — no `pull_request` trigger; findings appear only after merge.
11. **[L]** `sonarqube.yml:6-9,31-46` — no PR quality gate; static scanner cache key freezes `dotnet-sonarscanner` at its first-installed version forever.
12. **[L]** `publish.yml:77-78` — blind `sleep 300` for NuGet validation; poll the v3 registration endpoint instead.
13. **[L]** `ci.yml:17` — matrix covers ubuntu+windows but not macOS for a cross-platform path/Roslyn-heavy tool.
14. **[L]** `Directory.Build.props:19` + `src/ProjGraph.Mcp/.mcp/server.json:4,14` — committed version `0.4.0` is three releases behind `v1.0.2`; publish rewrites both via fragile single-line `sed`. Consider MinVer/NB.GV.
15. **[L]** `Directory.Packages.props:12-13` — `Microsoft.EntityFrameworkCore` 10.0.9 vs `.Design` 10.0.4 skew (samples only).
16. **[L]** `src/ProjGraph.Cli/ProjGraph.Cli.csproj` — no `<RollForward>` on the global tool; `projgraph` refuses to start for users with only a newer major runtime.
17. **[L]** BOMs in `ProjGraph.Cli.csproj`, `ProjGraph.Core.csproj`, `ProjGraph.Mcp.csproj`, `Tests.Contract.csproj`, `samples/Directory.Build.props` — the repo's own pre-commit hook rejects BOMs in these file types, so any future edit to them is blocked. Strip once.

### 4.7 Tests — 0H / 2M / 4L

1. **[M]** `tests/ProjGraph.Tests.Integration.Mcp/Helpers/McpTestHelper.cs:58` — the "integration" suite hand-wires `ProjGraphTools` in-process with `null!` for the required `McpServer` parameter: the real host, its DI, the stdio transport, and all server-dependent paths (progress, notifications, exception surfacing — see §4.4 #1) are never exercised. Add one test booting the host via the MCP client SDK over in-memory pipes.
2. **[M]** `tests/ProjGraph.Tests.Shared/Helpers/TestPathHelper.cs:17-31` — repo paths resolved from CWD with a hard-coded five-level `..` climb; where it fails, `SkipTestException` guards convert the miss into silent skips that would green-wash CI. Base on `AppContext.BaseDirectory` and fail loudly.
3. **[L]** `tests/ProjGraph.Tests.Shared/Helpers/SkipTestException.cs:30` — the `(message, innerException)` overload omits the `$XunitDynamicSkip$` token → produces a failure instead of a skip, opposite of the type's contract.
4. **[L]** `tests/ProjGraph.Tests.Integration.Mcp/McpResourcesTests.cs:241-248` — `Task.Delay(10)` + `BeAfter` races clock resolution/CI jitter.
5. **[L]** `tests/ProjGraph.Tests.Unit.ClassDiagram/AnalyzeFileUseCaseTests.cs:49-84` (also `AnalyzeDirectoryUseCaseTests.cs:95,134`) — delegation tests pin NSubstitute call shapes rather than observable output; refactors break tests without behavior change.
6. **[L]** `tests/ProjGraph.Tests.Integration.Mcp/McpRootsTests.cs:16,40` — fixed filename in the shared machine temp dir; behavior depends on leftovers from other runs/users.

**Coverage gaps aligned with the defects above** (each is untested today): malformed csproj flowing through `BuildGraphUseCase` (only a mocked `IOException` exists), duplicate solution entries, self-loops at the stats level, concurrent renderer use, external/unresolved-symbol paths end-to-end, arrays and `Dictionary` cardinality, same-named types in two namespaces, `IsRequired(false)` relationships, `OwnsOne` with builder lambda, base-context `DbSet`s, `IEntityTypeConfiguration`.

### 4.8 Documentation — 1H / 0M / 1L

1. **[H]** `CLAUDE.md:41-49` — the architecture section names a project `ProjGraph.Lib.ProjectGraph` that doesn't exist (actual: `ProjGraph.Lib.Dependencies`, correct everywhere else) and claims graphs are built "using Buildalyzer" — Buildalyzer appears nowhere in the repo (in-house parsers over `Microsoft.Build`). `TarjanSccAlgorithm` also lives in `Lib.Core`, not the feature library. This actively misleads contributors and AI agents.
2. **[L]** `CLAUDE.md:90` — states the ownership comment as `io.github.handys11/projgraph`; README and `server.json` (which the registry actually validates, and which agree with each other) use `io.github.HandyS11/projgraph`. CLAUDE.md's casing is the drift.

---

## 5. Suggested execution plan

| Phase | Content | Effort |
| --- | --- | --- |
| **1. Trust & crash fixes** | Shortlist items 1–7 (§3, Batch 1). All are small, local diffs; each High finding gets a regression test from the coverage-gap list. | ~1–2 days |
| **2. Product polish** | Shortlist items 8–12 (§3, Batch 2) plus the Medium findings in §4.1–4.5 that fall out naturally while touching those files. | ~2–3 days |
| **3. Strategic** | Replace EF regex layer with Roslyn syntax walking; `IEntityTypeConfiguration` support; unify path-normalization into one shared comparer; real end-to-end MCP transport test suite. | separate milestone |

Low-severity findings not covered by the phases are safe to batch opportunistically (good first issues); none are urgent.

---

*Generated by a five-track parallel audit (Fable 5). All High findings verified against source; line numbers valid as of commit `52c4632`.*
