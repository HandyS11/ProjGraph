# Class-diagram Lows — design

**Date:** 2026-07-17
**Branch:** `revamp/classdiagram-lows`
**Source:** audit report `2026-07-14-audit-report.md` — queue item 5: §4.2 #10/#11/#13/#14 (deferred Lows) + the two §5.2 class-diagram Mediums. The four remaining §5.2 class-diagram Lows are the same theme and are included, closing out the class-diagram section of the audit entirely.

## Goal

Fix the ten remaining class-diagram findings. All are small, independent, behavior-narrow fixes in `ProjGraph.Lib.ClassDiagram`. No new features, no API changes, no CLI surface changes.

## Findings and fixes

### F1 — Generic outer types discarded (§4.2 #10, L)

`RelationshipAnalyzer.ExtractTypesFromGeneric` extracts only type arguments from any generic type: `Result<Order> Prop` yields an edge to `Order` but the user-defined `Result<T>` container vanishes.

**Fix:** when the generic type itself is not a system type, also add its `OriginalDefinition` to the extracted list (before recursing into arguments). System generics (`List<T>`, `Dictionary<K,V>`) keep the current args-only behavior — we still don't want BCL container nodes.

### F2 — Collection detection by name substring (§4.2 #11, L)

`ProcessMemberType` classifies a member as a collection ("\*" cardinality) when the generic type's *name* contains `List`/`Collection`/`IEnumerable`/`Array`/`Set` — so `Settings<T>` is a "collection" and a custom `Portfolio<T>` is not.

**Fix:** semantic check — a resolved named type is a collection iff it is, or implements, `System.Collections.IEnumerable` (`SpecialType.System_Collections_IEnumerable`, via self-check + `AllInterfaces`). `string` never reaches this path (excluded by the `SpecialType.None` guard). For unresolved (`TypeKind.Error`) symbols, where no semantic info exists, keep the existing name heuristic as a best-effort fallback.

### F3 — Dedupe keys use simple names (§4.2 #13, L)

`AddDependencyRelationships` dedupes on `extracted.Name` (simple name): with `A.Order` and `B.Order` referenced from the same type, the second edge is silently dropped.

**Fix:** key both `seenCombinations` and `seenMethodTypes` on `TypeAnalyzer.GetFullyQualifiedName(extracted)` instead of `Name`.

### F4 — `Sanitize` merges distinct type IDs (§4.2 #14, L)

`MermaidClassDiagramRenderer.Sanitize` maps `.` and `_`-adjacent characters all to `_`, so `Ns.Foo_Bar` and `Ns.Foo.Bar` produce the same Mermaid node ID and the two types merge into one node.

**Fix:** collision-aware ID assignment per render. Build a `FullName → ID` map from `model.Types` in order: first full name to claim a sanitized ID keeps it; subsequent distinct full names that collide get a deterministic `_2`, `_3`, … suffix. `RenderRelationship` resolves endpoints through the map (fallback to plain `Sanitize` for endpoints not present in `Types`, which should not occur). Output changes **only** when a real collision exists.

### F5 — Bare relative filename aborts analysis (§5.2 M)

`AnalyzeFileUseCase` line 38: `GetDirectoryName("Foo.cs")` returns `""` (the `?? Environment.CurrentDirectory` guards only `null`), and `WorkspaceRootResolver` then throws `ArgumentException` on `new DirectoryInfo("")`, aborting `projgraph class Foo.cs -i` whenever any reference is unresolved.

**Fix:** `fileSystem.GetDirectoryName(fileSystem.GetFullPath(filePath))`, falling back to `Environment.CurrentDirectory` when null **or empty**.

### F6 — Method-dependency loop skips arrays (§5.2 M)

The array unwrap added to `ProcessMemberType` (properties/fields) was not mirrored in the method return/parameter loop: `Order[] GetAll()` / `void Save(Order[] batch)` produce no edge while `List<Order>` does. Residual of §4.2 #8.

**Fix:** extract the innermost-element unwrap into a shared helper and apply it before the `INamedTypeSymbol` guard in the method loop.

### F7 — Unguarded directory enumeration (§5.2 L)

`WorkspaceTypeDiscovery.CollectTypeMatchesAsync` wraps per-file reads but not the `EnumerateFiles`/`EnumerateDirectories` iteration itself; a mid-scan `IOException` (deleted directory, symlink cycle) aborts the whole analysis, contradicting the degrade-never-throw intent (`DiscoverCsFilesUseCase` already catches these).

**Fix:** materialize each enumeration inside `try/catch (IOException or UnauthorizedAccessException)`; on failure skip that directory level and continue.

### F8 — External-type nodes drop generic arity (§5.2 L)

`SymbolResolver.AddExternalType` uses bare `symbol.Name`: an external `AbstractValidator<T>` renders as `AbstractValidator` while in-source generics render `Bar~T~` (via `TypeAnalyzer.ShortNameFormat`).

**Fix:** use the same short display format as `TypeAnalyzer.AnalyzeType` for the node name so arity renders consistently.

### F9 — Trailing separator drops the diagram title (§5.2 L)

`AnalyzeDirectoryUseCase`: `projgraph class ./src/` → `GetFullPath` preserves the trailing separator → `Path.GetFileName` returns `""` → the diagram title is silently omitted.

**Fix:** `Path.TrimEndingDirectorySeparator(fullPath)` before `GetFileName` (compute the title once, used at both return sites).

### F10 — Optimistic common-dir pass: double IO + tie-break override (§5.2 L)

`WorkspaceTypeDiscovery.FindTypeDefinitionFileAsync` scans `Models/`, `Entities/`, … first and returns the first hit — overriding the deterministic path-sort tie-break of the root scan — and on a miss the root pass re-scans those same directories (double IO).

**Fix:** delete the optimistic pass; do the single deterministic root scan. Lookups are memoized per type name (`TypeFileLookupCache`), negative lookups already always paid the full scan, and the string-match pre-filter keeps the scan cheap. This makes resolution order-deterministic in all cases.

## Approaches considered

- **F4:** (a) injective character encoding (e.g. escape `_` first) — changes existing IDs for every name containing `_`, churning goldens/tests for no user benefit; (b) Mermaid backtick-quoted IDs — large output churn, v11-specific quirks; **(c) collision-suffix map — chosen:** zero output change unless a real collision exists, mirrors the dependency renderer's existing collision handling.
- **F2:** (a) exact-name allowlist of BCL collections — misses user collections and third-party ones; **(b) semantic `IEnumerable` check with error-symbol name fallback — chosen:** correct for everything resolvable, degrades exactly like today otherwise.
- **F10:** (a) keep the optimistic pass but skip visited dirs in the root pass — still overrides the global tie-break, more code; **(b) delete the pass — chosen:** simpler and fully deterministic; the memoization cache bounds the cost.

## Testing

TDD per finding in `tests/ProjGraph.Tests.Unit.ClassDiagram` (existing test classes per component):

- F1: property `Result<Order>` (user generic) → edges to both `Result~T~` and `Order`; `List<Order>` still yields only `Order`.
- F2: resolved `Settings<T>` (non-collection generic) → cardinality "1"; custom `IEnumerable`-implementing generic → "\*"; unresolved `MyCollection<T>` keeps "\*" via fallback.
- F3: two same-named types in different namespaces referenced from one type → two edges.
- F4: model with `Ns.Foo_Bar` and `Ns.Foo.Bar` types → two distinct class IDs, relationships target the right ones; non-colliding model output unchanged.
- F5: `ExecuteAsync("Foo.cs")` (bare relative name, existing file) with an unresolved reference → no throw, StartDirectory = current directory.
- F6: `Order[] GetAll()` and `void Save(Order[] batch)` → Dependency edges to `Order`.
- F7: `IFileSystem` stub whose enumeration throws `IOException` mid-scan → discovery returns gracefully (skips), doesn't throw.
- F8: external generic symbol → node name carries type parameters.
- F9: directory path with trailing separator → title equals directory name.
- F10: type present in both `Models/X.cs` and `Zebra/X.cs` → deterministic path-sorted pick; existing discovery tests still pass.

Integration suites (`Tests.Integration.Cli`) guard the CLI surface; EF goldens are untouched (class diagrams have no goldens). Full suite + `dotnet format` verification before PR.

## Out of scope

§4.2 #9 (include-flags default false — a UX/design decision, not a Low), §4.2 #1/#2 residuals (partial Highs with accepted residuals), CancellationToken plumbing (next themed PR), and all non-class-diagram §5.x findings.
