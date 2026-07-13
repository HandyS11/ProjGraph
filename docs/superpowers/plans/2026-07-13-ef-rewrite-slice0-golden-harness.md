# EF Rewrite — Slice 0: Golden-File Characterization Harness — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a golden-file test harness that pins the current ERD output of every EF sample context, so the later strangler-fig slices that replace the regex Fluent-API parser with Roslyn can be proven to preserve behavior (byte-identical output except where a golden is deliberately updated for a fixed bug).

**Architecture:** A single xUnit theory renders each `(contextPath, contextName)` through the real `EfAnalysisService` + `MermaidErdRenderer` and compares the result to a committed `.mmd` golden file. A `UPDATE_EF_GOLDENS` environment variable switches the harness from *assert* to *regenerate* mode so goldens are captured/refreshed deterministically. This is the cross-slice regression net; it adds no production code.

**Tech Stack:** C# / .NET 10, xUnit v3, FluentAssertions, Roslyn (via existing `EfAnalysisService`), `PhysicalFileSystem`, `TestPathHelper`.

## Global Constraints

- Target framework `net10.0`; build enforces `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true` — no warnings allowed.
- XML documentation is required on all public APIs (test classes are not public API, but keep helpers `internal`/`private`).
- All new tests live in `tests/ProjGraph.Tests.Unit.EntityFramework`.
- Use `dtk dotnet build` / `dtk dotnet test` / `dtk dotnet format --verify-no-changes` (token-optimized wrapper) for all build/test/format commands.
- Golden files are committed text with `\n` line endings; comparisons must normalize CRLF→LF so Windows CI matches.
- Do NOT modify any `src/` production code in this slice.

---

### Task 1: Golden harness + first baseline (simple-context)

**Files:**
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/simple-context.mmd` (generated in Step 4)
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj` (mark goldens as content copied to output)

**Interfaces:**
- Consumes (existing production API, do not change):
  - `ProjGraph.Lib.EntityFramework.Application.EfAnalysisService` constructed as in `EfAnalysisAdvancedTests.CreateService()`:
    `new EfAnalysisService(new AnalyzeContextUseCase(analyzer), new DiscoverContextsUseCase(analyzer, fs), new AnalyzeSnapshotUseCase(analyzer), new DiscoverSnapshotsUseCase(analyzer, fs))` where
    `analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs))` and `fs = new PhysicalFileSystem()`.
  - `Task<EfModel> EfAnalysisService.AnalyzeContextAsync(string path, string? contextName = null)`.
  - `string MermaidErdRenderer.Render(EfModel model, DiagramOptions? options = null)`.
  - `new DiagramOptions(ShowTitle: true, WrapInMarkdownFence: false)`.
  - `ProjGraph.Tests.Shared.Helpers.TestPathHelper.GetSamplePath(string relative)` → absolute path under `samples/`.
- Produces (later tasks rely on these exact members):
  - `internal static class EfGoldenRunner` with:
    - `static string RenderContext(string samplePath, string? contextName)` — analyze + render, returns normalized (LF) output.
    - `static void Verify(string goldenName, string actual)` — assert-or-regenerate against `Golden/goldens/{goldenName}.mmd`.
  - `public static IEnumerable<object?[]> Cases` on `EfErdGoldenTests` — theory data `{ samplePath, contextName, goldenName }`.

- [ ] **Step 1: Write the failing test**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs`:

```csharp
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Lib.EntityFramework.Rendering;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework.Golden;

/// <summary>
/// Characterization tests: every EF sample context's rendered ERD is pinned to a committed
/// golden file. Set the environment variable UPDATE_EF_GOLDENS=1 to (re)generate the goldens
/// instead of asserting against them.
/// </summary>
[Trait("Category", "Golden")]
public sealed class EfErdGoldenTests
{
    public static IEnumerable<object?[]> Cases =>
    [
        [@"erd\simple-context\EntityFramework\MyDbContext.cs", "MyDbContext", "simple-context"]
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Erd_MatchesGolden(string sampleRelativePath, string? contextName, string goldenName)
    {
        var actual = EfGoldenRunner.RenderContext(TestPathHelper.GetSamplePath(sampleRelativePath), contextName);
        EfGoldenRunner.Verify(goldenName, actual);
    }
}

/// <summary>
/// Shared machinery for the EF golden tests: renders a context's ERD and compares (or regenerates)
/// the committed golden file.
/// </summary>
internal static class EfGoldenRunner
{
    private static readonly bool UpdateMode =
        Environment.GetEnvironmentVariable("UPDATE_EF_GOLDENS") == "1";

    public static string RenderContext(string samplePath, string? contextName)
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        var service = new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));

        var model = service.AnalyzeContextAsync(samplePath, contextName).GetAwaiter().GetResult();
        var rendered = new MermaidErdRenderer().Render(model, new DiagramOptions(true, false));
        return Normalize(rendered);
    }

    public static void Verify(string goldenName, string actual)
    {
        var goldenPath = Path.Combine(GoldenDirectory(), $"{goldenName}.mmd");

        if (UpdateMode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, actual);
            return;
        }

        File.Exists(goldenPath).Should().BeTrue(
            $"golden '{goldenName}.mmd' must exist; run with UPDATE_EF_GOLDENS=1 to generate it");
        var expected = Normalize(File.ReadAllText(goldenPath));
        actual.Should().Be(expected,
            $"rendered ERD must match golden '{goldenName}.mmd'; if this change is intended, " +
            "regenerate with UPDATE_EF_GOLDENS=1 and review the diff");
    }

    private static string Normalize(string text) => text.ReplaceLineEndings("\n").TrimEnd('\n');

    private static string GoldenDirectory()
    {
        // Golden files are copied next to the test assembly (see csproj content include).
        return Path.Combine(AppContext.BaseDirectory, "Golden", "goldens");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~EfErdGoldenTests"`
Expected: FAIL — the assertion `golden 'simple-context.mmd' must exist` fails (no golden yet).

- [ ] **Step 3: Wire the golden files into the test project output**

Modify `tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj` — add inside a `<Project>` `<ItemGroup>`:

```xml
  <ItemGroup>
    <None Include="Golden/goldens/**/*.mmd" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 4: Generate the baseline golden**

Run: `UPDATE_EF_GOLDENS=1 dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~EfErdGoldenTests"`
Expected: PASS (regenerate mode writes the golden into the `bin` output).

**Important:** In regenerate mode the file is written to `AppContext.BaseDirectory` (the `bin` output), not the source tree. Copy it back into source so it is committed:

```bash
cp tests/ProjGraph.Tests.Unit.EntityFramework/bin/Debug/net10.0/Golden/goldens/simple-context.mmd \
   tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/simple-context.mmd
```

Then open `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/simple-context.mmd` and eyeball it: it must be a valid `erDiagram` block matching what `projgraph erd samples/erd/simple-context/EntityFramework/MyDbContext.cs` prints today.

- [ ] **Step 5: Run test to verify it passes**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~EfErdGoldenTests"`
Expected: PASS (assert mode; committed golden matches rendered output).

Then confirm the whole suite and format are clean:
Run: `dtk dotnet test ProjGraph.slnx` → Expected: all pass.
Run: `dtk dotnet format ProjGraph.slnx --verify-no-changes` → Expected: nothing to format.

- [ ] **Step 6: Commit**

```bash
git add tests/ProjGraph.Tests.Unit.EntityFramework/Golden/ \
        tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj
git commit -m "test(ef): add golden-file ERD characterization harness (simple-context)"
```

---

### Task 2: Baseline the remaining sample context (complex-ecommerce)

**Files:**
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs` (extend `Cases`)
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/complex-ecommerce.mmd` (generated)

**Interfaces:**
- Consumes: `EfGoldenRunner.RenderContext`, `EfGoldenRunner.Verify`, `EfErdGoldenTests.Cases` (from Task 1).
- Produces: an additional golden case; no new API.

- [ ] **Step 1: Extend the theory with the failing case**

In `EfErdGoldenTests.cs`, replace the `Cases` body with:

```csharp
    public static IEnumerable<object?[]> Cases =>
    [
        [@"erd\simple-context\EntityFramework\MyDbContext.cs", "MyDbContext", "simple-context"],
        [@"erd\complex-ecommerce\Data\MyDbContext.cs", "MyDbContext", "complex-ecommerce"]
    ];
```

- [ ] **Step 2: Run to verify the new case fails**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~EfErdGoldenTests"`
Expected: the `complex-ecommerce` case FAILS ("golden 'complex-ecommerce.mmd' must exist"); `simple-context` still PASSES.

- [ ] **Step 3: Generate the baseline golden**

Run: `UPDATE_EF_GOLDENS=1 dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~EfErdGoldenTests"`
Then copy it into source:

```bash
cp tests/ProjGraph.Tests.Unit.EntityFramework/bin/Debug/net10.0/Golden/goldens/complex-ecommerce.mmd \
   tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/complex-ecommerce.mmd
```

Eyeball `complex-ecommerce.mmd`: it must contain the ecommerce entities and relationships as `projgraph erd` prints them today.

- [ ] **Step 4: Run to verify both cases pass**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~EfErdGoldenTests"`
Expected: PASS (2 cases).

- [ ] **Step 5: Commit**

```bash
git add tests/ProjGraph.Tests.Unit.EntityFramework/Golden/
git commit -m "test(ef): baseline complex-ecommerce ERD golden"
```

---

### Task 3: Fixture corpus exercising every Fluent-API construct

**Files:**
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/RelationshipsContext.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/OwnedAndJoinContext.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/PropertyConfigContext.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/ConfigClassContext.cs` (uses `IEntityTypeConfiguration<T>` + `ApplyConfiguration` — currently unsupported; golden captures today's output so a later slice's fix is a visible, reviewed golden change)
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/BaseContext.cs` (base-class `DbSet`s — currently unsupported; same rationale)
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs` (extend `Cases`)
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj` (exclude fixtures from compilation)
- Create: five generated goldens under `Golden/goldens/`

**Interfaces:**
- Consumes: `EfGoldenRunner`, `EfErdGoldenTests.Cases`.
- Produces: five fixture `.cs` files (analyzed as data, NOT compiled into the test assembly) + their goldens.

- [ ] **Step 1: Stop the fixtures from being compiled into the test assembly**

The fixtures are input *data* for the analyzer (real disk files it reads), not test code. Add to `ProjGraph.Tests.Unit.EntityFramework.csproj`:

```xml
  <ItemGroup>
    <Compile Remove="Golden/fixtures/**/*.cs" />
    <None Include="Golden/fixtures/**/*.cs" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 2: Write the fixture contexts**

Create `Golden/fixtures/RelationshipsContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
namespace Fixtures;

public class RelationshipsContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; } = null!;
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<Author> Authors { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>()
            .HasOne(p => p.Blog).WithMany(b => b.Posts)
            .HasForeignKey(p => p.BlogId).IsRequired(false);

        modelBuilder.Entity<Blog>()
            .HasOne(b => b.Owner).WithMany().HasForeignKey(b => b.OwnerId);

        modelBuilder.Entity<Post>().HasMany(p => p.Authors).WithMany(a => a.Posts);
    }
}

public class Blog { public int Id { get; set; } public int? OwnerId { get; set; } public Author Owner { get; set; } = null!; public List<Post> Posts { get; set; } = []; }
public class Post { public int Id { get; set; } public int? BlogId { get; set; } public Blog Blog { get; set; } = null!; public List<Author> Authors { get; set; } = []; }
public class Author { public int Id { get; set; } public List<Post> Posts { get; set; } = []; }
```

Create `Golden/fixtures/OwnedAndJoinContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
namespace Fixtures;

public class OwnedAndJoinContext : DbContext
{
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.OwnsOne(c => c.Address, a => a.Property(p => p.City).HasMaxLength(50));
            e.HasMany(c => c.Products).WithMany(p => p.Customers)
                .UsingEntity<Dictionary<string, object>>("CustomerProduct",
                    j => j.HasOne<Product>().WithMany().HasForeignKey("ProductId"),
                    j => j.HasOne<Customer>().WithMany().HasForeignKey("CustomerId"));
        });
    }
}

public class Customer { public int Id { get; set; } public Address Address { get; set; } = null!; public List<Product> Products { get; set; } = []; }
public class Address { public string City { get; set; } = ""; }
public class Product { public int Id { get; set; } public List<Customer> Customers { get; set; } = []; }
```

Create `Golden/fixtures/PropertyConfigContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
namespace Fixtures;

public class PropertyConfigContext : DbContext
{
    public DbSet<Account> Accounts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Name).IsRequired().HasMaxLength(200);
            e.Property(a => a.Balance).HasPrecision(18, 2).HasDefaultValue(0);
            e.Property(a => a.Code).HasColumnType("char(8)");
        });
}

public class Account { public int Id { get; set; } public string Name { get; set; } = ""; public decimal Balance { get; set; } public string Code { get; set; } = ""; }
```

Create `Golden/fixtures/ConfigClassContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fixtures;

public class ConfigClassContext : DbContext
{
    public DbSet<Widget> Widgets { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfiguration(new WidgetConfiguration());
}

public class WidgetConfiguration : IEntityTypeConfiguration<Widget>
{
    public void Configure(EntityTypeBuilder<Widget> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Name).IsRequired().HasMaxLength(120);
    }
}

public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
```

Create `Golden/fixtures/BaseContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
namespace Fixtures;

public abstract class BaseAppContext : DbContext
{
    public DbSet<Note> Notes { get; set; } = null!;
}

public class BaseContext : BaseAppContext
{
    public DbSet<Tag> Tags { get; set; } = null!;
}

public class Note { public int Id { get; set; } public string Text { get; set; } = ""; }
public class Tag { public int Id { get; set; } public string Label { get; set; } = ""; }
```

- [ ] **Step 3: Extend the theory with the fixture cases**

In `EfErdGoldenTests.cs`, replace `Cases` with:

```csharp
    public static IEnumerable<object?[]> Cases =>
    [
        [@"erd\simple-context\EntityFramework\MyDbContext.cs", "MyDbContext", "simple-context"],
        [@"erd\complex-ecommerce\Data\MyDbContext.cs", "MyDbContext", "complex-ecommerce"],
        [FixturePath("RelationshipsContext.cs"), "RelationshipsContext", "fixture-relationships"],
        [FixturePath("OwnedAndJoinContext.cs"), "OwnedAndJoinContext", "fixture-owned-join"],
        [FixturePath("PropertyConfigContext.cs"), "PropertyConfigContext", "fixture-property-config"],
        [FixturePath("ConfigClassContext.cs"), "ConfigClassContext", "fixture-config-class"],
        [FixturePath("BaseContext.cs"), "BaseContext", "fixture-base-dbset"]
    ];

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Golden", "fixtures", fileName);
```

Note: fixtures are addressed by their copied-to-output absolute path (they are data files), while samples use `TestPathHelper.GetSamplePath`. The theory passes the *already-absolute* fixture path; update `Erd_MatchesGolden` to only apply `GetSamplePath` to relative sample paths:

```csharp
    [Theory]
    [MemberData(nameof(Cases))]
    public void Erd_MatchesGolden(string contextPath, string? contextName, string goldenName)
    {
        var absolute = Path.IsPathFullyQualified(contextPath)
            ? contextPath
            : TestPathHelper.GetSamplePath(contextPath);
        var actual = EfGoldenRunner.RenderContext(absolute, contextName);
        EfGoldenRunner.Verify(goldenName, actual);
    }
```

- [ ] **Step 4: Run to verify the fixture cases fail**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~EfErdGoldenTests"`
Expected: the five fixture cases FAIL ("golden '...' must exist"); the two sample cases still PASS.

- [ ] **Step 5: Generate the fixture goldens and copy into source**

Run: `UPDATE_EF_GOLDENS=1 dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~EfErdGoldenTests"`

```bash
cp tests/ProjGraph.Tests.Unit.EntityFramework/bin/Debug/net10.0/Golden/goldens/fixture-*.mmd \
   tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/
```

Eyeball each `fixture-*.mmd`. Expect: `fixture-relationships`, `fixture-owned-join`, `fixture-property-config` capture correct current output; `fixture-config-class` and `fixture-base-dbset` capture the *current, incomplete* output (Widget/Tag config or entities missing) — this is intentional. Record the known-gap in the commit message (Mermaid has no comment syntax to annotate the goldens themselves).

- [ ] **Step 6: Run to verify all cases pass**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~EfErdGoldenTests"`
Expected: PASS (7 cases).

Then: `dtk dotnet test ProjGraph.slnx` → all pass; `dtk dotnet format ProjGraph.slnx --verify-no-changes` → clean.

- [ ] **Step 7: Commit**

```bash
git add tests/ProjGraph.Tests.Unit.EntityFramework/Golden/ \
        tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj
git commit -m "$(cat <<'MSG'
test(ef): add Fluent-API fixture corpus + goldens

Captures current ERD output for relationships, owned/join, and property
config fixtures, plus two gap fixtures (IEntityTypeConfiguration and
base-class DbSets) whose goldens intentionally record today's incomplete
output — later rewrite slices will update those goldens as the gaps close.
MSG
)"
```

---

## Notes for the reviewer

- This slice adds **only test assets** — no `src/` change. A reviewer should confirm every golden matches what `projgraph erd <fixture>` prints today, and that the two gap goldens are called out as known-incomplete.
- The goldens are the contract for Slices 1–6: each later slice may only change a golden with a reviewer-visible diff justified by a specific fixed finding.
- After this merges, the next plan is **Slice 1 (relationships walker)**, written once this baseline exists.

## Self-review

- **Spec coverage:** Implements Slice 0 exactly ("snapshot current ERD output for every `samples/erd/*` context plus a new fixture corpus into golden files ... cross-slice regression net; no production change"). ✔
- **Placeholder scan:** No TBD/TODO; all code shown in full; commands and expected output given. The two "gap" goldens are deliberately-current, not placeholders. ✔
- **Type consistency:** `EfGoldenRunner.RenderContext` / `.Verify`, `EfErdGoldenTests.Cases` / `Erd_MatchesGolden`, `FixturePath` used consistently across Tasks 1–3. `DiagramOptions(true, false)`, `AnalyzeContextAsync(path, contextName)`, and the `EfAnalysisService` construction match the production signatures confirmed against source. ✔
- **Scope:** One testable deliverable (the harness + baselines). Subsequent slices are separate plans, per the spec's decomposition. ✔
