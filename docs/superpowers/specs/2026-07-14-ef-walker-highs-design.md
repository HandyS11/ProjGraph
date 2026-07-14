# EF walker Highs — design

**Date:** 2026-07-14 · **Branch:** `revamp/ef-walker-highs` · **Base:** `develop` (`b3aa285`)
**Queue position:** Phase 2 item 1 (Highs jump the themed-Lows queue — see [audit report §6](2026-07-14-audit-report.md)).

## Scope

One PR fixing the two new-High correctness defects in the freshly-rewritten EF fluent-walker
layer, plus the three EF Mediums that live in the same files and coverage gaps. All five are
"the walker emits the wrong thing for an input shape the golden suite never exercised", so they
share fixtures and a review theme.

| # | Sev | Defect | File |
| --- | --- | --- | --- |
| 1 | H | Two-string `HasForeignKey("Ns.Dependent","FkId")` fabricates a phantom FK column | `FluentRelationshipWalker.cs` |
| 2 | H | Chained `.OwnsOne(...).Property(...)` leaks owned props onto the owner entity | `FluentPropertyWalker.cs` + `FluentEntityWalker.cs` |
| 3 | M | Join-table cleanup removes *all* rels between the pair, not just the M2M | `RelationshipAnalyzer.cs` |
| 4 | M | Primitive collections (`List<string> Tags`) vanish (classified as navigation) | `NavigationPropertyAnalyzer.cs` |
| 5 | M | `DbSet<Blog>?` / `DbSet<Models.Blog>` mis-extracted at file-discovery level | `EntityFileDiscovery.cs` |

**Out of scope (deferred to their own queued PRs):** extracting the duplicated walker
scaffolding (Sonar-gate PR, item 4) — this PR applies the #2 fix identically in both walkers
rather than de-duplicating, to keep the correctness change reviewable in isolation.

## Fixes

### 1. Two-string `HasForeignKey` overload (new-H #1)

`ForeignKeyPropertyNames` (`FluentRelationshipWalker.cs:323`) treats every string literal as an FK
property name. The EF snapshot generator's 1:1 form `HasForeignKey("Blogging.BlogHeader", "BlogHeaderId")`
passes the **dependent entity type name first**; `MarkForeignKeys` then fabricates a
`string Blogging.BlogHeader FK` column.

**Fix:** thread the known-entity names into `ForeignKeyPropertyNames`. When ≥2 string-literal
arguments are present, drop the first if it is an entity-type name — i.e. it contains a `.` or
its last dotted segment matches a known entity key. This preserves:
- the single-string form `HasForeignKey("BlogId")` (only one literal → never dropped),
- the composite-key form `HasForeignKey("Prop1", "Prop2")` (neither is an entity name → both kept),
- the lambda form (unaffected).

`ApplyForeignKey` already holds `entities`; pass `entities.Keys`. Reuse the existing `LastSegment`
helper (`:270`) for last-segment stripping.

### 2. Chained owned-type builder leak (new-H #2)

`ResolveOwningEntity` (identical body in `FluentPropertyWalker.cs:215` and `FluentEntityWalker.cs:215`)
walks the receiver chain and stops at the first `Entity` call — **without noticing** it stepped
*through* an `OwnsOne`/`OwnsMany`/`UsingEntity` hop. For
`modelBuilder.Entity<Customer>().OwnsOne(c => c.Address).Property(a => a.City).HasMaxLength(50)`
the `Property` config resolves to `Customer` and a phantom `string City` column lands on Customer.
The lexical `IsInsideNestedBuilderScope` guard only covers the *argument-list-lambda* form
(`OwnsOne(c => c.Address, a => a.Property(...))`), not the chained form.

**Fix:** in the receiver-walk loop, when a hop's member name is in `NestedBuilderScopes`, return
`null` (the config targets an owned/nested builder, not the owner entity) — before the fallback
`Ancestors()` scan. Applied identically in both walkers. This also covers the chained
`.OwnsOne(...).ToTable(...)` (entity walker) and `.OwnsMany(...).HasKey(...)` (property walker) forms.

`FluentRelationshipWalker` resolves endpoints from `Entity<T>()`/`HasOne`/`HasMany`, not via
receiver-chain owner resolution, so it is unaffected (verified against source).

### 3. Join-table over-removal (§5.3 Medium)

`RemoveDirectRelationshipsWithJoinTables` (`RelationshipAnalyzer.cs:372-374`) removes every
relationship between the join-table pair, matched by endpoint names only. A legitimate
`Group.Owner → User` OneToMany alongside a `User ⇄ Group` M2M is silently dropped.

**Fix:** the audit suggested filtering the removal to `ManyToMany`, but review showed that
`ConvertManyToManyToJoinTables` (run immediately before the cleanup) already removes every
many-to-many edge, so a `ManyToMany`-only cleanup is a provable no-op. The deeper fix deletes the
now-dead `RemoveDirectRelationshipsWithJoinTables` and its only-caller-of helper `IsJoinTable`
entirely: the join decomposition already removes the redundant edge, and nothing else should be
stripped. This also trims dead code the Sonar-gate PR would otherwise have to account for.

### 4. Primitive collections vanish (§5.3 Medium)

`IsNavigationProperty` (`NavigationPropertyAnalyzer.cs:49-53`) classifies any generic collection as
a navigation without checking element-type entity candidacy, so `List<string> Tags` is skipped as
a column *and* produces no relationship — the property disappears.

**Fix:** gate the collection branch on `IsEntityCandidate(elementType)` (already available in the
class; `TryGetCollectionElementType` already yields an `INamedTypeSymbol`). A non-entity element
type falls through to `return false` so the property is treated as a scalar column.

### 5. `DbSet` forms at file discovery (§5.3 Medium)

`ExtractEntityTypeNames` (`EntityFileDiscovery.cs:176-188`) requires a bare `GenericNameSyntax`:
`DbSet<Blog>?` (nullable wrapper) is skipped and `DbSet<Models.Blog>` is stored as unmatched
`"Models.Blog"`.

**Fix:** unwrap `NullableTypeSyntax` before the `GenericNameSyntax` match, and reduce the type
argument to its last identifier segment (`QualifiedNameSyntax` → `.Right`, generic/identifier →
`.Identifier.Text`). Mirrors the semantic path's `entityType.Name` normalization.

## Tests (TDD)

Red-first via targeted xUnit units asserting *correct* behavior, then goldens as regression lock-in.

**Unit (fail against current code, pass after fix):**
- `FluentRelationshipWalkerTests` — two-string `HasForeignKey("Ns.Dependent","FkId")` marks only
  `FkId` and creates no phantom entity-named property; composite `("A","B")` still marks both;
  single `("Fk")` unchanged.
- `FluentPropertyWalkerTests` — chained `Entity<Customer>().OwnsOne(c=>c.Address).Property(a=>a.City)`
  adds no `City` column to Customer (alongside the existing lambda-form test).
- `FluentEntityWalkerTests` — chained `.OwnsOne(...).ToTable(...)` does not set the owner's table.
- `RelationshipAnalyzerTests` (new class if absent) — join synthesis keeps a co-existing OneToMany
  between the joined pair; removes only the M2M.
- `NavigationPropertyAnalyzerTests` — `List<string>` is not a navigation; `List<Order>` still is.
- `EntityFileDiscoveryTests` — `DbSet<Blog>?` and `DbSet<Models.Blog>` both extract `Blog`.

**Goldens (new fixtures + regenerate via `UPDATE_EF_GOLDENS=1`, manually inspected for correctness):**
- `fixture-onetoone-snapshot` — ModelSnapshot 1:1 with the two-string `HasForeignKey`; assert no
  phantom column. (Snapshot `[Fact]` alongside `fixture-snapshot`.)
- `fixture-chained-owned` — chained `.OwnsOne(...).Property(...)` + a primitive collection column;
  assert clean owner entity and the primitive column present.

## Verification

`dotnet build` (warnings-as-errors) + `dotnet test` green; new goldens reviewed in `git diff`;
re-run without `UPDATE_EF_GOLDENS` to confirm they lock. Then Copilot review → address → CI green → merge.
