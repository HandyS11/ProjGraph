# EF Rewrite — Slice 6: ModelSnapshot via the fluent walkers — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route ModelSnapshot analysis (`BuildModel(ModelBuilder)`) through the existing Roslyn fluent walkers and delete the now-dead regex parsing layer, completing Phase 1 of the EF rewrite.

**Architecture:** `ModelSnapshotParser.Parse` keeps its signature but its internals switch from the regex `FluentApiConfigurationParser.ApplyConstraintsFromMethod` to the three walkers (`FluentEntityWalker` → `FluentPropertyWalker` → `FluentRelationshipWalker`) — snapshots have the same fluent shape as `OnModelCreating` and the walkers already parse the string-literal overloads. The snapshot entity-name pre-scan in `EfModelAnalyzer` becomes a syntax walk. Afterwards the regex layer is dead: its three parser/utility files are deleted (live members move to their single remaining consumers) and `EfAnalysisRegexPatterns` shrinks to the two SQL type-string patterns. A new ModelSnapshot golden fixture — generated from the **old** path before the swap — gates parity.

**Tech Stack:** C# / .NET 10, Roslyn (`Microsoft.CodeAnalysis.CSharp.Syntax`), xUnit, FluentAssertions, `RoslynTestHelper`, `EfErdGoldenTests` golden harness, `PhysicalFileSystem`.

**Spec:** `docs/superpowers/specs/2026-07-14-ef-rewrite-slice6-model-snapshot-design.md`

## Global Constraints

- Target `net10.0`; `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true` — no warnings, no unused usings.
- XML documentation required on all public APIs; keep `<summary>` docs on internal walker members for consistency with siblings.
- Use `dtk dotnet build` / `dtk dotnet test` / `dtk dotnet format` (DotnetTokenKiller wrapper) for all build/test/format commands.
- This is a **parity** slice. The **nine existing goldens** (`simple-context`, `complex-ecommerce`, `fixture-relationships`, `fixture-owned-join`, `fixture-property-config`, `fixture-config-class`, `fixture-separate-config`, `fixture-base-dbset`, `fixture-base-dbset-multifile`) must stay **byte-identical** — never regenerate them. The one **new** golden (`fixture-snapshot`, Task 1) is regenerated exactly once at the swap (Task 3) with a fully predicted diff (the regex owned-type leak disappearing); any *other* diff in it means STOP and diagnose.
- The four `EfAnalysisServiceSnapshotTests` must pass **unchanged** — they are the black-box parity lock.
- Do not touch `RelationshipAnalyzer`, `DefaultValueResolver`, `EntityAnalyzer` (except no source change is planned there), the CLI/MCP surface, or `IEfModelAnalyzer`.
- New golden fixture entity names must not collide with classes in other fixture files (`Golden/fixtures/` is one search directory at analysis time); this plan uses `Journal`/`Entry`/`ShipmentItem`/`GeoTag`, which are unique in the repo.
- Fixture `.cs` files and golden `.mmd` files are glob-included by the test csproj (`Compile Remove` + `None Include ... CopyToOutputDirectory`) — **no csproj changes needed**.

### Key behaviors to preserve (verified against both pipelines pre-implementation)

- Walkers already parse every snapshot construct: `Entity("Ns.T", b => ...)` (first string literal, last dotted segment), `Property<int>("Id")` (generic type + string name), `HasKey("A", "B")` (string literals, no phantom `","` property), `HasOne("Ns.T", "Nav").WithMany("Navs").HasForeignKey("FkId").OnDelete(...).IsRequired()` (chain-scoped), `ToTable("X")`. Unrecognized chain calls (`ValueGeneratedOnAdd`, `HasAnnotation`, `HasIndex`, `Navigation`, `WithOwner`) are no-ops in both paths.
- **One deliberate behavior fix at the swap:** the regex section-scan attributed `Property`/`HasKey` calls inside an `OwnsOne(...)` builder lambda to the *owner* entity (a §4.3 leak); the walkers scope nested builder lambdas out. The snapshot fixture includes an `OwnsOne` block precisely so this surfaces as a reviewed golden diff in Task 3 (owner loses the leaked `GeoTag` properties), not as silent drift.
- `RelationshipAnalyzer.AnalyzeRelationships` still runs after `ModelSnapshotParser.Parse` in `AnalyzeSnapshotAsync` — unchanged.
- Entity order in `model.Entities` is document order of `Entity(...)` calls in both paths (regex split = document order); duplicate `Entity("Ns.T")` blocks (property pass + relationship pass) materialize once.

---

### Task 1: Snapshot golden fixture + `RenderSnapshot` (pre-swap baseline)

**Files:**
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/JournalSnapshot.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs`
- Create (generated): `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-snapshot.mmd`

**Interfaces:**
- Consumes: `EfAnalysisService.AnalyzeSnapshotAsync(string, string?)`, `MermaidErdRenderer`, `EfGoldenRunner.Verify`.
- Produces: `EfGoldenRunner.RenderSnapshot(string snapshotPath, string? snapshotName) : string` (Task 3 regenerates its golden); golden case `fixture-snapshot`.

- [ ] **Step 1: Create the ModelSnapshot fixture**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/JournalSnapshot.cs`:

```csharp
// <auto-generated /> — golden fixture: a realistic EF-generated ModelSnapshot exercising the
// string-based fluent forms (Entity("Ns.T"), Property<T>("Name"), HasKey("A","B"),
// HasOne(...).WithMany(...).HasForeignKey(...)), annotation noise, a second per-entity pass for
// relationships, and a nested OwnsOne builder block.
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace SnapFx.Migrations
{
    [DbContext(typeof(JournalContext))]
    partial class JournalContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            modelBuilder.Entity("SnapFx.Journal", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("Rating")
                        .HasColumnType("nvarchar(max)")
                        .HasDefaultValueSql("N'unrated'");

                    b.HasKey("Id");

                    b.ToTable("Journals");
                });

            modelBuilder.Entity("SnapFx.Entry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("JournalId")
                        .HasColumnType("int");

                    b.Property<decimal>("Score")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("JournalId");

                    b.ToTable("Entries");
                });

            modelBuilder.Entity("SnapFx.ShipmentItem", b =>
                {
                    b.Property<int>("OrderId")
                        .HasColumnType("int");

                    b.Property<int>("ProductId")
                        .HasColumnType("int");

                    b.Property<int>("Quantity")
                        .HasColumnType("int");

                    b.HasKey("OrderId", "ProductId");

                    b.ToTable("ShipmentItems");
                });

            modelBuilder.Entity("SnapFx.Entry", b =>
                {
                    b.HasOne("SnapFx.Journal", "Journal")
                        .WithMany("Entries")
                        .HasForeignKey("JournalId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Journal");
                });

            modelBuilder.Entity("SnapFx.Journal", b =>
                {
                    b.OwnsOne("SnapFx.GeoTag", "Location", b1 =>
                        {
                            b1.Property<int>("JournalId")
                                .HasColumnType("int");

                            b1.Property<string>("City")
                                .HasColumnType("nvarchar(max)");

                            b1.HasKey("JournalId");

                            b1.ToTable("Journals");

                            b1.WithOwner()
                                .HasForeignKey("JournalId");
                        });

                    b.Navigation("Entries");

                    b.Navigation("Location");
                });
        }
    }
}
```

- [ ] **Step 2: Add `RenderSnapshot` to `EfGoldenRunner` and the snapshot golden case**

In `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs`, add to the `EfErdGoldenTests` class (after `Erd_MatchesGolden`):

```csharp
    [Fact]
    public void SnapshotErd_MatchesGolden()
    {
        var actual = EfGoldenRunner.RenderSnapshot(
            FixturePath("JournalSnapshot.cs"), "JournalContextModelSnapshot");
        EfGoldenRunner.Verify("fixture-snapshot", actual);
    }
```

In `EfGoldenRunner`, extract the service construction shared by both entry points and add `RenderSnapshot`. Replace the existing `RenderContext` method with:

```csharp
    public static string RenderContext(string samplePath, string? contextName)
    {
        var service = CreateService();

#pragma warning disable VSTHRD002 // Deliberate sync-over-async bridge: harness API is pinned to a synchronous
        // signature (see task brief); no SynchronizationContext deadlock risk under xUnit.
        var model = service.AnalyzeContextAsync(samplePath, contextName).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        return Render(model);
    }

    public static string RenderSnapshot(string snapshotPath, string? snapshotName)
    {
        var service = CreateService();

#pragma warning disable VSTHRD002 // Same deliberate sync-over-async bridge as RenderContext.
        var model = service.AnalyzeSnapshotAsync(snapshotPath, snapshotName).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        return Render(model);
    }

    private static EfAnalysisService CreateService()
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        return new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));
    }

    private static string Render(EfModel model)
        => Normalize(new MermaidErdRenderer().Render(model, new DiagramOptions(true, false)));
```

(Keep `Verify`, `Normalize`, `GoldenDirectory`, `SourceGoldenDirectory` as they are.)

- [ ] **Step 3: Run the new golden case to verify it fails (golden missing)**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram 2>/dev/null; dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~SnapshotErd_MatchesGolden"`
Expected: FAIL — "golden 'fixture-snapshot.mmd' must exist; run with UPDATE_EF_GOLDENS=1 to generate it". (Ignore the first command if it errors; it is only a warm-up and not required.)

- [ ] **Step 4: Generate the golden from the CURRENT regex path**

Run: `UPDATE_EF_GOLDENS=1 dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~SnapshotErd_MatchesGolden"`
Then: `cat tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-snapshot.mmd`

Expected content shape (title `JournalContext`; `Journal` **includes the leaked owned-type properties `JournalId` and `City`** — that is the regex bug the fixture documents; it disappears in Task 3):

- `Journal`: `Id PK` (int), `Name` (required, max:200), `Rating` (string, default), **leaked** `JournalId PK` and `City`
- `Entry`: `Id PK`, `JournalId FK`, `Score` (precision 18,2), `Title` (required)
- `ShipmentItem`: `OrderId PK`, `ProductId PK`, `Quantity`
- one relationship `Journal ||--o{ Entry` (one-to-many, required)

If the leaked `JournalId`/`City` lines are absent from `Journal`, or the relationship is missing, STOP — the fixture is not exercising the constructs it must; fix the fixture before proceeding.

- [ ] **Step 5: Run the full golden suite to verify all ten cases pass**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "Category=Golden"`
Expected: PASS — 9 context cases + 1 snapshot case. Then `git status` must show only the three files of this task (fixture, test file, new golden).

- [ ] **Step 6: Commit**

```bash
git add tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/JournalSnapshot.cs tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-snapshot.mmd
git commit -m "test(ef): Slice 6 Task 1 — ModelSnapshot golden fixture pinned to the pre-swap regex output"
```

---

### Task 2: Walker snapshot-shape unit tests (characterization pins)

These pin the walker behaviors the snapshot path will rely on. They are expected to PASS against the walkers as they are — reconnaissance verified the string-form support exists; the tests lock it before the swap depends on it. If any fails, STOP: the reconnaissance was wrong and the walker needs the fix *first*.

**Files:**
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentPropertyWalkerTests.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentRelationshipWalkerTests.cs`

**Interfaces:**
- Consumes: each test file's existing `Build(string, params string[])` helper (compiles source, finds the `OnModelCreating` method, seeds entities), `FluentPropertyWalker.Apply(method, entities, compilation)`, `FluentRelationshipWalker.Apply(method, entities, model, compilation)`.
- Produces: nothing new — regression pins only.

- [ ] **Step 1: Add snapshot-shape tests to `FluentPropertyWalkerTests.cs`** (before the closing brace)

```csharp
    [Fact]
    public void Apply_SnapshotStringForm_UsesGenericTypeAndAppliesChain()
    {
        // The generated-ModelSnapshot shape: string entity, Property<T>("name") with generic type and
        // string-literal name, unknown generated calls (ValueGeneratedOnAdd) as no-ops in the chain.
        const string source = """
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity("SnapFx.Journal", b =>
                    {
                        b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
                        b.Property<string>("Name").IsRequired().HasMaxLength(200);
                        b.HasKey("Id");
                    });
                }
            }
            """;
        var (method, compilation, entities) = Build(source);
        entities["Journal"] = new EfEntity { Name = "Journal" };

        FluentPropertyWalker.Apply(method, entities, compilation);

        var id = Property(entities, "Journal", "Id");
        id.Type.Should().Be("int");
        id.IsPrimaryKey.Should().BeTrue();
        var name = Property(entities, "Journal", "Name");
        name.Type.Should().Be("string");
        name.IsRequired.Should().BeTrue();
        name.MaxLength.Should().Be(200);
    }

    [Fact]
    public void Apply_HasKeyStringLiterals_MarksCompositePrimaryKey()
    {
        const string source = """
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity("SnapFx.ShipmentItem", b =>
                    {
                        b.Property<int>("OrderId");
                        b.Property<int>("ProductId");
                        b.HasKey("OrderId", "ProductId");
                    });
                }
            }
            """;
        var (method, compilation, entities) = Build(source);
        entities["ShipmentItem"] = new EfEntity { Name = "ShipmentItem" };

        FluentPropertyWalker.Apply(method, entities, compilation);

        entities["ShipmentItem"].Properties.Should().HaveCount(2);
        Property(entities, "ShipmentItem", "OrderId").IsPrimaryKey.Should().BeTrue();
        Property(entities, "ShipmentItem", "ProductId").IsPrimaryKey.Should().BeTrue();
        entities["ShipmentItem"].Properties.Should().NotContain(p => p.Name.Contains(','));
    }
```

- [ ] **Step 2: Add snapshot-shape tests to `FluentRelationshipWalkerTests.cs`** (before the closing brace)

```csharp
    [Fact]
    public void Apply_SnapshotStringForm_CreatesRelationshipAndMarksForeignKey()
    {
        // The generated-ModelSnapshot relationship pass: string target + navigation names, FK by string,
        // OnDelete noise, explicit IsRequired() in the same chain.
        const string source = """
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity("SnapFx.Entry", b =>
                    {
                        b.HasOne("SnapFx.Journal", "Journal")
                            .WithMany("Entries")
                            .HasForeignKey("JournalId")
                            .OnDelete(DeleteBehavior.Cascade)
                            .IsRequired();
                    });
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source);
        entities["Journal"] = new EfEntity { Name = "Journal" };
        entities["Entry"] = new EfEntity { Name = "Entry" };

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.SourceEntity.Should().Be("Journal");
        rel.TargetEntity.Should().Be("Entry");
        rel.Type.Should().Be(EfRelationshipType.OneToMany);
        rel.IsRequired.Should().BeTrue();
        entities["Entry"].Properties.Should().ContainSingle(p => p.Name == "JournalId" && p.IsForeignKey);
    }

    [Fact]
    public void Apply_IsRequiredOnSeparatePropertyChain_DoesNotAffectRelationship()
    {
        // Preserves the RelationshipConfigParserTests regression intent before that suite is deleted in
        // Task 4: an IsRequired() on a *property* chain after the relationship statement must not flip
        // the relationship's requiredness (one-to-one defaults to optional).
        const string source = """
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity("SnapFx.Entry", b =>
                    {
                        b.HasOne("SnapFx.Journal", "Journal").WithOne("Entry");
                        b.Property<string>("Note").IsRequired();
                    });
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source);
        entities["Journal"] = new EfEntity { Name = "Journal" };
        entities["Entry"] = new EfEntity { Name = "Entry" };

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.Type.Should().Be(EfRelationshipType.OneToOne);
        rel.IsRequired.Should().BeFalse();
    }
```

- [ ] **Step 3: Run the new tests — expect PASS (pins, not TDD-red)**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~FluentPropertyWalkerTests|FullyQualifiedName~FluentRelationshipWalkerTests"`
Expected: PASS, including the 4 new tests. If a new test FAILS, STOP — fix the walker gap first (with the failing test as the red), then re-run.

- [ ] **Step 4: Commit**

```bash
git add tests/ProjGraph.Tests.Unit.EntityFramework/FluentPropertyWalkerTests.cs tests/ProjGraph.Tests.Unit.EntityFramework/FluentRelationshipWalkerTests.cs
git commit -m "test(ef): Slice 6 Task 2 — pin walker support for ModelSnapshot string-form chains"
```

---

### Task 3: Swap the snapshot path onto the walkers

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/ModelSnapshotParser.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/EfModelAnalyzer.cs`
- Modify (regenerate): `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-snapshot.mmd`

**Interfaces:**
- Consumes: `FluentEntityWalker.Apply`, `FluentPropertyWalker.Apply`, `FluentRelationshipWalker.Apply` (existing signatures, no `ambientEntity` argument — snapshots always have `Entity(...)` roots).
- Produces: `FluentEntityWalker.CollectEntityNames(MethodDeclarationSyntax) : HashSet<string>` (used by `EfModelAnalyzer`); `ModelSnapshotParser.Parse` unchanged signature, walker-backed. After this task `ApplyConstraintsFromMethod` has **zero callers** (Task 4 deletes it).

- [ ] **Step 1: Add `CollectEntityNames` to `FluentEntityWalker`**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs`, add after the `Apply` method:

```csharp
    /// <summary>
    /// Collects the entity type names referenced by every <c>Entity&lt;T&gt;()</c> / <c>Entity("Ns.T")</c>
    /// invocation in <paramref name="method"/>, namespace-stripped. Unlike <see cref="Apply"/>, nested
    /// builder scopes are NOT excluded: this feeds entity-*file* discovery, which wants maximal recall,
    /// while configuration scoping stays the walkers' concern.
    /// </summary>
    /// <param name="method">The configuring method (e.g. a snapshot's <c>BuildModel</c>) to scan.</param>
    public static HashSet<string> CollectEntityNames(MethodDeclarationSyntax method)
    {
        var names = new HashSet<string>();
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax ma
                && SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.Entity
                && EntityNameFromInvocation(invocation) is { Length: > 0 } name)
            {
                names.Add(name);
            }
        }

        return names;
    }
```

- [ ] **Step 2: Rewire `ModelSnapshotParser.Parse` onto the walkers**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/ModelSnapshotParser.cs`, replace the `Parse` method body and update the class/method docs. The full new `Parse`:

```csharp
    /// <summary>
    /// Parses a ModelSnapshot class to extract the Entity Framework model.
    /// </summary>
    /// <param name="snapshotClass">The <see cref="ClassDeclarationSyntax"/> representing the ModelSnapshot class.</param>
    /// <param name="snapshotType">The <see cref="INamedTypeSymbol"/> representing the type of the ModelSnapshot class.</param>
    /// <param name="compilation">The <see cref="Compilation"/> object used for Roslyn analysis.</param>
    /// <returns>
    /// An <see cref="EfModel"/> object representing the parsed Entity Framework model, including its context name and entities.
    /// </returns>
    public static EfModel Parse(ClassDeclarationSyntax snapshotClass, INamedTypeSymbol snapshotType,
        Compilation compilation)
    {
        var model = new EfModel { ContextName = ExtractContextName(snapshotType) };
        var entities = new Dictionary<string, EfEntity>();

        var buildModelMethod = snapshotClass.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == EfAnalysisConstants.EfMethods.BuildModel);

        if (buildModelMethod is null || (buildModelMethod.Body is null && buildModelMethod.ExpressionBody is null))
        {
            return model;
        }

        // A generated snapshot's BuildModel has the same fluent shape as OnModelCreating (string-based
        // Entity/Property/HasKey/HasOne overloads), so the same Roslyn syntax walkers apply, in the same
        // order as the context path. The walkers add materialized entities to the model directly.
        FluentEntityWalker.Apply(buildModelMethod, entities, model, compilation);
        FluentPropertyWalker.Apply(buildModelMethod, entities, compilation);
        FluentRelationshipWalker.Apply(buildModelMethod, entities, model, compilation);

        return model;
    }
```

Also update the class `<summary>` to:

```csharp
/// <summary>
/// Parser for Entity Framework ModelSnapshot files: locates the snapshot's <c>BuildModel</c> method and
/// folds its fluent configuration into an <see cref="EfModel"/> via the Roslyn syntax walkers
/// (<see cref="FluentEntityWalker"/>, <see cref="FluentPropertyWalker"/>, <see cref="FluentRelationshipWalker"/>).
/// </summary>
```

(`ExtractContextName` stays unchanged. Note the guard now also accepts expression-bodied `BuildModel`.)

- [ ] **Step 3: Replace the regex entity-name pre-scan in `EfModelAnalyzer`**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/EfModelAnalyzer.cs`, replace the whole `ExtractEntityTypeNamesFromSnapshot` method with:

```csharp
    /// <summary>
    /// Extracts entity type names from a ModelSnapshot class by walking the syntax of its BuildModel
    /// method for <c>Entity&lt;T&gt;()</c> / <c>Entity("Ns.T")</c> invocations.
    /// </summary>
    /// <param name="snapshotClass">The <see cref="ClassDeclarationSyntax"/> of the ModelSnapshot class.</param>
    /// <returns>A <see cref="HashSet{T}"/> containing the names of all entities found in the snapshot.</returns>
    /// <seealso cref="FluentEntityWalker.CollectEntityNames(MethodDeclarationSyntax)"/>
    private static HashSet<string> ExtractEntityTypeNamesFromSnapshot(ClassDeclarationSyntax snapshotClass)
    {
        var buildModelMethod = snapshotClass.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == EfAnalysisConstants.EfMethods.BuildModel);

        if (buildModelMethod is null || (buildModelMethod.Body is null && buildModelMethod.ExpressionBody is null))
        {
            return [];
        }

        return FluentEntityWalker.CollectEntityNames(buildModelMethod);
    }
```

Then delete the now-unused `using ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;` at the top of the file (the build fails on unused usings — verify no other member in this file references `EfAnalysisRegexPatterns`; as of writing, none does).

- [ ] **Step 4: Run the snapshot behavior tests (parity lock)**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~EfAnalysisServiceSnapshotTests"`
Expected: PASS — all 4, **unchanged test code**.

- [ ] **Step 5: Regenerate the snapshot golden and adjudicate the diff**

Run: `UPDATE_EF_GOLDENS=1 dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "Category=Golden"`
Then: `git diff tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens`

Expected: **exactly one file changed** — `fixture-snapshot.mmd`, and the only change is `Journal` losing the leaked owned-type lines (`JournalId` with its `PK` marker, and `City`). This is the documented §4.3 owned-type leak fix. If ANY of the nine context goldens changed, or the snapshot diff contains anything beyond those removals, STOP and diagnose before committing — do not accept an unexplained diff.

- [ ] **Step 6: Run the full golden suite + full EF unit suite against the updated golden**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework`
Expected: PASS (goldens now assert the walker output; regex layer still present but snapshot-dead).

- [ ] **Step 7: Full solution build + suite + format**

Run: `dtk dotnet build ProjGraph.slnx` then `dtk dotnet test ProjGraph.slnx` then `dtk dotnet format ProjGraph.slnx --verify-no-changes`
Expected: build 0 warnings/errors; all tests green; no format changes.

- [ ] **Step 8: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/ModelSnapshotParser.cs src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs src/ProjGraph.Lib.EntityFramework/Infrastructure/EfModelAnalyzer.cs tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-snapshot.mmd
git commit -m "feat(ef): Slice 6 Task 3 — route ModelSnapshot parsing through the fluent walkers (golden: owned-type leak fixed)"
```

---

### Task 4: Delete the dead regex layer

Everything below was verified dead-after-Task-3 by a full-solution usage grep. Moves keep the exact member implementations; only the host type changes. `TreatWarningsAsErrors` + the full build is the safety net for any missed reference.

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/EfPropertyFactory.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/Patterns/EfAnalysisRegexPatterns.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/Constants/EfAnalysisConstants.cs`
- Delete: `src/ProjGraph.Lib.EntityFramework/Infrastructure/RelationshipConfigParser.cs`
- Delete: `src/ProjGraph.Lib.EntityFramework/Infrastructure/PropertyConfigParser.cs`
- Delete: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiParsingUtilities.cs`
- Delete: `tests/ProjGraph.Tests.Unit.EntityFramework/RelationshipConfigParserTests.cs`
- Delete: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentApiParsingUtilitiesTests.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/EfPropertyFactoryTests.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/EfAnalysisRegexPatternsTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `EfPropertyFactory.GetOrCreateProperty(EfEntity, string, string) : EfProperty` and `EfPropertyFactory.IsValueTypeString(string) : bool` (both internal; new callers are `FluentPropertyWalker` and `FluentRelationshipWalker`). `CreateShadowRelationship` and `ApplyConfiguration` become private members of `FluentRelationshipWalker` / `FluentPropertyWalker` respectively.

- [ ] **Step 1: Move `GetOrCreateProperty` + `IsValueTypeString` into `EfPropertyFactory`**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/EfPropertyFactory.cs`, add `using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;` at the top, extend the class `<summary>`, and append the two members (implementations copied verbatim from `FluentApiParsingUtilities`, XML docs included):

```csharp
    /// <summary>
    /// Gets an existing property or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="entity">The entity containing the property.</param>
    /// <param name="propName">The property name.</param>
    /// <param name="type">The property type.</param>
    /// <returns>The EfProperty object.</returns>
    public static EfProperty GetOrCreateProperty(EfEntity entity, string propName, string type)
    {
        var property = entity.Properties.FirstOrDefault(p => p.Name == propName);
        if (property is null)
        {
            var detectedType = type;
            if (string.IsNullOrEmpty(detectedType))
            {
                detectedType =
                    propName.EndsWith(EfAnalysisConstants.Suffixes.IdSuffix, StringComparison.OrdinalIgnoreCase)
                        ? EfAnalysisConstants.DataTypes.Guid
                        : EfAnalysisConstants.DataTypes.StringTypeName;
            }

            property = new EfProperty
            {
                Name = propName,
                Type = detectedType,
                IsValueType = IsValueTypeString(detectedType)
            };
            entity.Properties.Add(property);
        }
        else if (!string.IsNullOrEmpty(type))
        {
            var updated = CopyWith(property, new EfPropertyOverrides
            {
                Type = type,
                IsValueType = IsValueTypeString(type)
            });
            var index = entity.Properties.IndexOf(property);
            if (index >= 0)
            {
                entity.Properties[index] = updated;
            }

            property = updated;
        }

        return property;
    }

    /// <summary>
    /// Determines whether a type name represents a value type.
    /// </summary>
    /// <param name="type">The type name to check.</param>
    public static bool IsValueTypeString(string type)
    {
        var typeName = type.TrimEnd('?');
        if (typeName.Contains('.', StringComparison.Ordinal))
        {
            typeName = typeName[(typeName.LastIndexOf('.') + 1)..];
        }

        return EfAnalysisConstants.DataTypes.ValueTypes.Contains(typeName);
    }
```

Update the class summary to:

```csharp
/// <summary>
/// Factory for <see cref="EfProperty"/> instances: creates modified copies (init-only setters make
/// in-place mutation impossible) and gets-or-creates named properties on an entity with type inference.
/// </summary>
```

- [ ] **Step 2: Move `ApplyConfiguration` + appliers into `FluentPropertyWalker`; retarget its utility calls**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs`:

1. Add `using ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;` to the usings.
2. Replace both `FluentApiParsingUtilities.GetOrCreateProperty(` occurrences with `EfPropertyFactory.GetOrCreateProperty(`.
3. Replace the call `PropertyConfigParser.ApplyConfiguration(current, name, argText, compilation)` with `ApplyConfiguration(current, name, argText, compilation)`.
4. Append these members (moved verbatim from `PropertyConfigParser`, `ApplyConfiguration` becomes private, doc updated):

```csharp
    /// <summary>
    /// Applies a single property-configuration call to a property, dispatching on the fluent method name;
    /// returns the same instance for unrecognized methods (e.g. generated-snapshot noise such as
    /// <c>ValueGeneratedOnAdd</c> or <c>HasAnnotation</c>).
    /// </summary>
    /// <param name="property">The property to configure.</param>
    /// <param name="configMethod">The configuration method name (e.g. <c>HasMaxLength</c>).</param>
    /// <param name="configArg">The raw argument text captured between the call's parentheses.</param>
    /// <param name="compilation">The Roslyn compilation for constant/enum resolution.</param>
    private static EfProperty ApplyConfiguration(EfProperty property, string configMethod, string configArg,
        Compilation compilation)
    {
        return configMethod switch
        {
            EfAnalysisConstants.EfMethods.IsRequired => ApplyIsRequiredConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasMaxLength => ApplyMaxLengthConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasPrecision => ApplyPrecisionConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasColumnType => ApplyColumnTypeConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasDefaultValue => DefaultValueResolver.CreateWithDefaultValue(property,
                configArg, compilation),
            EfAnalysisConstants.EfMethods.HasDefaultValueSql => DefaultValueResolver.CreateWithDefaultValueSql(
                property, configArg),
            _ => property
        };
    }

    private static EfProperty ApplyIsRequiredConfiguration(EfProperty property, string configArg)
    {
        var isRequired = string.IsNullOrEmpty(configArg) ||
                         configArg.Equals("true", StringComparison.OrdinalIgnoreCase);
        return EfPropertyFactory.CopyWith(property, new EfPropertyOverrides
        {
            IsRequired = isRequired,
            IsExplicitlyRequired = isRequired || property.IsExplicitlyRequired
        });
    }

    private static EfProperty ApplyMaxLengthConfiguration(EfProperty property, string configArg)
    {
        if (int.TryParse(configArg, out var maxLen))
        {
            return EfPropertyFactory.CopyWith(property, new EfPropertyOverrides
            {
                MaxLength = maxLen
            });
        }

        return property;
    }

    /// <summary>
    /// Configures the column type for a property, inferring max length from column type definition if needed.
    /// </summary>
    /// <param name="property">The property to configure.</param>
    /// <param name="configArg">The column type argument.</param>
    private static EfProperty ApplyColumnTypeConfiguration(EfProperty property, string configArg)
    {
        if (property.MaxLength is not null)
        {
            return property;
        }

        var match = EfAnalysisRegexPatterns.NumberInParensRegex().Match(configArg);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var len))
        {
            return EfPropertyFactory.CopyWith(property, new EfPropertyOverrides
            {
                MaxLength = len
            });
        }

        return property;
    }

    /// <summary>
    /// Creates a new property with the specified precision and scale.
    /// </summary>
    /// <param name="property">The source property.</param>
    /// <param name="configArg">The precision/scale argument string.</param>
    private static EfProperty ApplyPrecisionConfiguration(EfProperty property, string configArg)
    {
        var precisionArgs = configArg.Split(',');
        if (precisionArgs.Length < 1 || !int.TryParse(precisionArgs[0].Trim(), out var precision))
        {
            return property;
        }

        int? scale = null;
        if (precisionArgs.Length >= 2 && int.TryParse(precisionArgs[1].Trim(), out var s))
        {
            scale = s;
        }

        return EfPropertyFactory.CopyWith(property, new EfPropertyOverrides
        {
            Precision = precision,
            Scale = scale ?? property.Scale
        });
    }
```

5. Update the class header doc: replace `replacing the text/regex based <see cref="PropertyConfigParser"/> for the DbContext path` with `having replaced the retired text/regex property parser`.
6. In `NestedBuilderScopes`'s doc comment, drop the sentence `This mirrors the regex parser, whose single-level argument capture absorbed such nested calls so they were never applied to the outer entity.` (the regex parser no longer exists to mirror).

- [ ] **Step 3: Move `CreateShadowRelationship` into `FluentRelationshipWalker`; retarget its utility call**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs`:

1. Replace `RelationshipConfigParser.CreateShadowRelationship(` with `CreateShadowRelationship(` in `BuildRelationship`.
2. Replace `FluentApiParsingUtilities.GetOrCreateProperty(` with `EfPropertyFactory.GetOrCreateProperty(` in `MarkForeignKeys`.
3. Append the moved member (verbatim from `RelationshipConfigParser`, now private):

```csharp
    /// <summary>
    /// Creates an EfRelationship from has/with method combination.
    /// </summary>
    /// <param name="sourceEntity">The source entity name.</param>
    /// <param name="targetEntity">The target entity name.</param>
    /// <param name="hasMethod">The Has method name (HasOne/HasMany).</param>
    /// <param name="withMethod">The With method name (WithOne/WithMany).</param>
    /// <param name="explicitRequired">
    /// The explicit <c>.IsRequired(...)</c> value when configured, or <see langword="null"/> to apply the
    /// EF convention default for the relationship kind (required for one-to-many, optional otherwise).
    /// </param>
    private static EfRelationship CreateShadowRelationship(string sourceEntity, string targetEntity,
        string hasMethod, string withMethod, bool? explicitRequired = null)
    {
        return (hasMethod, withMethod) switch
        {
            (EfAnalysisConstants.EfMethods.HasOne, EfAnalysisConstants.EfMethods.WithMany) => new EfRelationship
            {
                SourceEntity = targetEntity,
                TargetEntity = sourceEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = explicitRequired ?? true
            },
            (EfAnalysisConstants.EfMethods.HasMany, EfAnalysisConstants.EfMethods.WithOne) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = explicitRequired ?? true
            },
            (EfAnalysisConstants.EfMethods.HasOne, EfAnalysisConstants.EfMethods.WithOne) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.OneToOne,
                IsRequired = explicitRequired ?? false
            },
            (EfAnalysisConstants.EfMethods.HasMany, EfAnalysisConstants.EfMethods.WithMany) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.ManyToMany,
                IsRequired = explicitRequired ?? false
            },
            _ => new EfRelationship
            {
                SourceEntity = targetEntity,
                TargetEntity = sourceEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = explicitRequired ?? true
            }
        };
    }
```

4. Update the class header doc: replace `replacing the text/regex based <see cref="RelationshipConfigParser"/> for the DbContext path` with `having replaced the retired text/regex relationship parser`.

- [ ] **Step 4: Trim `FluentApiConfigurationParser` to the context-path orchestrator**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs`:

1. Delete the methods `ApplyConstraintsFromMethod`, `ProcessEntityConfigSection`, `AddUniqueRelationships`, and `ParseEntityConfiguration` (everything below `FindOnModelCreatingMethod` except the closing class brace).
2. Delete the usings `ProjGraph.Lib.EntityFramework.Infrastructure.Extensions` and `ProjGraph.Lib.EntityFramework.Infrastructure.Patterns` (both only served the deleted members).
3. Replace the class `<summary>` (it names the deleted parsers) with:

```csharp
/// <summary>
/// Orchestrates the DbContext path's Fluent API analysis: locates <c>OnModelCreating</c> and folds its
/// configuration into the model via the Roslyn syntax walkers.
/// </summary>
```

4. In `ApplyFluentApiConstraints`, replace the first walker comment block (`// Context path: every concern now flows ... (retired in Slice 6).`) with:

```csharp
        // Every concern flows through the Roslyn syntax walkers. FluentEntityWalker materializes
        // fluent-only entities and applies ToTable; FluentPropertyWalker derives property config +
        // primary keys; FluentRelationshipWalker derives relationships + foreign keys.
```

- [ ] **Step 5: Delete the three dead source files**

```bash
git rm src/ProjGraph.Lib.EntityFramework/Infrastructure/RelationshipConfigParser.cs src/ProjGraph.Lib.EntityFramework/Infrastructure/PropertyConfigParser.cs src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiParsingUtilities.cs
```

- [ ] **Step 6: Trim `EfAnalysisRegexPatterns` to the two SQL type-string patterns**

Replace the entire contents of `src/ProjGraph.Lib.EntityFramework/Infrastructure/Patterns/EfAnalysisRegexPatterns.cs` with:

```csharp
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using System.Text.RegularExpressions;

namespace ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;

/// <summary>
/// Compiled regex patterns for parsing SQL type strings (column type names, attribute arguments).
/// These parse string *values*, not C# source — source-level Fluent API parsing is done on the syntax
/// tree by the fluent walkers.
/// </summary>
public static partial class EfAnalysisRegexPatterns
{
    /// <summary>
    /// A regex pattern to extract numeric arguments from SQL type names.
    /// </summary>
    /// <remarks>
    /// Captures numeric values within parentheses.
    /// Example: In nvarchar(30) or decimal(18,2), it captures 30 from the first match.
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.FluentApiPatterns.NumericArgumentPattern)]
    public static partial Regex NumberInParensRegex();

    /// <summary>
    /// A regex pattern to match decimal types with precision and scale.
    /// </summary>
    /// <remarks>
    /// Matches decimal(precision, scale) format and captures both precision and scale values.
    /// Used to extract precision and scale constraints from ColumnAttribute TypeName arguments.
    /// The regex captures two groups:
    /// - Group 1: The precision (number of total digits)
    /// - Group 2: The scale (number of digits after the decimal point)
    /// </remarks>
    [GeneratedRegex(EfAnalysisConstants.SqlTypePatterns.DecimalPattern)]
    public static partial Regex DecimalPrecisionRegex();
}
```

- [ ] **Step 7: Trim the `FluentApiPatterns` constants**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/Constants/EfAnalysisConstants.cs`, replace the `FluentApiPatterns` class body so only the surviving constant remains:

```csharp
    internal static class FluentApiPatterns
    {
        public const string NumericArgumentPattern = @"\((\d+)\)";
    }
```

(Leave `SqlTypePatterns` untouched.)

- [ ] **Step 8: Prune and migrate the regex-layer tests**

```bash
git rm tests/ProjGraph.Tests.Unit.EntityFramework/RelationshipConfigParserTests.cs tests/ProjGraph.Tests.Unit.EntityFramework/FluentApiParsingUtilitiesTests.cs
```

(The `RelationshipConfigParserTests` regression intents are preserved by the existing `Apply_ExplicitIsRequiredFalse_*` / `Apply_HasOneWithoutWith_*` walker tests plus the Task 2 `Apply_IsRequiredOnSeparatePropertyChain_*` pin; the `FluentApiParsingUtilities` tests for deleted regex helpers die with the helpers.)

In `tests/ProjGraph.Tests.Unit.EntityFramework/EfAnalysisRegexPatternsTests.cs`, delete the seven test methods for removed patterns (`EntityNameRegex_ValidPatterns_ShouldMatch`, `EntitySplitRegex_ShouldMatchEntityCalls`, `ShadowRelationshipRegex_HasOneWithMany_ShouldMatch`, `PropertyLambdaRegex_SimpleLambda_ShouldMatch`, `MethodCallRegex_ShouldMatchMethodWithArgs`, `ToTableRegex_ShouldMatchTableName`, `StringLiteralRegex_ShouldMatchQuotedStrings`), keeping only `NumberInParensRegex_ShouldMatchNumericArg` and `DecimalPrecisionRegex_ShouldMatchDecimalPrecisionScale`.

Create `tests/ProjGraph.Tests.Unit.EntityFramework/EfPropertyFactoryTests.cs` with the migrated property-utility tests:

```csharp
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="EfPropertyFactory"/>: get-or-create property semantics and value-type detection.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EfPropertyFactoryTests
{
    [Fact]
    public void GetOrCreateProperty_ExistingProperty_ShouldReturnExisting()
    {
        var entity = new EfEntity
        {
            Name = "Order"
        };
        entity.Properties.Add(new EfProperty
        {
            Name = "Total",
            Type = "decimal"
        });

        var result = EfPropertyFactory.GetOrCreateProperty(entity, "Total", "");

        result.Name.Should().Be("Total");
        result.Type.Should().Be("decimal");
        entity.Properties.Should().HaveCount(1);
    }

    [Fact]
    public void GetOrCreateProperty_NewProperty_ShouldCreateAndAdd()
    {
        var entity = new EfEntity
        {
            Name = "Order"
        };

        var result = EfPropertyFactory.GetOrCreateProperty(entity, "NewProp", "int");

        result.Name.Should().Be("NewProp");
        result.Type.Should().Be("int");
        entity.Properties.Should().HaveCount(1);
    }

    [Fact]
    public void GetOrCreateProperty_NewPropertyWithIdSuffix_ShouldInferGuidType()
    {
        var entity = new EfEntity
        {
            Name = "Order"
        };

        var result = EfPropertyFactory.GetOrCreateProperty(entity, "CustomerId", "");

        result.Type.Should().Be("Guid");
    }

    [Fact]
    public void GetOrCreateProperty_NewPropertyWithoutSuffix_ShouldInferStringType()
    {
        var entity = new EfEntity
        {
            Name = "Order"
        };

        var result = EfPropertyFactory.GetOrCreateProperty(entity, "Description", "");

        result.Type.Should().Be("string");
    }

    [Fact]
    public void GetOrCreateProperty_ExistingPropertyWithNewType_ShouldUpdateType()
    {
        var entity = new EfEntity
        {
            Name = "Order"
        };
        entity.Properties.Add(new EfProperty
        {
            Name = "Total",
            Type = "string"
        });

        var result = EfPropertyFactory.GetOrCreateProperty(entity, "Total", "decimal");

        result.Name.Should().Be("Total");
        result.Type.Should().Be("decimal");
        result.IsValueType.Should().BeTrue();
        entity.Properties.Should().HaveCount(1);
        entity.Properties[0].Type.Should().Be("decimal");
    }

    [Fact]
    public void IsValueTypeString_IntType_ShouldReturnTrue()
    {
        EfPropertyFactory.IsValueTypeString("int").Should().BeTrue();
    }

    [Fact]
    public void IsValueTypeString_StringType_ShouldReturnFalse()
    {
        EfPropertyFactory.IsValueTypeString("string").Should().BeFalse();
    }

    [Theory]
    [InlineData("Guid", true)]
    [InlineData("DateTime", true)]
    [InlineData("decimal", true)]
    [InlineData("bool", true)]
    [InlineData("MyCustomClass", false)]
    public void IsValueTypeString_VariousTypes_ShouldReturnExpected(string type, bool expected)
    {
        EfPropertyFactory.IsValueTypeString(type).Should().Be(expected);
    }

    [Theory]
    [InlineData("System.Int32", true)]
    [InlineData("System.Guid", true)]
    [InlineData("MyNamespace.MyClass", false)]
    [InlineData("System.DateTime?", true)]
    public void IsValueTypeString_DottedTypeNames_ShouldExtractLastPartAndCheck(string type, bool expected)
    {
        EfPropertyFactory.IsValueTypeString(type).Should().Be(expected);
    }
}
```

- [ ] **Step 9: Sweep for stale doc references to the deleted types**

Run: `grep -rn "RelationshipConfigParser\|PropertyConfigParser\|FluentApiParsingUtilities\|ApplyConstraintsFromMethod" src tests --include="*.cs" | grep -v obj/`
Expected: no hits. Fix any stragglers (they will be XML doc comments — e.g. `FluentPropertyWalkerTests.cs:12` and `FluentRelationshipWalkerTests.cs:12` header docs mention the parsers; reword to "the retired regex property/relationship parser").

- [ ] **Step 10: Build, full suite, format**

Run: `dtk dotnet build ProjGraph.slnx` then `dtk dotnet test ProjGraph.slnx` then `dtk dotnet format ProjGraph.slnx --verify-no-changes`
Expected: build 0 warnings/errors (this is the dead-reference safety net); all tests green — goldens **all ten byte-identical to Task 3's state** (pure deletion/moves cannot change output); no format changes.

- [ ] **Step 11: Commit**

```bash
git add -A src/ProjGraph.Lib.EntityFramework tests/ProjGraph.Tests.Unit.EntityFramework
git commit -m "refactor(ef): Slice 6 Task 4 — delete the regex fluent-parsing layer"
```

---

### Task 5: Final verification + PR

**Files:**
- Modify: `docs/superpowers/specs/2026-07-14-ef-rewrite-slice6-model-snapshot-design.md` (status line)
- This plan file (check off tasks)

- [ ] **Step 1: Verify the exit criteria mechanically**

- `grep -rn "Regex" src/ProjGraph.Lib.EntityFramework --include="*.cs" | grep -v obj/` → hits only in `Patterns/EfAnalysisRegexPatterns.cs` (2 patterns), `EntityAnalyzer.cs` (DecimalPrecisionRegex use), `FluentPropertyWalker.cs` (NumberInParensRegex use), i.e. **no regex touches C# source**.
- `git diff develop --stat -- tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens` → only `fixture-snapshot.mmd` added; the nine pre-existing goldens untouched.
- Full suite: `dtk dotnet test ProjGraph.slnx` → green. Format: `dtk dotnet format ProjGraph.slnx --verify-no-changes` → clean.

- [ ] **Step 2: Flip the design doc status**

In `docs/superpowers/specs/2026-07-14-ef-rewrite-slice6-model-snapshot-design.md`, change `**Status:** approved design, pre-implementation` to `**Status:** implemented`.

- [ ] **Step 3: Commit docs, push, open PR**

```bash
git add docs/superpowers
git commit -m "docs(ef): Slice 6 — design spec + implementation plan"
git push -u origin feat/ef-slice6-modelsnapshot
```

Open a PR against `develop` titled `feat(ef): Slice 6 — ModelSnapshot via the fluent walkers; retire the regex layer`, whose body summarizes: walker-routed snapshot path, the one deliberate golden change (owned-type leak fix, with before/after), the deletion inventory (3 source files, 2 test files, 9 regex patterns, ~1,000 LOC), and Phase 1 completion. Follow the repo's PR conventions (Copilot review → address → CI → merge).

---

## Self-Review

- **Spec coverage:** Component 1 (walker-based `Parse`) → Task 3 Step 2; Component 2 (syntax pre-scan) → Task 3 Steps 1+3; Component 3 (deletion cascade, all six rows) → Task 4; golden-first regression net → Task 1 (pre-swap) + Task 3 Step 5 (adjudicated regen); walker string-form pins → Task 2; test pruning/migration rows → Task 4 Step 8; exit criteria → Task 5. No gaps.
- **Placeholder scan:** all code steps carry complete code; no TBD/TODO/"similar to" references.
- **Type consistency:** `CollectEntityNames(MethodDeclarationSyntax) : HashSet<string>` defined in Task 3 Step 1, consumed in Step 3; `EfPropertyFactory.GetOrCreateProperty(EfEntity, string, string)` defined Task 4 Step 1, consumed Steps 2–3 and tested Step 8; `CreateShadowRelationship`/`ApplyConfiguration` private hosts match their single callers.
