# EF Rewrite — Slice 4: `IEntityTypeConfiguration<T>` support — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold the fluent configuration of `IEntityTypeConfiguration<T>` classes — reached from `OnModelCreating` via `modelBuilder.ApplyConfiguration(new XConfig())` and `modelBuilder.ApplyConfigurationsFromAssembly(...)` — into the EF model by walking each config class's `Configure(EntityTypeBuilder<T> builder)` body with the existing Roslyn walkers, closing a real feature gap.

**Architecture:** The three walkers (`FluentEntityWalker`, `FluentPropertyWalker`, `FluentRelationshipWalker`) resolve the owning entity from an `Entity<T>()` call in the chain. A config class's chains are rooted at the `EntityTypeBuilder<T>` parameter with no such call, so each walker gains an optional `string? ambientEntity = null` parameter used as the fallback owning entity when no `Entity<T>()` is found (the `OnModelCreating` path keeps passing the default `null` → byte-identical). A new `internal static EntityConfigurationWalker` scans `OnModelCreating` for `ApplyConfiguration`/`ApplyConfigurationsFromAssembly`, finds the referenced config classes **by syntax** (their `IEntityTypeConfiguration<T>` base list) across the compilation, materializes each target entity `T` if absent, and runs the three walkers on the `Configure` body with `ambientEntity = T`. `EntityFileDiscovery` is extended to pull separate `IEntityTypeConfiguration<T>` source files into the compilation so config classes that live in their own files are visible.

**Tech Stack:** C# / .NET 10, Roslyn (`Microsoft.CodeAnalysis.CSharp.Syntax`), xUnit v3, FluentAssertions, `RoslynTestHelper`, `EfErdGoldenTests` (Slice 0), `PhysicalFileSystem`.

## Global Constraints

- Target framework `net10.0`; build enforces `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true` — no warnings allowed.
- XML documentation is required on all public APIs. `EntityConfigurationWalker` is `internal`, but keep `<summary>` docs on it and every member for consistency with the sibling walkers.
- Use `dtk dotnet build` / `dtk dotnet test` / `dtk dotnet format ProjGraph.slnx --verify-no-changes` (the token-optimized DotnetTokenKiller wrapper) for all build/test/format commands.
- This is a **gap-closing** slice, so it deliberately changes output. **Exactly one existing golden changes** (`fixture-config-class.mmd`, gaining `max:120` on `Widget.Name`, Task 3) and **one new golden is added** (Task 4). The other six goldens (`simple-context`, `complex-ecommerce`, `fixture-relationships`, `fixture-owned-join`, `fixture-property-config`, `fixture-base-dbset`) must stay **byte-identical** — never regenerate them.
- Do NOT touch the snapshot path (`ModelSnapshotParser`, `AnalyzeSnapshotUseCase`, `AnalyzeSnapshotAsync`, `BuildSnapshotSyntaxTreesAsync`), `RelationshipConfigParser`, `PropertyConfigParser`, `FluentApiParsingUtilities`, `RelationshipAnalyzer`, or the regex patterns. `ApplyConstraintsFromMethod` remains the snapshot path (retired in Slice 6).
- Config-class detection is **syntax-based** (the `IEntityTypeConfiguration<T>` base list + the generic argument `T`), not semantic. `RoslynTestHelper.CreateCompilation` references only the BCL — not EF Core — so the interface symbol is unresolvable in unit tests; syntax matching keeps the walker reference-independent (the same syntax-first degradation the other walkers already rely on).
- A handful of tiny syntax helpers (`SimpleName`, `TypeName`, `LastSegment`) are duplicated across the walkers; consolidation stays deferred to Slice 6, matching the Slice-1/2/3 stance.

### Key behaviors to preserve / establish (verified against the current pipeline)

- **Ambient defaults to `null` on the context path.** `FluentApiConfigurationParser.ApplyFluentApiConstraints` calls the three walkers with no `ambientEntity` argument (default `null`). Their `Entity<T>()`-based resolution is unchanged, so the six parity goldens and every existing walker test stay green.
- **`ApplyConfiguration(new X())` applies only `X`.** Explicit application resolves the single named config class; other config classes present in the compilation are ignored. (The golden fixtures use this explicit form to avoid cross-fixture config bleed under the apply-all semantics of `ApplyConfigurationsFromAssembly` when fixtures share a scanned directory.)
- **`ApplyConfigurationsFromAssembly(...)` applies every `IEntityTypeConfiguration<T>` in the compilation.** The exact assembly argument is intentionally ignored (a single-project tool cannot reliably map a `typeof(X).Assembly` expression to a Roslyn assembly subset). This is exercised by orchestrator **unit tests** over an isolated single-source compilation (where "all in compilation" is exactly the intended set), not by a shared-directory golden fixture.
- **Config-only entities are materialized.** A `T` with no `DbSet<T>` is added to `entities` + `model.Entities` via `EntityAnalyzer.AnalyzeEntity` (resolved with `compilation.GetSymbolsWithName`, falling back to `new EfEntity { Name = T }`), mirroring `FluentEntityWalker.MaterializeEntity`.
- **Nested owned/join scopes still excluded.** The Slice-3 `NestedBuilderScopes` / `IsInsideUsingEntity` guards already scope out nested builder lambdas, so ambient never leaks into an `OwnsOne`/`UsingEntity` lambda inside a `Configure` body.

### Findings folded into this slice

None. Slice 4 is a feature-gap slice; the deferred §4.3 Lows (#12/#14/#16) live on the snapshot path or in the untouched `RelationshipAnalyzer` and are retired in Slice 6.

---

### Task 1: Ambient-entity parameter in the three walkers

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentPropertyWalkerTests.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentEntityWalkerTests.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentRelationshipWalkerTests.cs`

**Interfaces:**
- Consumes (existing, unchanged): the walkers' current private resolution helpers (`ResolveOwningEntity`, `ResolveSourceEntity`, `ApplyTableName`, `ApplyPropertyChain`, `ApplyKey`), `RoslynTestHelper.CreateCompilation`/`GetTypeSymbol`, `EntityAnalyzer.AnalyzeEntity`.
- Produces (Task 2 relies on these exact signatures):
  - `FluentEntityWalker.Apply(MethodDeclarationSyntax method, Dictionary<string, EfEntity> entities, EfModel model, Compilation compilation, string? ambientEntity = null)`
  - `FluentPropertyWalker.Apply(MethodDeclarationSyntax method, Dictionary<string, EfEntity> entities, Compilation compilation, string? ambientEntity = null)`
  - `FluentRelationshipWalker.Apply(MethodDeclarationSyntax method, Dictionary<string, EfEntity> entities, EfModel model, Compilation compilation, string? ambientEntity = null)`

The ambient tests drive each walker with a method whose chains are rooted at a bare `builder` identifier (no `Entity<T>()`) — exactly the shape of a config class's `Configure` body — reusing each test file's existing `Build(...)` helper (which locates the method named `OnModelCreating`; the walkers ignore the method name and simply scan descendant chains).

- [ ] **Step 1: Write the failing ambient tests**

Append to `FluentPropertyWalkerTests.cs` (before the closing brace):

```csharp
    [Fact]
    public void Apply_AmbientEntity_RoutesBareBuilderChainToAmbient()
    {
        // A config class's Configure(EntityTypeBuilder<Widget> builder) body: chains are rooted at the
        // bare `builder` parameter with no Entity<T>() call, so the owning entity is the ambient T.
        const string source = """
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic builder)
                {
                    builder.HasKey(w => w.Id);
                    builder.Property(w => w.Name).IsRequired().HasMaxLength(120);
                }
            }
            """;
        var (method, compilation, entities) = Build(source, "Widget");

        FluentPropertyWalker.Apply(method, entities, compilation, ambientEntity: "Widget");

        var name = Property(entities, "Widget", "Name");
        name.IsRequired.Should().BeTrue();
        name.MaxLength.Should().Be(120);
        Property(entities, "Widget", "Id").IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_NoAmbient_BareBuilderChainAppliesNothing()
    {
        const string source = """
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic builder)
                {
                    builder.Property(w => w.Name).HasMaxLength(120);
                }
            }
            """;
        var (method, compilation, entities) = Build(source, "Widget");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Widget", "Name").MaxLength.Should().BeNull();
    }
```

Append to `FluentEntityWalkerTests.cs` (before the closing brace):

```csharp
    [Fact]
    public void Apply_AmbientEntity_RoutesBareBuilderToTableToAmbient()
    {
        const string source = """
            public class Widget { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic builder)
                {
                    builder.ToTable("widgets");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget");

        FluentEntityWalker.Apply(method, entities, model, compilation, ambientEntity: "Widget");

        entities["Widget"].TableName.Should().Be("widgets");
        model.Entities.Single(e => e.Name == "Widget").TableName.Should().Be("widgets");
    }

    [Fact]
    public void Apply_NoAmbient_BareBuilderToTableSetsNothing()
    {
        const string source = """
            public class Widget { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic builder)
                {
                    builder.ToTable("widgets");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities["Widget"].TableName.Should().BeEmpty();
    }
```

Append to `FluentRelationshipWalkerTests.cs` (before the closing brace):

```csharp
    [Fact]
    public void Apply_AmbientEntity_RoutesBareBuilderRelationshipToAmbient()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = new(); }
            public class Post { public int Id { get; set; } public Blog Blog { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic builder)
                {
                    builder.HasMany(b => b.Posts).WithOne(p => p.Blog);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        FluentRelationshipWalker.Apply(method, entities, model, compilation, ambientEntity: "Blog");

        model.Relationships.Should().ContainSingle(r => r.SourceEntity == "Blog" && r.TargetEntity == "Post");
    }

    [Fact]
    public void Apply_NoAmbient_BareBuilderRelationshipAddsNothing()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = new(); }
            public class Post { public int Id { get; set; } public Blog Blog { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic builder)
                {
                    builder.HasMany(b => b.Posts).WithOne(p => p.Blog);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        model.Relationships.Should().BeEmpty();
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~FluentPropertyWalkerTests|FullyQualifiedName~FluentEntityWalkerTests|FullyQualifiedName~FluentRelationshipWalkerTests"`
Expected: FAIL to compile — `Apply` has no `ambientEntity` parameter.

- [ ] **Step 3: Add `ambientEntity` to `FluentPropertyWalker`**

In `FluentPropertyWalker.cs`, replace the `Apply` method (the signature through its closing brace) with:

```csharp
    public static void Apply(
        MethodDeclarationSyntax method,
        Dictionary<string, EfEntity> entities,
        Compilation compilation,
        string? ambientEntity = null)
    {
        foreach (var propertyRoot in FindConfigRoots(method, EfAnalysisConstants.EfMethods.Property))
        {
            ApplyPropertyChain(propertyRoot, entities, compilation, ambientEntity);
        }

        foreach (var keyRoot in FindConfigRoots(method, EfAnalysisConstants.EfMethods.HasKey))
        {
            ApplyKey(keyRoot, entities, ambientEntity);
        }
    }
```

Change `ApplyPropertyChain`'s signature and its resolution call. Replace:

```csharp
    private static void ApplyPropertyChain(
        InvocationExpressionSyntax propertyRoot,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        var entityName = ResolveOwningEntity(propertyRoot);
```

with:

```csharp
    private static void ApplyPropertyChain(
        InvocationExpressionSyntax propertyRoot,
        Dictionary<string, EfEntity> entities,
        Compilation compilation,
        string? ambientEntity)
    {
        var entityName = ResolveOwningEntity(propertyRoot, ambientEntity);
```

Change `ApplyKey`'s signature and its resolution call. Replace:

```csharp
    private static void ApplyKey(InvocationExpressionSyntax keyRoot, Dictionary<string, EfEntity> entities)
    {
        var entityName = ResolveOwningEntity(keyRoot);
```

with:

```csharp
    private static void ApplyKey(
        InvocationExpressionSyntax keyRoot,
        Dictionary<string, EfEntity> entities,
        string? ambientEntity)
    {
        var entityName = ResolveOwningEntity(keyRoot, ambientEntity);
```

Change `ResolveOwningEntity` to accept and fall back to the ambient entity. Replace its signature line:

```csharp
    private static string? ResolveOwningEntity(InvocationExpressionSyntax configInvocation)
    {
```

with:

```csharp
    private static string? ResolveOwningEntity(InvocationExpressionSyntax configInvocation, string? ambientEntity)
    {
```

and replace its final `return` statement:

```csharp
        return enclosingEntity is null ? null : EntityNameFromInvocation(enclosingEntity);
```

with:

```csharp
        return enclosingEntity is null ? ambientEntity : EntityNameFromInvocation(enclosingEntity);
```

- [ ] **Step 4: Add `ambientEntity` to `FluentEntityWalker`**

In `FluentEntityWalker.cs`, replace the `Apply` method with (add the parameter; thread it into `ApplyTableName`; `MaterializeEntity` is unchanged since a config body has no `Entity<T>()` root):

```csharp
    public static void Apply(
        MethodDeclarationSyntax method,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        string? ambientEntity = null)
    {
        foreach (var entityInvocation in FindConfigRoots(method, EfAnalysisConstants.EfMethods.Entity))
        {
            MaterializeEntity(entityInvocation, entities, model, compilation);
        }

        foreach (var toTableInvocation in FindConfigRoots(method, EfAnalysisConstants.EfMethods.ToTable))
        {
            ApplyTableName(toTableInvocation, entities, model, ambientEntity);
        }
    }
```

Change `ApplyTableName`'s signature and its resolution call. Replace:

```csharp
    private static void ApplyTableName(
        InvocationExpressionSyntax toTableInvocation,
        Dictionary<string, EfEntity> entities,
        EfModel model)
    {
        var tableName = TableNameArgument(toTableInvocation);
        if (tableName is null)
        {
            return;
        }

        var entityName = ResolveOwningEntity(toTableInvocation);
```

with:

```csharp
    private static void ApplyTableName(
        InvocationExpressionSyntax toTableInvocation,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        string? ambientEntity)
    {
        var tableName = TableNameArgument(toTableInvocation);
        if (tableName is null)
        {
            return;
        }

        var entityName = ResolveOwningEntity(toTableInvocation, ambientEntity);
```

Change `ResolveOwningEntity`'s signature line:

```csharp
    private static string? ResolveOwningEntity(InvocationExpressionSyntax configInvocation)
    {
```

to:

```csharp
    private static string? ResolveOwningEntity(InvocationExpressionSyntax configInvocation, string? ambientEntity)
    {
```

and replace its final `return`:

```csharp
        return enclosingEntity is null ? null : EntityNameFromInvocation(enclosingEntity);
```

with:

```csharp
        return enclosingEntity is null ? ambientEntity : EntityNameFromInvocation(enclosingEntity);
```

- [ ] **Step 5: Add `ambientEntity` to `FluentRelationshipWalker`**

In `FluentRelationshipWalker.cs`, replace the `Apply` signature block and the `ResolveSourceEntity` call. Replace:

```csharp
    public static void Apply(
        MethodDeclarationSyntax method,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        var existingKeys = model.Relationships.Select(r => r.GenerateKey()).ToHashSet();

        foreach (var hasInvocation in FindRelationshipRoots(method))
        {
            var chain = new FluentChain(hasInvocation);

            var sourceEntity = ResolveSourceEntity(chain);
```

with:

```csharp
    public static void Apply(
        MethodDeclarationSyntax method,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        string? ambientEntity = null)
    {
        var existingKeys = model.Relationships.Select(r => r.GenerateKey()).ToHashSet();

        foreach (var hasInvocation in FindRelationshipRoots(method))
        {
            var chain = new FluentChain(hasInvocation);

            var sourceEntity = ResolveSourceEntity(chain, ambientEntity);
```

Change `ResolveSourceEntity`'s signature line:

```csharp
    private static string? ResolveSourceEntity(FluentChain chain)
    {
```

to:

```csharp
    private static string? ResolveSourceEntity(FluentChain chain, string? ambientEntity)
    {
```

and replace its final `return`:

```csharp
        return enclosingEntity is null ? null : EntityNameFromInvocation(enclosingEntity);
```

with:

```csharp
        return enclosingEntity is null ? ambientEntity : EntityNameFromInvocation(enclosingEntity);
```

- [ ] **Step 6: Run the walker tests to verify they pass**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~FluentPropertyWalkerTests|FullyQualifiedName~FluentEntityWalkerTests|FullyQualifiedName~FluentRelationshipWalkerTests"`
Expected: PASS — all prior walker tests plus the 6 new ambient tests.

- [ ] **Step 7: Build, full suite, format-check**

Run: `dtk dotnet build ProjGraph.slnx`, then `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework`, then `dtk dotnet format ProjGraph.slnx --verify-no-changes`.
Expected: build 0 warnings/errors; EF unit suite green (the `OnModelCreating` path still passes `ambientEntity=null`, so goldens are unchanged); format reports no changes.

- [ ] **Step 8: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs tests/ProjGraph.Tests.Unit.EntityFramework/FluentPropertyWalkerTests.cs tests/ProjGraph.Tests.Unit.EntityFramework/FluentEntityWalkerTests.cs tests/ProjGraph.Tests.Unit.EntityFramework/FluentRelationshipWalkerTests.cs
git commit -m "feat(ef): Slice 4 Task 1 — ambient-entity parameter on the fluent walkers"
```

---

### Task 2: `EntityConfigurationWalker` orchestrator

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/Constants/EfAnalysisConstants.cs`
- Create: `src/ProjGraph.Lib.EntityFramework/Infrastructure/EntityConfigurationWalker.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/EntityConfigurationWalkerTests.cs`

**Interfaces:**
- Consumes (Task 1): `FluentEntityWalker.Apply(..., string? ambientEntity)`, `FluentPropertyWalker.Apply(..., string? ambientEntity)`, `FluentRelationshipWalker.Apply(..., string? ambientEntity)`. Also `EntityAnalyzer.AnalyzeEntity(INamedTypeSymbol)`, `compilation.GetSymbolsWithName`, `compilation.SyntaxTrees`.
- Produces (Task 3 relies on): `internal static class EntityConfigurationWalker` with `public static void Apply(MethodDeclarationSyntax method, Dictionary<string, EfEntity> entities, EfModel model, Compilation compilation)`.

- [ ] **Step 1: Add the config method/interface name constants**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/Constants/EfAnalysisConstants.cs`, inside the `EfMethods` static class, add these constants immediately after the existing `BuildModel` line (`public const string BuildModel = "BuildModel";`):

```csharp

        /// <summary>
        /// Entity type configuration (<c>IEntityTypeConfiguration&lt;T&gt;</c>) methods and names.
        /// </summary>
        public const string ApplyConfiguration = "ApplyConfiguration";
        public const string ApplyConfigurationsFromAssembly = "ApplyConfigurationsFromAssembly";
        public const string Configure = "Configure";
        public const string EntityTypeConfigurationInterface = "IEntityTypeConfiguration";
```

- [ ] **Step 2: Write the failing orchestrator tests**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/EntityConfigurationWalkerTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Unit tests for <see cref="EntityConfigurationWalker"/>: discovery of <c>IEntityTypeConfiguration&lt;T&gt;</c>
/// classes referenced from <c>OnModelCreating</c> and folding of their <c>Configure</c> bodies into the model
/// via the ambient-entity walkers.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class EntityConfigurationWalkerTests
{
    /// <summary>
    /// Compiles <paramref name="source"/>, locates its OnModelCreating method, and seeds the entities
    /// dictionary/model from the named DbSet entity classes so the orchestrator can be driven in isolation.
    /// </summary>
    /// <param name="source">The C# source to compile.</param>
    /// <param name="seededEntityNames">Entity class names to pre-analyze (simulating DbSet discovery).</param>
    private static (MethodDeclarationSyntax Method, Compilation Compilation, Dictionary<string, EfEntity> Entities, EfModel Model)
        Build(string source, params string[] seededEntityNames)
    {
        var compilation = RoslynTestHelper.CreateCompilation(source);
        var method = compilation.SyntaxTrees[0].GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "OnModelCreating");

        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel();
        foreach (var name in seededEntityNames)
        {
            var symbol = RoslynTestHelper.GetTypeSymbol(compilation, name)!;
            var entity = EntityAnalyzer.AnalyzeEntity(symbol);
            entities[name] = entity;
            model.Entities.Add(entity);
        }

        return (method, compilation, entities, model);
    }

    [Fact]
    public void Apply_ApplyConfiguration_FoldsPropertyConfigIntoSeededEntity()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class WidgetConfiguration : IEntityTypeConfiguration<Widget>
            {
                public void Configure(EntityTypeBuilder<Widget> builder)
                {
                    builder.HasKey(w => w.Id);
                    builder.Property(w => w.Name).IsRequired().HasMaxLength(120);
                }
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfiguration(new WidgetConfiguration());
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        var name = entities["Widget"].Properties.Single(p => p.Name == "Name");
        name.IsRequired.Should().BeTrue();
        name.MaxLength.Should().Be(120);
        entities["Widget"].Properties.Single(p => p.Name == "Id").IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_ApplyConfiguration_ConfiguresRelationship()
    {
        const string source = """
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = new(); }
            public class Post { public int Id { get; set; } public Blog Blog { get; set; } = null!; }
            public class BlogConfiguration : IEntityTypeConfiguration<Blog>
            {
                public void Configure(EntityTypeBuilder<Blog> builder)
                    => builder.HasMany(b => b.Posts).WithOne(p => p.Blog);
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfiguration(new BlogConfiguration());
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        model.Relationships.Should().ContainSingle(r => r.SourceEntity == "Blog" && r.TargetEntity == "Post");
    }

    [Fact]
    public void Apply_ConfigOnlyEntity_MaterializedFromConfiguration()
    {
        // Gadget has no DbSet and is not seeded; its configuration alone must materialize it.
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Gadget { public int Id { get; set; } public string Label { get; set; } = ""; }
            public class GadgetConfiguration : IEntityTypeConfiguration<Gadget>
            {
                public void Configure(EntityTypeBuilder<Gadget> builder)
                {
                    builder.HasKey(g => g.Id);
                    builder.Property(g => g.Label).HasMaxLength(40);
                }
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfiguration(new GadgetConfiguration());
            }
            """;
        var (method, compilation, entities, model) = Build(source);

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities.Should().ContainKey("Gadget");
        model.Entities.Should().ContainSingle(e => e.Name == "Gadget");
        entities["Gadget"].Properties.Single(p => p.Name == "Label").MaxLength.Should().Be(40);
    }

    [Fact]
    public void Apply_ApplyConfigurationsFromAssembly_AppliesAllConfigClasses()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Gadget { public int Id { get; set; } public string Label { get; set; } = ""; }
            public class WidgetConfiguration : IEntityTypeConfiguration<Widget>
            {
                public void Configure(EntityTypeBuilder<Widget> builder)
                    => builder.Property(w => w.Name).HasMaxLength(120);
            }
            public class GadgetConfiguration : IEntityTypeConfiguration<Gadget>
            {
                public void Configure(EntityTypeBuilder<Gadget> builder)
                    => builder.Property(g => g.Label).HasMaxLength(40);
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfigurationsFromAssembly(typeof(Ctx).Assembly);
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget", "Gadget");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities["Widget"].Properties.Single(p => p.Name == "Name").MaxLength.Should().Be(120);
        entities["Gadget"].Properties.Single(p => p.Name == "Label").MaxLength.Should().Be(40);
    }

    [Fact]
    public void Apply_ExplicitConfiguration_DoesNotApplyOtherConfigClasses()
    {
        // Only WidgetConfiguration is applied; GadgetConfiguration is present but not referenced.
        const string source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Gadget { public int Id { get; set; } public string Label { get; set; } = ""; }
            public class WidgetConfiguration : IEntityTypeConfiguration<Widget>
            {
                public void Configure(EntityTypeBuilder<Widget> builder)
                    => builder.Property(w => w.Name).HasMaxLength(120);
            }
            public class GadgetConfiguration : IEntityTypeConfiguration<Gadget>
            {
                public void Configure(EntityTypeBuilder<Gadget> builder)
                    => builder.Property(g => g.Label).HasMaxLength(40);
            }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.ApplyConfiguration(new WidgetConfiguration());
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget", "Gadget");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities["Widget"].Properties.Single(p => p.Name == "Name").MaxLength.Should().Be(120);
        entities["Gadget"].Properties.Single(p => p.Name == "Label").MaxLength.Should().BeNull();
    }

    [Fact]
    public void Apply_NoConfiguration_IsNoOp()
    {
        const string source = """
            public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity("Widget");
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Widget");

        EntityConfigurationWalker.Apply(method, entities, model, compilation);

        entities["Widget"].Properties.Single(p => p.Name == "Name").MaxLength.Should().BeNull();
        model.Entities.Should().ContainSingle(e => e.Name == "Widget");
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~EntityConfigurationWalkerTests"`
Expected: FAIL to compile — `EntityConfigurationWalker` does not exist yet.

- [ ] **Step 4: Create `EntityConfigurationWalker`**

Create `src/ProjGraph.Lib.EntityFramework/Infrastructure/EntityConfigurationWalker.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Discovers <c>IEntityTypeConfiguration&lt;T&gt;</c> classes referenced from a DbContext's
/// <c>OnModelCreating</c> method — via <c>modelBuilder.ApplyConfiguration(new XConfig())</c> or
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(...)</c> — and folds each class's
/// <c>Configure(EntityTypeBuilder&lt;T&gt;)</c> body into the model by running the fluent walkers with
/// <c>T</c> as their ambient entity. Config classes are matched by syntax (their
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> base list), so no EF Core reference is required.
/// </summary>
internal static class EntityConfigurationWalker
{
    /// <summary>A config class resolved from the compilation: its type name, target entity, and Configure body.</summary>
    /// <param name="ClassName">The config class's simple name.</param>
    /// <param name="EntityName">The configured entity type <c>T</c>.</param>
    /// <param name="Configure">The <c>Configure(EntityTypeBuilder&lt;T&gt;)</c> method declaration.</param>
    private readonly record struct ConfigClass(string ClassName, string EntityName, MethodDeclarationSyntax Configure);

    /// <summary>
    /// Applies every <c>IEntityTypeConfiguration&lt;T&gt;</c> class referenced from <paramref name="method"/>.
    /// </summary>
    /// <param name="method">The <c>OnModelCreating</c> method declaration.</param>
    /// <param name="entities">Entities already discovered; augmented with materialized config-only entities.</param>
    /// <param name="model">The model whose entities/relationships are populated.</param>
    /// <param name="compilation">The compilation whose syntax trees are scanned for config classes.</param>
    public static void Apply(
        MethodDeclarationSyntax method,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        var explicitNames = CollectExplicitConfigNames(method);
        var applyAll = HasApplyFromAssembly(method);
        if (explicitNames.Count == 0 && !applyAll)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var configClass in FindConfigClasses(compilation))
        {
            if (!applyAll && !explicitNames.Contains(configClass.ClassName))
            {
                continue;
            }

            if (!seen.Add(configClass.ClassName))
            {
                continue;
            }

            MaterializeEntity(configClass.EntityName, entities, model, compilation);

            FluentEntityWalker.Apply(configClass.Configure, entities, model, compilation, configClass.EntityName);
            FluentPropertyWalker.Apply(configClass.Configure, entities, compilation, configClass.EntityName);
            FluentRelationshipWalker.Apply(configClass.Configure, entities, model, compilation, configClass.EntityName);
        }
    }

    /// <summary>Collects the config-class type names from every <c>ApplyConfiguration(new X())</c> call in the method.</summary>
    /// <param name="method">The method to scan.</param>
    private static HashSet<string> CollectExplicitConfigNames(MethodDeclarationSyntax method)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax ma
                || SimpleName(ma.Name) != EfAnalysisConstants.EfMethods.ApplyConfiguration)
            {
                continue;
            }

            if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is ObjectCreationExpressionSyntax creation)
            {
                names.Add(TypeName(creation.Type));
            }
        }

        return names;
    }

    /// <summary>Returns whether the method contains any <c>ApplyConfigurationsFromAssembly(...)</c> call.</summary>
    /// <param name="method">The method to scan.</param>
    private static bool HasApplyFromAssembly(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax ma
                               && SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.ApplyConfigurationsFromAssembly);
    }

    /// <summary>Enumerates every class in the compilation that implements <c>IEntityTypeConfiguration&lt;T&gt;</c> and has a <c>Configure</c> method.</summary>
    /// <param name="compilation">The compilation whose syntax trees are scanned.</param>
    private static IEnumerable<ConfigClass> FindConfigClasses(Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (AsConfigClass(declaration) is { } configClass)
                {
                    yield return configClass;
                }
            }
        }
    }

    /// <summary>Interprets a class declaration as a config class, or returns <see langword="null"/> if it is not one.</summary>
    /// <param name="declaration">The class declaration.</param>
    private static ConfigClass? AsConfigClass(ClassDeclarationSyntax declaration)
    {
        var configInterface = declaration.BaseList?.Types
            .Select(baseType => baseType.Type)
            .OfType<GenericNameSyntax>()
            .FirstOrDefault(generic =>
                generic.Identifier.Text == EfAnalysisConstants.EfMethods.EntityTypeConfigurationInterface
                && generic.TypeArgumentList.Arguments.Count == 1);

        if (configInterface is null)
        {
            return null;
        }

        var configure = declaration.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(member => member.Identifier.Text == EfAnalysisConstants.EfMethods.Configure);

        if (configure is null)
        {
            return null;
        }

        var entityName = TypeName(configInterface.TypeArgumentList.Arguments[0]);
        return new ConfigClass(declaration.Identifier.Text, entityName, configure);
    }

    /// <summary>
    /// Materializes the entity named <paramref name="entityName"/> when it is not already known, resolving its
    /// symbol (or falling back to a bare entity) and adding it to both the entities dictionary and the model.
    /// Mirrors <see cref="FluentEntityWalker"/>'s materialization.
    /// </summary>
    /// <param name="entityName">The configured entity type name.</param>
    /// <param name="entities">The known entities, augmented in place.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is augmented.</param>
    /// <param name="compilation">The compilation for symbol resolution.</param>
    private static void MaterializeEntity(
        string entityName,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        if (string.IsNullOrEmpty(entityName) || entities.ContainsKey(entityName))
        {
            return;
        }

        var symbol = compilation.GetSymbolsWithName(entityName, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault();

        var entity = symbol is not null
            ? EntityAnalyzer.AnalyzeEntity(symbol)
            : new EfEntity { Name = entityName };

        entities[entityName] = entity;
        if (model.Entities.All(e => e.Name != entityName))
        {
            model.Entities.Add(entity);
        }
    }

    /// <summary>Returns the simple identifier of a name syntax (drops any generic type arguments).</summary>
    /// <param name="name">The name syntax.</param>
    private static string SimpleName(SimpleNameSyntax name) => name.Identifier.Text;

    /// <summary>
    /// Returns the name of a type syntax: the bare identifier for a simple name, otherwise the last dotted
    /// segment of its text (namespace qualification stripped; any generic argument list is retained).
    /// </summary>
    /// <param name="type">The type syntax.</param>
    private static string TypeName(TypeSyntax type)
    {
        return type is IdentifierNameSyntax identifier
            ? identifier.Identifier.Text
            : LastSegment(type.ToString());
    }

    /// <summary>Returns the substring after the last <c>.</c>, or the whole string when there is none.</summary>
    /// <param name="value">The dotted name.</param>
    private static string LastSegment(string value)
        => value.Contains('.', StringComparison.Ordinal) ? value.Split('.')[^1] : value;
}
```

- [ ] **Step 5: Run the orchestrator tests to verify they pass**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~EntityConfigurationWalkerTests"`
Expected: PASS (6 tests).

- [ ] **Step 6: Build and format-check**

Run: `dtk dotnet build ProjGraph.slnx` then `dtk dotnet format ProjGraph.slnx --verify-no-changes`
Expected: build 0 warnings/errors; format reports no changes.

- [ ] **Step 7: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/Constants/EfAnalysisConstants.cs src/ProjGraph.Lib.EntityFramework/Infrastructure/EntityConfigurationWalker.cs tests/ProjGraph.Tests.Unit.EntityFramework/EntityConfigurationWalkerTests.cs
git commit -m "feat(ef): Slice 4 Task 2 — EntityConfigurationWalker resolves and folds config classes"
```

---

### Task 3: Wire the orchestrator into the context path + update the single-file golden

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-config-class.mmd`

**Interfaces:**
- Consumes: `EntityConfigurationWalker.Apply(MethodDeclarationSyntax, Dictionary<string, EfEntity>, EfModel, Compilation)` (Task 2).
- Produces: `ApplyFluentApiConstraints` runs the config-class orchestrator after the three `OnModelCreating` walkers.

This task delivers the end-to-end win for a config class **co-located with the context** (the pre-staged `ConfigClassContext.cs` fixture, whose `WidgetConfiguration` is in the same file), so no file discovery is needed yet.

- [ ] **Step 1: Run the config-class golden to confirm the current (pre-fix) output**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~EfErdGoldenTests"`
Expected: PASS — all 7 goldens still match (the orchestrator is not yet wired). This confirms the baseline before the intended change.

- [ ] **Step 2: Wire the orchestrator into `ApplyFluentApiConstraints`**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs`, replace the body of `ApplyFluentApiConstraints` after the `FindOnModelCreatingMethod` guard (the three walker calls at lines 37-43) with:

```csharp
        // Context path: every concern now flows through the Roslyn syntax walkers. FluentEntityWalker
        // materializes fluent-only entities and applies ToTable; FluentPropertyWalker derives property
        // config + primary keys; FluentRelationshipWalker derives relationships + foreign keys. The regex
        // ApplyConstraintsFromMethod path is used only by the snapshot path now (retired in Slice 6).
        FluentEntityWalker.Apply(methodSyntax, entities, model, compilation);
        FluentPropertyWalker.Apply(methodSyntax, entities, compilation);
        FluentRelationshipWalker.Apply(methodSyntax, entities, model, compilation);

        // Fold IEntityTypeConfiguration<T> classes referenced via ApplyConfiguration /
        // ApplyConfigurationsFromAssembly by walking each Configure(EntityTypeBuilder<T>) body with T as
        // the ambient entity (Slice 4).
        EntityConfigurationWalker.Apply(methodSyntax, entities, model, compilation);
```

- [ ] **Step 3: Regenerate the single golden and review the diff**

Run: `UPDATE_EF_GOLDENS=1 dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~EfErdGoldenTests"`
Then: `git diff tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens`
Expected: **exactly one** file changed — `fixture-config-class.mmd` — with the single line `    string Name "required"` becoming `    string Name "required, max:120"`. No other golden changes. If any other golden differs, STOP and diagnose (a non-config fixture must not change; the orchestrator is a no-op when no `ApplyConfiguration*` call is present).

After confirming, verify the committed `fixture-config-class.mmd` reads:

```
---
title: ConfigClassContext
---
erDiagram
  Widget {
    int Id PK
    string Name "required, max:120"
  }
```

- [ ] **Step 4: Assert against the updated golden**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~EfErdGoldenTests"`
Expected: PASS — all 7 goldens (with the updated `fixture-config-class.mmd`) match.

- [ ] **Step 5: Full solution suite + format-check**

Run: `dtk dotnet test ProjGraph.slnx` then `dtk dotnet format ProjGraph.slnx --verify-no-changes`
Expected: full suite green (snapshot path untouched); format reports no changes.

- [ ] **Step 6: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-config-class.mmd
git commit -m "feat(ef): Slice 4 Task 3 — fold co-located IEntityTypeConfiguration classes (golden: +max:120)"
```

---

### Task 4: Separate-file config discovery + multi-file golden

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/EntityFileDiscovery.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/EfModelAnalyzer.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/Constants/EfAnalysisConstants.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/EntityFileDiscoveryTests.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/SeparateConfigContext.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/GadgetConfiguration.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-separate-config.mmd`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs`

**Interfaces:**
- Consumes: `IFileSystem.EnumerateFiles`/`ReadAllTextAsync`/`GetFullPath` (already injected into `EntityFileDiscovery`), `EfAnalysisConstants.FilePatterns.CSharpFiles`, `DirectoryFilters.ShouldSkipDirectory`.
- Produces: `EntityFileDiscovery.DiscoverConfigurationFilesAsync(IReadOnlyList<string> searchDirectories, string contextFilePath) : Task<Dictionary<string, string>>` (config-class name → file path), merged into the context compilation by `EfModelAnalyzer.BuildSyntaxTreesAsync`.

- [ ] **Step 1: Write the failing discovery test**

Append to `tests/ProjGraph.Tests.Unit.EntityFramework/EntityFileDiscoveryTests.cs` (before the closing brace):

```csharp
    [Fact]
    public async Task DiscoverConfigurationFilesAsync_WithSeparateConfigFile_ShouldFindIt()
    {
        const string configCode = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class GadgetConfiguration : IEntityTypeConfiguration<Gadget>
            {
                public void Configure(EntityTypeBuilder<Gadget> builder) { }
            }
            """;
        var configPath = Path.Combine(_tempDir, "GadgetConfiguration.cs");
        await File.WriteAllTextAsync(configPath, configCode);

        var contextFilePath = Path.Combine(_tempDir, "MyContext.cs");
        await File.WriteAllTextAsync(contextFilePath, "public class MyContext { }");

        var result = await _sut.DiscoverConfigurationFilesAsync(new List<string> { _tempDir }, contextFilePath);

        result.Should().ContainKey("GadgetConfiguration");
        result["GadgetConfiguration"].Should().Contain("GadgetConfiguration.cs");
    }

    [Fact]
    public async Task DiscoverConfigurationFilesAsync_ContextFileItself_ShouldBeExcluded()
    {
        const string contextCode = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class InlineConfiguration : IEntityTypeConfiguration<Gadget>
            {
                public void Configure(EntityTypeBuilder<Gadget> builder) { }
            }
            """;
        var contextFilePath = Path.Combine(_tempDir, "MyContext.cs");
        await File.WriteAllTextAsync(contextFilePath, contextCode);

        var result = await _sut.DiscoverConfigurationFilesAsync(new List<string> { _tempDir }, contextFilePath);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverConfigurationFilesAsync_NoConfigClasses_ShouldReturnEmpty()
    {
        var entityPath = Path.Combine(_tempDir, "Gadget.cs");
        await File.WriteAllTextAsync(entityPath, "public class Gadget { public int Id { get; set; } }");
        var contextFilePath = Path.Combine(_tempDir, "MyContext.cs");
        await File.WriteAllTextAsync(contextFilePath, "public class MyContext { }");

        var result = await _sut.DiscoverConfigurationFilesAsync(new List<string> { _tempDir }, contextFilePath);

        result.Should().BeEmpty();
    }
```

- [ ] **Step 2: Run the discovery test to verify it fails**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~DiscoverConfigurationFilesAsync"`
Expected: FAIL to compile — `DiscoverConfigurationFilesAsync` does not exist.

- [ ] **Step 3: Add the config-file discovery to `EntityFileDiscovery`**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/EntityFileDiscovery.cs`, add these members (place `DiscoverConfigurationFilesAsync` next to `DiscoverEntityFilesAsync`, and the two private helpers next to `SearchDirectoryRecursiveAsync`/`ProcessSourceFileAsync`):

```csharp
    /// <summary>
    /// Discovers source files declaring an <c>IEntityTypeConfiguration&lt;T&gt;</c> class within the given
    /// search directories, so config classes that live in their own files are added to the compilation. The
    /// context file is excluded (its declarations are already in the primary syntax tree).
    /// </summary>
    /// <param name="searchDirectories">The directories to search recursively.</param>
    /// <param name="contextFilePath">The DbContext file path to exclude.</param>
    /// <returns>A dictionary of config-class name to file path.</returns>
    public async Task<Dictionary<string, string>> DiscoverConfigurationFilesAsync(
        IReadOnlyList<string> searchDirectories,
        string contextFilePath)
    {
        var configFiles = new Dictionary<string, string>();
        var normalizedContextPath = fileSystem.GetFullPath(contextFilePath);

        foreach (var searchDir in searchDirectories.Where(fileSystem.DirectoryExists))
        {
            await SearchDirectoryForConfigurationsAsync(searchDir, normalizedContextPath, configFiles);
        }

        return configFiles;
    }

    /// <summary>
    /// Recursively searches a directory for source files declaring an <c>IEntityTypeConfiguration&lt;T&gt;</c>
    /// class, skipping the context file and build-artifact directories.
    /// </summary>
    /// <param name="currentDir">The directory to search.</param>
    /// <param name="normalizedContextPath">The normalized context file path to exclude.</param>
    /// <param name="configFiles">The dictionary of config-class name to file path, augmented in place.</param>
    private async Task SearchDirectoryForConfigurationsAsync(
        string currentDir,
        string normalizedContextPath,
        Dictionary<string, string> configFiles)
    {
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System
            };

            foreach (var csFile in fileSystem.EnumerateFiles(
                         currentDir,
                         EfAnalysisConstants.FilePatterns.CSharpFiles,
                         options))
            {
                var fullPath = fileSystem.GetFullPath(csFile);
                if (fullPath.Equals(normalizedContextPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await ProcessConfigurationFileAsync(fullPath, configFiles);
            }

            foreach (var subDir in fileSystem.EnumerateDirectories(currentDir, "*", options))
            {
                if (DirectoryFilters.ShouldSkipDirectory(subDir))
                {
                    continue;
                }

                await SearchDirectoryForConfigurationsAsync(subDir, normalizedContextPath, configFiles);
            }
        }
        catch (IOException)
        {
            // Ignore access errors for directories we can't read
        }
    }

    /// <summary>
    /// Adds a source file to <paramref name="configFiles"/> for each <c>IEntityTypeConfiguration&lt;T&gt;</c>
    /// class it declares. Skips files whose text does not mention the interface (a cheap pre-parse guard).
    /// </summary>
    /// <param name="filePath">The path of the source file to inspect.</param>
    /// <param name="configFiles">The dictionary of config-class name to file path, augmented in place.</param>
    private async Task ProcessConfigurationFileAsync(string filePath, Dictionary<string, string> configFiles)
    {
        var fileCode = await fileSystem.ReadAllTextAsync(filePath);
        if (!fileCode.Contains(EfAnalysisConstants.EfMethods.EntityTypeConfigurationInterface, StringComparison.Ordinal))
        {
            return;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(fileCode);
        var root = await syntaxTree.GetRootAsync();

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var implementsInterface = classDecl.BaseList?.Types
                .Select(baseType => baseType.Type)
                .OfType<GenericNameSyntax>()
                .Any(generic =>
                    generic.Identifier.Text == EfAnalysisConstants.EfMethods.EntityTypeConfigurationInterface
                    && generic.TypeArgumentList.Arguments.Count == 1) ?? false;

            if (implementsInterface)
            {
                configFiles.TryAdd(classDecl.Identifier.Text, filePath);
            }
        }
    }
```

- [ ] **Step 4: Run the discovery test to verify it passes**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~DiscoverConfigurationFilesAsync"`
Expected: PASS (3 tests).

- [ ] **Step 5: Wire config-file discovery into the context compilation**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/EfModelAnalyzer.cs`, in `BuildSyntaxTreesAsync`, add a config-file discovery pass before the final `CreateSyntaxTrees` call. Replace:

```csharp
        MergeFileDictionaries(entityFiles, baseClassFiles);

        return CreateSyntaxTrees(contextSyntaxTree, entityFiles);
```

with:

```csharp
        MergeFileDictionaries(entityFiles, baseClassFiles);

        // Slice 4: pull separate IEntityTypeConfiguration<T> files into the compilation so config classes
        // that live in their own files are visible to EntityConfigurationWalker.
        var configFiles = await entityFileDiscovery.DiscoverConfigurationFilesAsync(searchDirectories, contextPath);
        MergeFileDictionaries(entityFiles, configFiles);

        return CreateSyntaxTrees(contextSyntaxTree, entityFiles);
```

(Note: `searchDirectories` and `contextPath` are already in scope in `BuildSyntaxTreesAsync`.)

- [ ] **Step 6: Add the separate-file golden fixture**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/SeparateConfigContext.cs` — the context references its config class via explicit `ApplyConfiguration` (so other fixtures' config classes present in the scanned directory are not applied), and `Gadget` is a **config-only** entity (no `DbSet`) to prove materialization:

```csharp
using Microsoft.EntityFrameworkCore;
namespace Fixtures;

public class SeparateConfigContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfiguration(new GadgetConfiguration());
}

public class Gadget { public int Id { get; set; } public string Label { get; set; } = ""; }
```

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/GadgetConfiguration.cs` — a config class in a **separate file** (only discovered via `DiscoverConfigurationFilesAsync`):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fixtures;

public class GadgetConfiguration : IEntityTypeConfiguration<Gadget>
{
    public void Configure(EntityTypeBuilder<Gadget> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Label).IsRequired().HasMaxLength(64);
    }
}
```

- [ ] **Step 7: Register the golden case**

In `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs`, add a `Cases` entry after the `ConfigClassContext.cs` line:

```csharp
        [FixturePath("SeparateConfigContext.cs"), "SeparateConfigContext", "fixture-separate-config"],
```

- [ ] **Step 8: Generate the new golden and review it**

Run: `UPDATE_EF_GOLDENS=1 dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~EfErdGoldenTests"`
Then: `git status --porcelain tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens`
Expected: exactly one **new** file `fixture-separate-config.mmd` (and no modifications to the other seven goldens). Confirm it reads:

```
---
title: SeparateConfigContext
---
erDiagram
  Gadget {
    int Id PK
    string Label "required, max:64"
  }
```

If `Gadget` is absent or `Label` lacks `max:64`, the separate-file `GadgetConfiguration.cs` was not discovered or not applied — diagnose `DiscoverConfigurationFilesAsync` (is `fixtures/` in the search set? is the context file excluded correctly?) rather than editing the golden by hand.

- [ ] **Step 9: Assert against all goldens**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~EfErdGoldenTests"`
Expected: PASS — 8 golden cases (the 7 prior, unchanged except Task 3's `fixture-config-class`, plus the new `fixture-separate-config`).

- [ ] **Step 10: Full solution suite + format-check**

Run: `dtk dotnet test ProjGraph.slnx` then `dtk dotnet format ProjGraph.slnx --verify-no-changes`
Expected: full suite green; format reports no changes.

- [ ] **Step 11: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/EntityFileDiscovery.cs src/ProjGraph.Lib.EntityFramework/Infrastructure/EfModelAnalyzer.cs tests/ProjGraph.Tests.Unit.EntityFramework/EntityFileDiscoveryTests.cs tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/SeparateConfigContext.cs tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/GadgetConfiguration.cs tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-separate-config.mmd tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs
git commit -m "feat(ef): Slice 4 Task 4 — discover separate IEntityTypeConfiguration files (new golden)"
```

---

## Self-Review

**Spec coverage (design §Design):**
- Component 1 (ambient entity in the three walkers) → Task 1 adds `string? ambientEntity = null` to all three `Apply` methods and their resolution helpers; covered by 6 ambient tests + parity (existing goldens/tests, ambient defaults to `null`). ✅
- Component 2 (`EntityConfigurationWalker` orchestrator: `ApplyConfiguration`, `ApplyConfigurationsFromAssembly` → apply-all, resolve `T`, materialize config-only entities, run walkers with ambient) → Task 2, covered by 6 orchestrator unit tests including explicit-vs-all, config-only materialization, and no-op. ✅
- Component 3 (config-file discovery) → Task 4 `DiscoverConfigurationFilesAsync` + `BuildSyntaxTreesAsync` wiring, covered by 3 discovery unit tests + the separate-file golden. ✅
- Testing (deliberate `fixture-config-class` change; new multi-file golden with `ApplyConfigurationsFromAssembly`/separate file/relationship/config-only entity; six parity goldens byte-identical) → Task 3 updates `fixture-config-class.mmd` (+`max:120`); Task 4 adds `fixture-separate-config.mmd` (separate file + config-only materialization). **Divergence from spec, with rationale:** the golden fixtures use explicit `ApplyConfiguration` rather than `ApplyConfigurationsFromAssembly`, and `ApplyConfigurationsFromAssembly` is proven by orchestrator **unit tests** (isolated compilation) instead of a golden. Reason: under apply-all semantics, a `FromAssembly` golden fixture sharing the copied `fixtures/` directory would pull in sibling fixtures' config classes and cross-contaminate; explicit application is contamination-proof while still gating separate-file discovery end-to-end. The multi-file golden covers separate-file discovery, relationship-in-config (via the unit test) and config-only materialization. ✅
- Error handling / degradation (external/no-syntax config skipped; syntax-only interface match; no-config no-op) → orchestrator matches by syntax and only yields config classes with a `Configure` method; `Apply_NoConfiguration_IsNoOp` covers the no-op path. ✅
- Non-goals (snapshot path, `RelationshipConfigParser`/`PropertyConfigParser`/`RelationshipAnalyzer` untouched; no CLI/MCP surface change) → no task touches those. ✅

**Placeholder scan:** No TBD/TODO/"add error handling"/"similar to Task N" — every code step contains complete, compilable code and exact commands with expected output. ✅

**Type consistency:** `Apply(..., string? ambientEntity = null)` is defined identically in Task 1 for all three walkers and consumed verbatim in Task 2's `EntityConfigurationWalker.Apply`. `EntityConfigurationWalker.Apply(MethodDeclarationSyntax, Dictionary<string, EfEntity>, EfModel, Compilation)` is defined in Task 2 and consumed verbatim in Task 3. `DiscoverConfigurationFilesAsync(IReadOnlyList<string>, string)` is defined in Task 4 Step 3 and consumed in Step 5. Constant names (`ApplyConfiguration`, `ApplyConfigurationsFromAssembly`, `Configure`, `EntityTypeConfigurationInterface`) added in Task 2 Step 1 are used consistently in Tasks 2 and 4. Helper names (`SimpleName`, `TypeName`, `LastSegment`, `MaterializeEntity`, `AsConfigClass`, `FindConfigClasses`, `CollectExplicitConfigNames`, `HasApplyFromAssembly`) are internally consistent within `EntityConfigurationWalker`. ✅
