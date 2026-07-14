# EF Rewrite Slice 6 — ModelSnapshot via the fluent walkers

**Date:** 2026-07-14 · **Status:** implemented
**Author:** revamp effort (Fable 5)
**Parent program:** [2026-07-13-remaining-work-design.md](2026-07-13-remaining-work-design.md) — Phase 1, Slice 6 (final slice)

## Purpose

Route the ModelSnapshot path (`BuildModel(ModelBuilder)`) through the same Roslyn fluent
walkers the DbContext path already uses, and delete the now-dead regex parsing layer.
Snapshots are generated C# with the same fluent shape as `OnModelCreating` — string-based
`Entity("Ns.T", b => ...)`, `Property<int>("Id")`, `HasKey("Id")`,
`HasOne("Ns.Other", "Nav").WithMany("Navs").HasForeignKey("XId")` — all of which the
walkers already parse (Slices 1–3 deliberately covered the string-literal overloads).

This is a **parity** slice, not a gap-closing one: snapshot output must be preserved.
It also completes the Phase 1 exit criterion "all six regex files retired" and deletes the
deferred §4.3 Lows (#12, #13, #14, #16) wholesale with the code that hosts them.

## Current state

- **Snapshot call chain (the last regex consumer):** `EfModelAnalyzer.AnalyzeSnapshotAsync`
  → `ModelSnapshotParser.Parse` → `FluentApiConfigurationParser.ApplyConstraintsFromMethod`
  (regex split on `.Entity`) → `RelationshipConfigParser.Parse*` +
  `PropertyConfigParser.ParsePropertyConfigurations` + `ToTableRegex`.
- **Entity-name pre-scan is regex too:** `EfModelAnalyzer.ExtractEntityTypeNamesFromSnapshot`
  scans the `BuildModel` text with `EntityMatchRegex` to seed entity-file discovery.
- **The context path is already fully walker-based** (`ApplyFluentApiConstraints` →
  `FluentEntityWalker` / `FluentPropertyWalker` / `FluentRelationshipWalker` /
  `EntityConfigurationWalker`); a Slice-4 comment marks `ApplyConstraintsFromMethod` as
  "used only by the snapshot path now (retired in Slice 6)".
- **Coverage gap:** no golden fixture is a ModelSnapshot — the snapshot path is covered only
  by four inline-string tests in `EfAnalysisServiceSnapshotTests`. The golden harness
  (`EfGoldenRunner`) only exposes `RenderContext`.

### Verified walker parity for snapshot shapes (pre-implementation reconnaissance)

- `Entity("Ns.T", b => ...)` → `EntityNameFromInvocation` takes the first string literal,
  last dotted segment — same as the regex's namespace-strip.
- `b.Property<int>("Id")` → generic arg gives the type (predefined types resolve via
  `TypeSyntax.ToString()`), string literal gives the name; trailing chain calls dispatch
  through the same `PropertyConfigParser.ApplyConfiguration` the regex path used;
  unrecognized calls (`ValueGeneratedOnAdd`, `HasAnnotation`, `OnDelete`, `HasIndex`,
  `HasFilter`, …) are no-ops in both.
- `b.HasKey("A", "B")` → `KeyPropertyNames` collects string literals (no phantom `","`
  property — the behavior `AnalyzeSnapshotAsync_WithCompositeKey_…` pins).
- `b.HasOne("Ns.Blog", "Blog").WithMany("Posts").HasForeignKey("BlogId").OnDelete(…).IsRequired()`
  inside `Entity("Ns.Post", …)` → chain resolves source from the enclosing `Entity` call,
  target from the first string literal, FK marks `Post.BlogId`, explicit `IsRequired` read
  from the chain — semantics identical to the regex forward-scan, minus its match-window bugs.
- Owning-entity resolution is by receiver/ancestor syntax, so config never leaks across
  statements — where the regex section-scan *did* leak (e.g. nested `OwnsOne` property calls
  attributed to the owner), the golden pins whichever behavior the new fixture exhibits
  under the walkers, and any diff from the pre-swap golden must be reviewed as a §4.3 bug
  fix, not silently accepted.

## Decisions (locked)

| Decision | Choice | Rationale |
| --- | --- | --- |
| Seam | Keep `ModelSnapshotParser.Parse(snapshotClass, snapshotType, compilation)` and `AnalyzeSnapshotAsync` signatures; replace `Parse` internals with the three walkers in context-path order (entity → property → relationship) | Same strangler-fig seam discipline as Slices 1–5; `ModelSnapshotParser` survives as a thin, regex-free orchestrator exactly like `FluentApiConfigurationParser` did. |
| `EntityConfigurationWalker` on snapshots | Not called | Generated snapshots never contain `ApplyConfiguration*` calls. |
| Entity-name pre-scan | Replace `ExtractEntityTypeNamesFromSnapshot`'s regex with a syntax walk over **all** `Entity(...)` invocations of `BuildModel` (no nested-scope exclusion) | File discovery wants the broadest name set, matching the regex's match-anything behavior; scoping rules belong to the walkers, not discovery. |
| Regression net | New ModelSnapshot golden fixture + `EfGoldenRunner.RenderSnapshot`, with the golden generated **before** the swap (from the regex path) and asserted after | The four inline snapshot tests are too narrow to gate the swap alone; the golden pins ERD-level parity end-to-end. |
| `RelationshipConfigParser` | Delete the file; move `CreateShadowRelationship` (its only live member) into `FluentRelationshipWalker` as a private helper | Single remaining caller; "Has/With → EfRelationship" mapping is relationship-walker logic. |
| `PropertyConfigParser` | Delete the file; move `ApplyConfiguration` + the four private appliers into `FluentPropertyWalker` (private) | Single remaining caller after the section parser dies; keeps the config dispatch next to the only chain that feeds it. |
| `FluentApiParsingUtilities` | Delete the file; move `GetOrCreateProperty` + `IsValueTypeString` into `EfPropertyFactory` (internal) | Two walker consumers need a shared home; `EfPropertyFactory` already owns EfProperty construction concerns. |
| `EfAnalysisRegexPatterns` | Keep only `NumberInParensRegex` + `DecimalPrecisionRegex`; delete the other nine patterns and their `FluentApiPatterns` constants | The two survivors parse **SQL type strings** (`"nvarchar(30)"`, `decimal(18,2)` in attribute args) — string-value parsing, not C#-source parsing; regex is the right tool there. |
| "Retired" interpretation | `FluentApiConfigurationParser` and `ModelSnapshotParser` survive as regex-free orchestrators; the other four regex files are deleted or reduced to value-string parsing | The §4.3 fragility class lives in parsing C# **source** with regex; that is what exits the codebase. |

## Non-goals

- No new snapshot features (`HasIndex`, `HasAnnotation`, check constraints stay unparsed —
  no-ops in both old and new paths).
- No change to `RelationshipAnalyzer` (semantic pass) or `DefaultValueResolver`.
- No change to the CLI/MCP public surface or to `IEfModelAnalyzer`.
- No consolidation of the walkers' duplicated tiny syntax helpers beyond what the moves
  above force (full consolidation was deferred repeatedly; a dedicated cleanup can ride
  Phase 2 if wanted).

## Design

### Component 1 — Walker-based `ModelSnapshotParser.Parse`

```csharp
public static EfModel Parse(ClassDeclarationSyntax snapshotClass, INamedTypeSymbol snapshotType,
    Compilation compilation)
{
    var model = new EfModel { ContextName = ExtractContextName(snapshotType) };
    var entities = new Dictionary<string, EfEntity>();

    var buildModelMethod = /* unchanged lookup */;
    if (buildModelMethod is null || (buildModelMethod.Body is null && buildModelMethod.ExpressionBody is null))
    {
        return model;
    }

    FluentEntityWalker.Apply(buildModelMethod, entities, model, compilation);
    FluentPropertyWalker.Apply(buildModelMethod, entities, compilation);
    FluentRelationshipWalker.Apply(buildModelMethod, entities, model, compilation);

    return model;
}
```

The leftover-entity copy loop is dropped — `FluentEntityWalker.MaterializeEntity` adds each
entity to both the dictionary and `model.Entities` in document order, which matches the
regex split's section order. `RelationshipAnalyzer.AnalyzeRelationships` continues to run
afterwards in `AnalyzeSnapshotAsync`, unchanged.

### Component 2 — Syntax-based snapshot entity-name pre-scan

`EfModelAnalyzer.ExtractEntityTypeNamesFromSnapshot` walks the `BuildModel` syntax for every
invocation whose member name is `Entity` and collects the generic-argument or first-string-
literal name (last dotted segment) — the same name extraction the walkers use, but over
**all** descendants (discovery wants maximal recall). Implemented as a private helper in
`EfModelAnalyzer`; also accepts expression-bodied `BuildModel` (the old `Body == null` check
only supported block bodies).

### Component 3 — Dead-code deletion cascade

| File | Action |
| --- | --- |
| `FluentApiConfigurationParser.cs` | Delete `ApplyConstraintsFromMethod`, `ProcessEntityConfigSection`, `AddUniqueRelationships`, `ParseEntityConfiguration`; class keeps only the context-path orchestration. |
| `RelationshipConfigParser.cs` | Delete file; `CreateShadowRelationship` moves to `FluentRelationshipWalker` (private, doc updated). |
| `PropertyConfigParser.cs` | Delete file; `ApplyConfiguration` + `ApplyIsRequired/MaxLength/Precision/ColumnType` appliers move to `FluentPropertyWalker` (private). |
| `FluentApiParsingUtilities.cs` | Delete file; `GetOrCreateProperty` + `IsValueTypeString` move to `EfPropertyFactory` (internal). |
| `Patterns/EfAnalysisRegexPatterns.cs` | Keep `NumberInParensRegex` + `DecimalPrecisionRegex` only; header doc rewritten to "SQL type-string patterns". |
| `Constants/EfAnalysisConstants.cs` | In `FluentApiPatterns`, keep `NumericArgumentPattern` only. |

## Testing

- **Golden — new snapshot fixture, generated pre-swap.** `Golden/fixtures/SnapshotFixture.cs`
  (a realistic generated-style `ModelSnapshot`: two entities, namespace-qualified names,
  `Property<T>("…")` chains with `ValueGeneratedOnAdd`/`HasColumnType`/`HasMaxLength`/
  `IsRequired`/`HasDefaultValueSql`/`HasAnnotation`, composite key, one-to-many with
  `HasForeignKey` + `OnDelete` + `IsRequired`, `ToTable`) + `EfGoldenRunner.RenderSnapshot`
  + a snapshot case in `EfErdGoldenTests`. Golden is generated while the regex path is
  still live, then must stay byte-identical across the swap (any diff is reviewed and
  justified as a §4.3 bug fix before acceptance).
- **Golden — parity.** All nine existing context goldens stay byte-identical (context path
  untouched; the moves are mechanical).
- **Unit — walker snapshot shapes.** Add string-form tests to the walker suites where
  missing (composite string `HasKey`, string `HasOne/WithMany/HasForeignKey` chain,
  `Property<T>("name")` with unknown trailing calls as no-ops).
- **Unit — snapshot behavior.** `EfAnalysisServiceSnapshotTests` (4 tests) must pass
  unchanged — they are the black-box parity check.
- **Test pruning with the dead code:** delete `RelationshipConfigParserTests`; trim
  `EfAnalysisRegexPatternsTests` to the two surviving patterns; move the
  `GetOrCreateProperty`/`IsValueTypeString` tests from `FluentApiParsingUtilitiesTests`
  to an `EfPropertyFactory` suite and delete the rest (their behaviors are covered at
  walker level).
- **Suite + format:** full `ProjGraph.slnx` green; `dotnet format --verify-no-changes` clean.

## Risks & mitigations

- **Silent snapshot output regression** → the new snapshot golden is generated from the
  *old* path first and gates the swap; the four service-level snapshot tests double-lock.
- **Regex/walker divergence on quirky snapshots** (nested owned-type blocks, annotation
  noise) → divergences surface as golden diffs at swap time; each is either fixed for
  parity or accepted as a documented §4.3 bug fix — never silently.
- **Deleting a still-used member** → the deletion cascade was derived from a full-solution
  usage grep; `TreatWarningsAsErrors` + full build catches any miss.
- **Test-only regressions from moves** → moved members keep their exact implementations;
  only namespaces/hosts change.

## Exit criteria

- Snapshot analysis flows through the fluent walkers; no regex touches C# source anywhere
  in `Lib.EntityFramework`.
- `RelationshipConfigParser`, `PropertyConfigParser`, `FluentApiParsingUtilities` deleted;
  `EfAnalysisRegexPatterns` reduced to the two SQL type-string patterns.
- New snapshot golden committed (generated pre-swap, byte-identical post-swap or with a
  reviewed, documented diff); nine existing goldens byte-identical.
- `EfAnalysisServiceSnapshotTests` green unchanged; full suite + `dotnet format` clean.
- Phase 1 of the parent program is complete.
