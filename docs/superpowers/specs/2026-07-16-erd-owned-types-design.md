# ERD support for EF Core owned types (OwnsOne / OwnsMany)

Date: 2026-07-16
Status: Approved (design)
Program: EF rewrite

## Problem

Owned types are dropped entirely from generated ERDs. `Order.OwnsOne(o => o.ShipToAddress, ...)`
in eShopOnWeb configures five columns (Street, City, State, Country, ZipCode, with max-lengths and
required flags) that appear in neither the DbContext-path nor the snapshot-path ERD.

This is deliberate today. The Fluent API walkers in
`src/ProjGraph.Lib.EntityFramework/Infrastructure/` (`FluentSyntax.cs`, `FluentEntityWalker.cs`,
`FluentPropertyWalker.cs`) treat `OwnsOne`/`OwnsMany`/`UsingEntity` as a fence
(`FluentSyntax.NestedBuilderScopes`) so nested configuration does not leak onto the owner. The fence
works, but nothing then captures the owned type, so it is silently lost.

The goal is to capture the owned-type builder instead of merely skipping it, represent it in
`EfModel`, and render it.

## Decisions

Two representations were considered and one was rejected outright:

- **Pure inlining cannot satisfy the requirement.** `OwnsMany` in EF Core is always a separate table
  with its own key and an FK back to the owner. There are no columns to inline onto the owner, so
  inlining can never be the only rule.
- **A uniform weak entity misrepresents the flagship case.** `OwnsOne` without `ToTable` is
  table-splitting: the columns physically live in the owner's table as `ShipToAddress_Street` etc.
  Drawing a separate box implies a table that does not exist.

The resolution is **two modes, selectable, with the model recording facts and the renderer owning
presentation**:

- **MirrorEf (default)** — the physical view. An owned type that maps to the owner's table is
  inlined onto the owner using EF's own `{Nav}_{Property}` column naming. Owned types on their own
  table (`OwnsMany`, or `OwnsOne` + `ToTable`) get their own box plus an identifying relationship.
- **Classic** — the conceptual view. Every owned type is its own entity box linked to the owner by
  an identifying relationship. The owner never gets a navigation/reference column for it; the
  relationship line carries that information, as in a textbook ERD.

MirrorEf is the default: Mermaid's `erDiagram` is a database-shaped notation, the tool reads a
`DbContext`/snapshot, and the physical truth is the less surprising answer. Owned types are dropped
today, so every existing owned-type golden changes under either default — there is no
output-preserving default to protect.

## Architecture

### Model (`src/ProjGraph.Core/Models/EfModel.cs`)

`EfModel` records owned types faithfully and only once: always as a distinct entity, never
pre-inlined. Presentation is entirely the renderer's job.

`EfEntity` gains four fields:

| Field | Meaning |
| --- | --- |
| `IsOwned` | Marks an owned entity rather than a root one. |
| `OwnerEntity` | The owning entity's key. |
| `NavigationName` | e.g. `ShipToAddress`; source of both the column prefix and the relationship. |
| `IsCollection` | `OwnsMany` vs `OwnsOne`; selects `\|\|--o{` vs `\|\|--\|\|`. |

The existing `TableName` does the load-bearing work. The analyzer resolves each owned entity's
**effective** table, and the renderer decides to inline purely by comparing it to the owner's
effective table (`TableName` when set, else entity name).

This is what keeps the two paths honest: neither path makes a presentation decision, so they cannot
disagree about one. The DbContext path infers the table from the *absence* of `ToTable`; the
snapshot path reads the `ToTable` EF always emits; both converge on the same recorded fact.

Effective-table resolution rules:

- `OwnsOne` with explicit `ToTable("X")` → `X`.
- `OwnsOne` without `ToTable` → the owner's effective table (table-splitting).
- `OwnsMany` with explicit `ToTable("X")` → `X`.
- `OwnsMany` without `ToTable` → EF's default, `{OwnerTable}_{Nav}`. This value is only ever used
  for the owner-table equality check, and an owned collection never shares the owner's table, so it
  always renders as a box. The renderer does not display table names today.

**No `EfRelationship` is synthesized for owned types.** The renderer derives the identifying
relationship from the owned entities it chooses to draw as boxes. If the model held those
relationships, MirrorEf mode would have to filter out lines pointing at entities it had just
inlined — two places to keep in sync. Deriving is one.

**Entity keying.** Entities are keyed by name, but owned types collide: `Order.ShipToAddress` and
`Customer.Address` are distinct entity types in EF even though both are `Address`. The dictionary
key becomes `{Owner}.{Nav}` (EF itself uses `Order.ShipToAddress#Address`). Display naming is a
renderer concern (see Rendering).

### Capture: DbContext path

A new **`FluentOwnedTypeWalker`** runs after `FluentEntityWalker` in
`FluentApiConfigurationParser.ApplyFluentApiConstraints`. The existing `NestedBuilderScopes` fence
stays exactly as-is for the other walkers — that fence is what protects the current no-leak
behaviour, and it is not being weakened.

For each `OwnsOne`/`OwnsMany` invocation, found anywhere including inside `Entity<T>(e => ...)`
lambdas and `IEntityTypeConfiguration<T>.Configure` bodies:

1. **Resolve the owner** via the existing `FluentSyntax.ResolveOwningEntity` on the `OwnsOne` call's
   own receiver chain (chained form) or the ambient entity (lambda form).
2. **Resolve the owned CLR type and navigation name** from the lambda argument (`o => o.ShipToAddress`):
   type via the Roslyn semantic model where available, falling back to the owner entity symbol's
   property type; navigation name from the member access. For `OwnsMany`, unwrap the collection.
3. **Materialize** an `EfEntity` keyed `{Owner}.{Nav}` with `IsOwned`, `OwnerEntity`,
   `NavigationName` and `IsCollection` set, and its scalar properties discovered from the CLR type
   via the existing `EntityAnalyzer` (navigations excluded).
4. **Apply nested configuration** by re-running the property-walking logic *scoped to* the builder
   lambda body, or to the chained continuation (`OwnsOne(c => c.Address).Property(a => a.City)`),
   with the owned entity as the ambient target. `ToTable` inside sets the owned entity's
   `TableName`; its absence resolves per the rules above.
5. **Suppress the navigation property on the owner** — `Order` must not list a `ShipToAddress`
   column. `EntityAnalyzer` already excludes navigation-typed properties for related entities;
   verify owned navigations fall out the same way and fix if not.

Scoping requires making the walkers' config-root discovery accept a `SyntaxNode` scope rather than
only a `MethodDeclarationSyntax`, and making nested-scope exclusion *relative to the current scope
root* (exclude only fences nested strictly inside the scope being walked, not the `OwnsOne` fence
that opened it).

Nested ownership (`OwnsOne` inside an owned builder) recurses naturally: the owned entity's key
becomes the owner key for the inner call, and column prefixes compound (`Nav1_Nav2_Prop`).

Forms handled, all present in real code or existing fixtures:

- Builder-lambda: `OwnsOne(o => o.Nav, b => { ... })`
- Chained: `OwnsOne(o => o.Nav).Property(...)`
- Repeated chained calls targeting the same navigation across separate statements, which merge into
  one owned entity (as `ChainedOwnedContext` does).

### Capture: snapshot path

Snapshots spell ownership differently. Inside the owner's `b.Entity("Ns.Order", b => { ... })`
block, EF emits:

```csharp
b.OwnsOne("Ns.OrderAddress", "ShipToAddress", b1 =>
{
    b1.Property<string>("City").HasMaxLength(100);
    b1.WithOwner().HasForeignKey("OrderId");
    b1.ToTable("Orders");
});
```

Differences from the context path:

- Type and navigation come from **string literals**, not lambdas.
- The nested block **always** contains explicit `Property<T>` calls and an explicit `ToTable`, so no
  CLR-type reflection is needed; properties come from the block itself.
- `WithOwner`/`HasForeignKey` noise must not become columns, but the FK properties EF declares in
  the block (e.g. `OrderId`) are real columns and are kept, marked FK.

The same `FluentOwnedTypeWalker` handles this with a string-literal branch in step 2, and by letting
step 4's property walking do all property discovery when the block provides it — step 3's CLR-type
analysis is the context-path fallback. `ToTable` comparison against the owner's table decides
effective-table equality, producing the same recorded fact as the context path.

`ModelSnapshotParser.Parse` gains the walker call in the same position as the context path.

### Rendering

`DiagramOptions` gains `ErdOwnedMode` (enum: `MirrorEf` default, `Classic`), following the existing
`IncludePackages` precedent of feature-specific options living on the shared record.

`MermaidErdRenderer`:

- **MirrorEf** — for each owned entity whose effective table equals its owner's, fold its properties
  into the owner as `{Nav}_{Prop}`, preserving FK markers and constraint comments. The owned type's
  own PK (typically a shadow key) is not emitted; the owner's PK rules stand. Owned entities on a
  different table render as their own box plus a derived identifying relationship.
- **Classic** — every owned entity renders as a box, unprefixed, plus the identifying relationship.

Identifying relationship syntax: `||--||` for `OwnsOne`, `||--o{` for `OwnsMany`.

Display naming for owned boxes: the owned type's simple name (`Address`) when unambiguous among
rendered entities, falling back to `{Owner}_{Nav}` (`Order_ShipToAddress`) on collision.

### Surfaces

- CLI: `--owned-mode <mirror|classic>` on `ErdCommand`.
- MCP: optional `ownedMode` parameter on the ERD tool.

Both default to mirror.

### Error handling

An unresolvable owned type (missing symbol, cross-file type absent from the compilation) degrades to
a bare `EfEntity` named from the navigation — the same graceful fallback
`FluentSyntax.MaterializeEntity` already uses. Capture never throws; the worst case is an empty
owned box rather than dropped data.

## Testing

TDD throughout: a failing test precedes each behaviour.

Unit tests per walker behaviour (owner resolution, chained vs lambda form, nested ownership,
navigation suppression, snapshot string-literal form, FK retention, table resolution).

Golden coverage under `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/`:

- `ChainedOwnedContext` — golden updates. Its `OwnsOne(...).ToTable("ShopperAddresses")` means the
  owned type is a weak entity in *both* modes. This fixture stops asserting "owned type invisible"
  and starts pinning the weak-entity branch. The no-leak guarantee it was written for still holds:
  no `City` column may appear on `Shopper`.
- `OwnedAndJoinContext` — golden updates. `Address` inlines as `Address_City "max:50"` on `Customer`
  in mirror mode.
- New `OwnedModesContext` fixture exercising table-split `OwnsOne`, `OwnsOne` + `ToTable`, and
  `OwnsMany` together, with **two goldens** (mirror + classic). This is the only fixture rendered in
  both modes, so mode divergence is pinned once without doubling every golden.
- New snapshot fixture with an `OwnsOne` nested block, modelled on real `dotnet ef` output.
- **Cross-path agreement test**: one model expressed both as a context and as its snapshot,
  asserting the two rendered ERDs are identical. No such invariant exists today — the context and
  snapshot fixtures are unrelated models — so this closes the drift gap the two-path design
  otherwise leaves open.

Mermaid v11 validation (browser + `mermaid.parse`) on every changed and new golden, as in the
prior validation session.

## Out of scope

- `UsingEntity` join-entity capture. It shares the `NestedBuilderScopes` fence but is a separate
  concern and stays fenced-off as today.
- Rendering table names in the ERD.
- Owned-type support in the class-diagram library.
