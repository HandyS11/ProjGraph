# EF Rewrite Slice 5 — base-class `DbSet`s

**Date:** 2026-07-14 · **Status:** approved design, pre-implementation
**Author:** revamp effort (Opus 4.8)
**Parent program:** [2026-07-13-remaining-work-design.md](2026-07-13-remaining-work-design.md) — Phase 1, Slice 5

## Purpose

Close the second named EF feature gap: `DbSet<T>` properties declared on a **base**
`DbContext` are invisible to the analyzer. Entity discovery enumerates only the entry
context's own members, so a base class such as

```csharp
public abstract class BaseDbContext : DbContext
{
    public DbSet<Note> Notes { get; set; } = null!;
}
public class BaseContext : BaseDbContext
{
    public DbSet<Tag> Tags { get; set; } = null!;
}
```

surfaces `Tag` but drops `Note`. This is a common shape (a shared/abstract base context in
a library, tenant/audit base contexts, generic `DbContext<T>` bases).

Like Slice 4, this is a **gap-closing** slice: it deliberately changes output. Exactly one
existing golden changes (`fixture-base-dbset.mmd` gains `Note`); the other seven stay
byte-identical.

## Current state

- **`EfModelAnalyzer.DiscoverEntitiesFromDbSets(contextType)`** iterates
  `contextType.GetMembers().OfType<IPropertySymbol>()` — **only the entry type's own
  members**. `DbSet<T>` inherited from a base context is never seen.
- **Entity property discovery already walks base types.** `EntityAnalyzer.AnalyzeEntity`
  climbs `BaseType` to collect inherited properties/keys. The context side is the only place
  that does not climb.
- **Base-context files are already pulled into the compilation.**
  `EfModelAnalyzer.BuildSyntaxTreesAsync` runs `ExtractBaseClassNamesFromSyntax` over the
  context file and `SearchForBaseClassFiles` to add sibling base-class files (this is how a
  separate `BaseDbContext.cs` reaches the compilation). So `contextType.BaseType` resolves to
  a real symbol with its `DbSet<T>` members — the semantic information is present; only the
  discovery loop ignores it.
- **Entity type names for file discovery come only from the entry context.**
  `EntityFileDiscovery.ExtractEntityTypeNames(contextClass)` scans a single class's members.
  A base-declared entity that lives in **its own file** (referenced by no entry-context
  `DbSet`) is therefore never added to the compilation, so it would resolve to an error type
  with no columns.
- **Pre-staged fixture.** Slice 0 committed `Golden/fixtures/BaseContext.cs` (the snippet
  above, single file). Its golden `fixture-base-dbset.mmd` currently renders **only `Tag`** —
  `Note` is missing. **The gap is exactly this missing entity.**

## Decisions (locked)

| Decision | Choice | Rationale |
| --- | --- | --- |
| Where to walk | Climb `contextType.BaseType` in `DiscoverEntitiesFromDbSets`, mirroring `EntityAnalyzer.AnalyzeEntity` | The gap is a missing loop, not a missing seam; keep the fix in the one method that owns DbSet discovery. |
| Stop condition | Stop at `SpecialType.System_Object` (same guard `AnalyzeEntity` uses); the EF `DbContext` base contributes no `DbSet<T>` and is harmless to scan | Matches existing convention; no special-casing of the `DbContext` symbol needed. |
| Dedup precedence | First-wins walking derived → base (keep existing `entities.ContainsKey` guard) | A derived override of the same `DbSet<T>` should win over the base; consistent with C# member hiding. |
| Base entity syntax across files | Extend `BuildSyntaxTreesAsync` to also collect DbSet entity-type names from the discovered base-context files and feed them into entity-file discovery | Fulfils the spec's "…and their entity syntax": a base-declared entity in its own file must still materialize with columns. |
| No `IsDbContext` change | Leave `DbContextIdentifier.IsDbContext` as-is (base name must contain "DbContext") | Out of scope; the entry context is chosen by the caller and already resolves. |

## Non-goals

- No base-class `OnModelCreating` merging (fluent config declared on a base context body).
  Slice 5 is DbSet **discovery** only; fluent-config inheritance is not a named gap.
- No snapshot-path change (Slice 6).
- No change to the CLI/MCP public surface.
- No change to entity **property** discovery (`AnalyzeEntity` already walks base types).

## Design

### Component 1 — Walk the context `BaseType` chain (core)

Rework `DiscoverEntitiesFromDbSets(INamedTypeSymbol contextType)` to climb the base chain,
mirroring `EntityAnalyzer.AnalyzeEntity`:

```csharp
var entities = new Dictionary<string, EfEntity>();
for (var current = contextType;
     current is not null && current.SpecialType is not SpecialType.System_Object;
     current = current.BaseType)
{
    foreach (var member in current.GetMembers().OfType<IPropertySymbol>())
    {
        // existing DbSet<T> match + AnalyzeEntity, guarded by !entities.ContainsKey
    }
}
```

Because the walk is derived → base and the existing `!entities.ContainsKey(entityType.Name)`
guard is kept, a derived context that re-declares a `DbSet<T>` wins over the base's.

### Component 2 — Base-declared entity syntax across files

In `BuildSyntaxTreesAsync`, after the base-context files are discovered
(`SearchForBaseClassFiles`), also extract DbSet entity-type names from those base-context
class declarations and run the existing entity-file discovery for them. This ensures a
base-declared entity that lives in **its own file** reaches the compilation and materializes
with columns, rather than resolving to a bare error type.

Co-located entities (declared in the same file as their base context) already come along for
free when the base-context file is added, so this only affects the fully-separated layout.

### Data flow

```
entry context syntax
  ├─ ExtractEntityTypeNames(entry context)          ─┐
  ├─ base-context files via SearchForBaseClassFiles  ─┤─ entity files → compilation
  └─ ExtractEntityTypeNames(each base context)  ◄─────┘  (Component 2)
BuildEfModel
  └─ DiscoverEntitiesFromDbSets: entry ∪ base-chain DbSet<T>   (Component 1)
```

### Error handling / degradation

- Base type absent from source (external/compiled base context) → its `DbSet<T>` members are
  still enumerable from metadata; entity types that are metadata-only degrade the same way any
  unresolved entity does today (name only). No throw.
- Context with no base beyond `DbContext`/`object` → loop runs once over the entry type;
  output identical to today (the seven parity goldens).

## Testing

- **Golden — deliberate change.** `fixture-base-dbset.mmd`: now renders both `Note` (from the
  base `BaseDbContext`) and `Tag`, regenerated via `UPDATE_EF_GOLDENS=1`, diff reviewed.
- **Golden — new multi-file fixture.** Add a fixture whose entry context derives from a base
  `DbContext` in a **separate file**, each declaring a `DbSet<T>`, proving Components 1 + 2
  end-to-end through `EfErdGoldenTests`.
- **Golden — parity.** The other seven goldens stay byte-identical.
- **Unit — `EfModelAnalyzer` / `EfAnalysisService`:** base-declared `DbSet<T>` discovered;
  multi-level base chain (grandparent) discovered; derived override wins over base; a context
  with no user base is unchanged; base entity in a separate file materializes with columns.
- **Suite + format:** full `ProjGraph.slnx` green (snapshot path untouched);
  `dotnet format --verify-no-changes` clean.

## Risks & mitigations

- **Silent output regression on the seven parity goldens** → gated byte-for-byte; the walk
  reduces to the current single-pass behavior when there is no user base context.
- **Scanning the EF `DbContext` base symbol** → it exposes no `DbSet<T>` properties, so the
  extra iterations add nothing to the model; the `System_Object` stop bounds the walk.
- **Duplicate entity from base + derived re-declaration** → `entities.ContainsKey` guard plus
  `DeduplicateModelContent` keep a single entity; covered by the override-precedence test.

## Exit criteria

- Base-class `DbSet<T>` (single- and multi-level) is folded into the model, including base
  contexts and base-declared entities that live in separate files.
- `fixture-base-dbset.mmd` updated (adds `Note`); the seven other goldens byte-identical.
- New multi-file fixture golden + unit tests green; full suite + `dotnet format` clean.
- No public CLI/MCP surface change; snapshot path unchanged.
