# EF Rewrite Slice 4 — `IEntityTypeConfiguration<T>` support

**Date:** 2026-07-14 · **Status:** approved design, pre-implementation
**Author:** revamp effort (Opus 4.8)
**Parent program:** [2026-07-13-remaining-work-design.md](2026-07-13-remaining-work-design.md) — Phase 1, Slice 4

## Purpose

Close the first named EF feature gap: entities configured through
`IEntityTypeConfiguration<T>` classes are invisible to the analyzer today. Their fluent
configuration lives in a separate `Configure(EntityTypeBuilder<T> builder)` method that
`OnModelCreating` reaches via `modelBuilder.ApplyConfiguration(new XConfig())` or
`modelBuilder.ApplyConfigurationsFromAssembly(...)`. The context path never walks those
bodies, so their keys, property config, table mapping, and relationships are dropped.

Slice 4 makes the existing Roslyn walkers (`FluentEntityWalker`, `FluentPropertyWalker`,
`FluentRelationshipWalker`) fold a config class's `Configure` body into its target entity,
reusing the exact chain-folding built in Slices 1–3.

Unlike Slices 1–3 (byte-identical parity), this is a **gap-closing** slice: it deliberately
changes output. Exactly one existing golden changes (`fixture-config-class.mmd`); the other
six stay byte-identical.

## Current state

- **Walkers resolve the owning entity from the chain.** `ResolveOwningEntity` /
  `ResolveSourceEntity` walk the chain receiver (and enclosing lambda) looking for an
  `Entity<T>()` / `Entity("Ns.T")` call. A config class's chains are rooted at the
  `EntityTypeBuilder<T>` parameter (`builder.Property(...)`, `builder.HasMany(...)`), with
  **no `Entity<T>()` call** — so resolution returns `null` and nothing is applied.
- **The compilation excludes config files.** `EfModelAnalyzer.BuildSyntaxTreesAsync` pulls in
  the context file plus files whose name/content match **entity type names** extracted from
  `DbSet<T>` properties (`EntityFileDiscovery`). A config class is not an entity type, so a
  separate `Configurations/WidgetConfiguration.cs` is never added to the compilation and its
  syntax is unreachable.
- **Pre-staged fixture.** Slice 0 committed
  `tests/.../Golden/fixtures/ConfigClassContext.cs` (single-file: a `DbSet<Widget>` context
  whose `OnModelCreating` calls `ApplyConfiguration(new WidgetConfiguration())`, and a
  `WidgetConfiguration` doing `HasKey`, `Property(...).IsRequired().HasMaxLength(120)`). Its
  golden `fixture-config-class.mmd` currently reads `string Name "required"` — `Widget` and
  its `Id PK` / `Name "required"` come purely from DbSet discovery + conventions; the config
  class's `HasMaxLength(120)` is dropped. **The gap is exactly this missing `max:120`.**

## Decisions (locked)

| Decision | Choice | Rationale |
| --- | --- | --- |
| Walker reuse | Add an **ambient entity** to the three walkers; do not duplicate chain-folding | Spec goal: "walk each config class's `Configure` body with the same walker." |
| `ApplyConfigurationsFromAssembly` fidelity | Apply **every `IEntityTypeConfiguration<T>` in the compilation** when any `...FromAssembly` call is present; ignore the exact assembly argument | A single-project tool cannot reliably map a `typeof(X).Assembly` expression to a Roslyn assembly subset; false extras are unlikely in a real project. |
| Config-only entities (no `DbSet`) | **Materialize** `T` via `EntityAnalyzer` when its symbol is in-source | A configured type *is* a model entity in EF; consistent with how `FluentEntityWalker` materializes `Entity<T>()` types. |
| Config-file discovery | **Extend `EntityFileDiscovery`** to pull separate `IEntityTypeConfiguration<T>` files into the compilation | Config classes almost always live in their own files; without this the feature only works co-located with the context and the gap is not really closed. |

## Non-goals

- No `Configure`-body constructs beyond what Slices 1–3 already parse (relationships,
  property config, keys, `ToTable`, owned/join). Slice 4 only *routes* those bodies through
  the walkers; it adds no new fluent verbs.
- No snapshot-path change (Slice 6).
- No base-class `DbSet` discovery (Slice 5).
- No attempt to honor the *specific* assembly passed to `ApplyConfigurationsFromAssembly`.
- No change to the CLI/MCP public surface.

## Design

### Component 1 — Ambient entity in the three walkers

Add an optional trailing parameter `string? ambientEntity = null` to each walker's `Apply`,
threaded into its entity-resolution helper:

- `FluentEntityWalker.Apply(method, entities, model, compilation, string? ambientEntity = null)`
- `FluentPropertyWalker.Apply(method, entities, compilation, string? ambientEntity = null)`
- `FluentRelationshipWalker.Apply(method, entities, model, compilation, string? ambientEntity = null)`

Resolution changes: when the existing chain/ancestor scan finds **no** `Entity<T>()`, return
`ambientEntity` instead of `null`. The `OnModelCreating` path keeps passing the default
(`null`) → identical behavior, so the six parity goldens are untouched. The config-class path
passes `ambientEntity = T`.

For `FluentEntityWalker` specifically (materialize + `ToTable`): a config-class body has no
`Entity<T>()` root, so its `FindConfigRoots("Entity")` scan yields nothing and materialization
never fires from the body. `T` is materialized by the orchestrator instead (Component 2). The
walker still processes `builder.ToTable(...)` roots, which resolve to `ambientEntity`.

**Why ambient is safe.** A config class configures exactly one entity `T`; every top-level
chain in its `Configure` body belongs to `T`. Nested owned/join lambdas are already excluded
by the Slice-3 `NestedBuilderScopes` / `IsInsideUsingEntity` guards, so ambient never leaks
into a nested builder scope.

### Component 2 — `EntityConfigurationWalker` (new orchestrator)

New `internal static class EntityConfigurationWalker` in `Infrastructure/`, invoked from
`FluentApiConfigurationParser.ApplyFluentApiConstraints` **after** the three `OnModelCreating`
walkers run:

```
FluentEntityWalker.Apply(methodSyntax, entities, model, compilation);
FluentPropertyWalker.Apply(methodSyntax, entities, compilation);
FluentRelationshipWalker.Apply(methodSyntax, entities, model, compilation);
EntityConfigurationWalker.Apply(methodSyntax, contextType, entities, model, compilation);
```

`Apply`:
1. **Collect config-class symbols** by scanning `OnModelCreating` invocations:
   - `ApplyConfiguration(new XConfig())` → resolve the argument's type symbol
     (`SemanticModel.GetTypeInfo` on the `ObjectCreationExpressionSyntax`).
   - Any `ApplyConfigurationsFromAssembly(...)` present → enumerate all
     `INamedTypeSymbol` in the compilation implementing
     `IEntityTypeConfiguration<>` (constructed-from check on the interface name/arity;
     degrade to interface simple-name when the EF reference is absent).
   - Dedup the resulting symbol set.
2. **For each config-class symbol:**
   - Resolve `T` from its `IEntityTypeConfiguration<T>` interface argument.
   - Find the `Configure` method's `MethodDeclarationSyntax` via
     `DeclaringSyntaxReferences`. If none (external/compiled type), skip — graceful degrade.
   - **Materialize** the entity for `T` if absent from `entities`/`model` (via
     `EntityAnalyzer.AnalyzeEntity(TSymbol)`), matching the `FluentEntityWalker` materialize
     path.
   - Run the three walkers on the `Configure` body with `ambientEntity = T.Name`.

Relationship dedup/FK marking reuse the walkers' existing `GenerateKey`/`existingKeys`
logic, so a relationship declared in both `OnModelCreating` and a config class collapses.

### Component 3 — Config-file discovery

Extend `EntityFileDiscovery` so config-class files reach the compilation:

- After entity/base-class discovery in `BuildSyntaxTreesAsync`, add a pass that finds `.cs`
  files under the existing search directories whose content contains
  `IEntityTypeConfiguration` and adds them to the syntax-tree set (same
  `EnumerateFiles` + content-guard style as the existing entity/base-class discovery, so it
  respects the current directory-walk bounds and dedup).
- This reuses the established discovery plumbing rather than adding a new traversal.

### Data flow

```
OnModelCreating syntax
  ├─ (existing) 3 walkers, ambientEntity=null ── DbSet + inline fluent config
  └─ EntityConfigurationWalker.Apply
        ├─ ApplyConfiguration(new XConfig())        ─┐
        ├─ ApplyConfigurationsFromAssembly(...)      ─┤─ config-class symbols (deduped)
        └─ per config class:
              resolve T ─ materialize T if absent ─ 3 walkers on Configure body, ambientEntity=T
Compilation now includes separate *Configuration.cs files (Component 3)
```

### Error handling / degradation

- Config type with no in-source syntax (compiled/external) → skipped, no throw.
- `IEntityTypeConfiguration<T>` not resolvable (missing EF reference) → interface matched by
  simple name + arity; `T` matched by generic-argument syntax. Same syntax-only degradation
  the walkers already use.
- No config classes / no `ApplyConfiguration*` calls → orchestrator is a no-op; output
  identical to today.

## Testing

- **Golden — deliberate change.** `fixture-config-class.mmd`: `string Name "required"` →
  `string Name "required, max:120"` (regenerated via `UPDATE_EF_GOLDENS=1`, diff reviewed).
- **Golden — new multi-file fixture.** Add a fixture context exercising
  `ApplyConfigurationsFromAssembly(...)`, a config class in a **separate file**, a config
  class that configures a **relationship** (`HasMany/WithOne`), and a **config-only entity**
  with no `DbSet` (to cover materialization). Add its golden. This proves Components 2 + 3
  end-to-end through the existing `EfErdGoldenTests` harness.
- **Golden — parity.** The other six goldens
  (`simple-context`, `complex-ecommerce`, `fixture-relationships`, `fixture-owned-join`,
  `fixture-property-config`, `fixture-base-dbset`) stay byte-identical.
- **Unit — `EntityConfigurationWalkerTests`:** `ApplyConfiguration(new X())` resolves & folds
  config; `ApplyConfigurationsFromAssembly` picks up all implementers; ambient entity routes
  property/key/relationship/`ToTable` to `T`; config-only entity materialized; external/no-syntax
  config skipped; no-config no-op.
- **Suite + format:** full `ProjGraph.slnx` green (snapshot path untouched);
  `dotnet format --verify-no-changes` clean.

## Risks & mitigations

- **Over-broad `FromAssembly` discovery** picks up an unrelated config class → bounded by the
  compilation's discovered files (Component 3 only adds files under the context's search
  directories); acceptable per the locked decision.
- **Ambient leaking into a nested scope** → prevented by the existing Slice-3 nested-scope
  guards; covered by a walker test asserting a `Configure` body with an owned type still only
  materializes `T`.
- **Silent output regression on the six parity goldens** → gated byte-for-byte; ambient
  defaults to `null` on the `OnModelCreating` path.

## Exit criteria

- `IEntityTypeConfiguration<T>` via both `ApplyConfiguration(new X())` and
  `ApplyConfigurationsFromAssembly(...)` is folded into the model, including separate-file
  config classes and config-only entities.
- `fixture-config-class.mmd` updated (only `max:120` added); six other goldens byte-identical.
- New multi-file fixture golden + `EntityConfigurationWalkerTests` green; full suite +
  `dotnet format` clean.
- No public CLI/MCP surface change; snapshot path unchanged.
