# ProjGraph Remaining-Work Design

**Date:** 2026-07-13 · **Status:** approved design, pre-implementation
**Author:** revamp effort (Opus 4.8)

## Purpose

Define the remaining work for the ProjGraph revamp as a single, sequenced program.
The audit-driven High and Medium findings are done or in review (PRs #119–#142); what
remains is: (0) a fresh audit of the current `develop` branch, (1) the strategic
replacement of the EF Fluent-API regex layer with Roslyn semantic analysis while
closing known feature gaps, and (2) the remaining and newly-surfaced Low findings.

Delivery is many small, independently reviewed PRs — the established cadence (Copilot
review → address comments → CI green → merge).

## Decisions (locked)

| Decision | Choice |
| --- | --- |
| Plan scope | Everything, re-audited first |
| EF rewrite goal | Correctness (kill the regex bug class) **and** close known gaps |
| EF rewrite strategy | Strangler-fig, incremental, behind the existing `IEfModelAnalyzer` seam |
| Re-audit depth | Full multi-agent, like the original |

## Non-goals

- No full re-architecture of the EF analyzer; entity/property discovery stays as-is
  (already Roslyn-based).
- No new product features beyond the three named EF gaps.
- No change to the CLI/MCP public surface except where a finding requires it.
- No reintroduction of the removed `REVAMP_REPORT.md`; Phase 0 produces a fresh report
  under `docs/superpowers/specs/`.

---

## Phase 0 — Fresh full re-audit

**Why:** 24 PRs have merged since the original audit; its report was removed (PR #131)
and is now stale. A fresh pass confirms no regressions, catches anything the merged work
introduced, and produces the current findings list that Phase 2 consumes.

**Approach:** five parallel read-only agents, each scoped to one area and returning ranked
findings as `- [Severity] path:line — defect; failure scenario (input → wrong output); one-line fix`:

1. Dependency graph (`Lib.Core`, `Lib.Dependencies`)
2. Class diagram (`Lib.ClassDiagram`)
3. EF / ERD (`Lib.EntityFramework`) — doubles as rewrite-input reconnaissance
4. CLI + MCP entry points (`Cli`, `Mcp`, `Lib`)
5. Tests, CI/CD, packaging, docs

Constraints: read-only, no build/test (a central build may run concurrently). Every
**High** finding is verified against source before inclusion.

**Output:**
- `docs/superpowers/specs/YYYY-MM-DD-audit-report.md` — the new report.
- A triaged findings table: `already-fixed | new-High | new-Medium | Low`, superseding the
  original report's line-items.

**Exit criteria:** report committed; new Highs (if any) queued ahead of Phase 1; EF-area
findings folded into the Phase 1 slice they belong to.

---

## Phase 1 — EF Fluent-API → Roslyn (strangler-fig)

### Current state

The EF analyzer already uses Roslyn for **entity and property discovery**
(`EntityAnalyzer`, `NavigationPropertyAnalyzer`, `TypeSymbolExtensions`). The
**regex-over-text** layer is confined to:

- `FluentApiConfigurationParser` (194 LOC) — orchestrates parsing of the
  `OnModelCreating` method **text**.
- `RelationshipConfigParser` (410) — `HasOne/HasMany/WithOne/WithMany/HasForeignKey/IsRequired`.
- `PropertyConfigParser` (228) — `Property/HasKey/HasMaxLength/...`.
- `FluentApiParsingUtilities` (192) — regex helpers (paren-balance block detection, etc.).
- `Patterns/EfAnalysisRegexPatterns` (155) + parts of `Constants/EfAnalysisConstants`.
- `ModelSnapshotParser` (70) — snapshot file parsing.

≈ 1,900 LOC is the replacement target. The whole `§4.3` fragility class (unbounded
match-window scans, paren-balance heuristics, `nameof(...)` fabrication, phantom
properties) is a *structural* consequence of parsing text instead of the syntax tree.

### The seam

`FluentApiConfigurationParser.ApplyFluentApiConstraints(INamedTypeSymbol contextType,
Dictionary<string,EfEntity> entities, EfModel model, Compilation compilation)` already
receives the `Compilation`. We keep this signature (and `IEfModelAnalyzer` above it) and
replace the **internals** with a Roslyn fluent-chain walker that reads the configuring
method's `InvocationExpressionSyntax` chains plus the semantic model — one concern per
slice, deleting the matching regex path as each lands.

The walker's core abstraction: given a method body (`OnModelCreating`, a config class's
`Configure`, or a snapshot's `BuildModel`), enumerate the fluent invocation chains rooted
at `modelBuilder.Entity<T>(...)` / `builder.` and fold each chain's calls into the target
`EfEntity`. Because it walks real syntax, "which entity does this `.Property` belong to"
is answered by the receiver expression, not by scanning nearby text.

### Slices (each = one PR)

- **Slice 0 — Golden-file harness.** Snapshot current ERD output for every `samples/erd/*`
  context plus a new fixture corpus (contexts exercising every construct) into golden
  files. This is the cross-slice regression net; no production change.
- **Slice 1 — Relationships.** `HasOne/HasMany/WithOne/WithMany/HasForeignKey/IsRequired/
  OnDelete` via chain walking. Retire `RelationshipConfigParser` regex paths.
- **Slice 2 — Property config.** `Property/HasKey/HasMaxLength/HasColumnType/
  HasDefaultValue[Sql]/HasPrecision/IsRequired`. Retire `PropertyConfigParser` regex.
- **Slice 3 — Owned types & join tables.** `OwnsOne/OwnsMany` (nested builder lambdas
  scoped by syntax, not paren-balance) and `UsingEntity` join-entity synthesis.
- **Slice 4 — Gap: `IEntityTypeConfiguration<T>`.** Resolve `ApplyConfiguration(new XConfig())`
  and `ApplyConfigurationsFromAssembly(...)` targets, walk each config class's
  `Configure(EntityTypeBuilder<T>)` body with the same walker.
- **Slice 5 — Gap: base-class `DbSet`s.** Walk the context's `BaseType` chain when
  discovering `DbSet<T>` members and their entity syntax.
- **Slice 6 — ModelSnapshot.** Snapshots are generated C# (`BuildModel(ModelBuilder)`) with
  the same fluent shape → reuse the walker; retire `ModelSnapshotParser` and snapshot
  regex patterns.

### Per-slice checklist

1. Build the Roslyn mechanism for the concern.
2. Wire it behind the seam (old and new may briefly coexist within `ApplyFluentApiConstraints`).
3. Delete the corresponding regex code + now-dead constants/patterns.
4. Golden files + targeted unit tests green; full suite + `dotnet format` clean.
5. PR → review → CI → merge.

### EF-internal Lows folded here (deleted, not fixed)

The deferred §4.3 Lows live inside the regex layer and are removed wholesale by the
rewrite rather than patched first: `#12` (join-table over-removal / `fk.Name` mis-strip),
`#13` (`ToTable` schema overload / dead `TableName`), `#14` (dead truncation branch /
whitespace split), `#16` (`IsInsideUsingEntityBlock` O(n²)). The rewrite must preserve or
correct their intent (verified by golden files).

### Risks & mitigations

- **Silent output regression** → Slice 0 golden files gate every later slice.
- **Semantic-model gaps** when referenced assemblies are missing → the walker degrades to
  syntax-only (identifier/arity) like today, never throws.
- **Scope creep in a slice** → each slice is one concern; feature-gap slices (4/5) only
  after the core walker (1–3) exists.

**Exit criteria:** all six regex files retired; golden files reproduce today's output
except where a golden is deliberately updated for a fixed bug; `IEntityTypeConfiguration`
and base-class `DbSet`s covered by new tests.

---

## Phase 2 — Remaining & new Lows

The deferred set from PR #142 (minus the EF-regex Lows deleted in Phase 1) plus anything
Phase 0 surfaces, grouped into a few themed PRs, each behavior change golden/unit-tested:

- **Class-diagram output Lows** (§4.2 #10 generic-outer types, #11 substring collection
  detection, #13 simple-name dedup, #14 sanitize collision) — output-changing; disambiguate
  only on collision where possible to bound test churn.
- **Interface/plumbing Lows** (§4.1 #17 & §4.4 #9 — thread `CancellationToken` through class/EF
  analysis) — one PR touching the two service interfaces.
- **CI items needing config** (§4.6 #11 Sonar PR quality gate, #12 poll NuGet instead of
  `sleep`, #14 committed-version strategy) — YAML + maintainer settings; user merges.
- **Any new findings** from Phase 0 at their severity.

**Exit criteria:** Phase-0 findings list fully triaged to done / deliberately-deferred with
rationale.

---

## Sequencing

```
Phase 0 (audit)  →  Phase 1 (slices 0 → 1 → 2 → 3 → 4 → 5 → 6)  →  Phase 2 (themed PRs)
```

New Highs from Phase 0, if any, jump ahead of Phase 1. Each slice/PR is independent with
its own review + CI + merge cycle. The full test suite, `TreatWarningsAsErrors`, and
`dotnet format` gate every PR.

## Success criteria

- Fresh audit report committed; no open High/Medium regressions.
- The six EF regex files are gone; ERD output is preserved-or-corrected under golden files.
- `IEntityTypeConfiguration<T>`, `ApplyConfigurationsFromAssembly`, and base-class `DbSet`s
  are supported and tested.
- Remaining Lows are done or deferred with written rationale.
