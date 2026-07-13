# EF Rewrite — Slice 3: Owned Types & Join Tables (Roslyn fluent entity walker) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retire the last regex-over-text parsing on the DbContext `OnModelCreating` path — the fluent-only **entity materialization** and **`ToTable`** handling still performed by `FluentApiConfigurationParser.ApplyConstraintsFromMethod(...)` — by moving it to a new Roslyn syntax-tree walker (`FluentEntityWalker`), producing byte-identical ERD output (gated by the Slice-0 golden files) and preserving every existing EF unit-test assertion.

**Architecture:** A new `internal static FluentEntityWalker` finds the `.Entity<T>()`/`.Entity("Ns.T")` and `.ToTable("X")` `InvocationExpressionSyntax` nodes in `OnModelCreating`, materializes any entity that has no `DbSet<T>` (resolving its symbol exactly as the regex path did, via `EntityAnalyzer.AnalyzeEntity`), and applies `ToTable` to the owning entity resolved from the call's receiver — never from a forward text scan and never leaking into a nested `OwnsOne`/`OwnsMany`/`UsingEntity` builder lambda (scoped by syntax, not by paren-balance). It is wired into the context path only, replacing the `ApplyConstraintsFromMethod(..., includeRelationships:false, includeProperties:false)` call, and runs **first** (before `FluentPropertyWalker` and `FluentRelationshipWalker`, exactly as today) so those walkers see the fully-materialized entity set. Owned-type and join-table *behavior* is unchanged: `OwnsOne`/`OwnsMany` still contribute nothing to the ERD, and the `UsingEntity` many-to-many join entity is still synthesized by `RelationshipAnalyzer`'s convention path (untouched). The snapshot path keeps `ApplyConstraintsFromMethod` (regex) until Slice 6.

**Tech Stack:** C# / .NET 10, Roslyn (`Microsoft.CodeAnalysis.CSharp.Syntax`), xUnit v3, FluentAssertions, `RoslynTestHelper`, `EfErdGoldenTests` (Slice 0).

## Global Constraints

- Target framework `net10.0`; build enforces `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true` — no warnings allowed.
- XML documentation is required on all public APIs. `FluentEntityWalker` is `internal`, but keep `<summary>` docs on it and every member for consistency with the surrounding files (`FluentPropertyWalker`/`FluentRelationshipWalker` are the model).
- Use `dtk dotnet build` / `dtk dotnet test` / `dtk dotnet format ProjGraph.slnx --verify-no-changes` (the token-optimized DotnetTokenKiller wrapper) for all build/test/format commands.
- The Slice-0 golden files (`tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/*.mmd`) are the parity contract. **This slice targets zero golden changes** — the goldens must never be regenerated. A golden may only change with a reviewer-visible diff justified by a specific fixed finding; none is expected here.
- Do NOT touch the snapshot path (`ModelSnapshotParser`, `AnalyzeSnapshotUseCase`, `AnalyzeSnapshotAsync`), `RelationshipConfigParser`, `PropertyConfigParser`, `FluentApiParsingUtilities`, or the regex patterns' parsing logic in this slice. `ParseEntityConfiguration`'s materialization + `ToTable` blocks stay live for the snapshot path and are retired in Slice 6.
- Do NOT touch `RelationshipAnalyzer` (the M2M→join-table convention synthesis), `FluentPropertyWalker` (Slice 2), or `FluentRelationshipWalker` (Slice 1). A handful of tiny syntax helpers (`SimpleName`, `TypeName`, `LastSegment`, `GenericTypeArgumentName`, `EntityNameFromInvocation`, `IsInsideNestedBuilderScope`, `ResolveOwningEntity`, `ChainReceiver`, and the `NestedBuilderScopes` set) are duplicated between `FluentEntityWalker` and `FluentPropertyWalker` — consolidating them is deferred to Slice 6, matching the Slice-1/2 "old and new briefly coexist, relocate at Slice 6" stance.

### Byte-exact behaviors to preserve (verified against the current pipeline)

- **Fluent-only entity materialization.** An entity declared only via `modelBuilder.Entity<T>()` (no `DbSet<T>`) must appear in the model. The single such case across all goldens/samples is **`ProductSupplier`** in `samples/erd/complex-ecommerce/Data/MyDbContext.cs` (not a `DbSet`; declared via three `.Entity<ProductSupplier>()` calls). It must be resolved via `compilation.GetSymbolsWithName(name, SymbolFilter.Type)` → `EntityAnalyzer.AnalyzeEntity(symbol)` (falling back to `new EfEntity { Name = name }` when the symbol is unresolved), then added to **both** the `entities` dictionary and `model.Entities` (if absent) — identical to `ParseEntityConfiguration` (`FluentApiConfigurationParser.cs:164-183`). Its `ProductId`/`SupplierId` become `PK,FK` via the downstream `FluentPropertyWalker` (`HasKey`) and `FluentRelationshipWalker` (`HasForeignKey`), so `FluentEntityWalker` must run **before** them (unchanged order).
- **`ToTable` / `TableName` (context path).** `modelBuilder.Entity<T>().ToTable("X")` must set `EfEntity.TableName = "X"` on the owning entity. This is asserted on the context path by `EfAnalysisAdvancedTests.AnalyzeContextAsync_ShouldShortenDefaultValueNamespaces` (`tests/ProjGraph.Tests.Unit.EntityFramework/EfAnalysisAdvancedTests.cs:425,442`: `.ToTable("tbl_Assistants")` → `assistant.TableName.Should().Be("tbl_Assistants")`). The regex path replaced the entity instance with a copy carrying `TableName` (`FluentApiConfigurationParser.cs:198-221`); the walker must do the same (copy `Name`/`Properties`/`IsJoinEntity`, set `TableName`, replace by name in `entities` and by index in `model.Entities`). `TableName` is never rendered in the ERD (`MermaidErdRenderer` reads only `entity.Name`), so it is golden-neutral — but the unit test above locks it. No golden fixture or `samples/erd/**` context uses `.ToTable`, so goldens are unaffected either way.
- **Owned types render nothing.** `OwnsOne`/`OwnsMany` contribute no entity and no property to the ERD today (the owned nav is skipped as a navigation property in `EntityAnalyzer`, the owned type is never materialized, and its nested `.Property`/`.HasKey`/`.ToTable` calls are excluded by the walkers' nested-scope guard). `fixture-owned-join.mmd` shows `Customer { int Id PK }`. This slice preserves that exactly — `FluentEntityWalker` must **not** materialize a type referenced only inside an `OwnsOne`/`OwnsMany`/`UsingEntity` builder lambda, and must **not** apply a `ToTable` call nested inside one.
- **`UsingEntity` join entity unchanged.** The `CustomerProduct` / `ProductSupplier`-style join entities in the goldens are synthesized by `RelationshipAnalyzer.ConvertManyToManyToJoinTables` from the many-to-many convention (sorted-name concatenation), **not** from the `UsingEntity(...)` call. `RelationshipAnalyzer` is untouched, so this is unchanged.
- **Snapshot path untouched.** `ModelSnapshotParser.Parse` (`ModelSnapshotParser.cs:37`) still calls `ApplyConstraintsFromMethod`, which still materializes entities and parses `ToTable` for snapshots (asserted by `EfAnalysisServiceSnapshotTests.cs:85,99` — `b.ToTable("Blogs")` → `blog.TableName.Should().Be("Blogs")`). After this slice, `ApplyConstraintsFromMethod` is called **only** by the snapshot path.

### Finding folded into this slice

- **Low #13 (`ToTable` schema overload / dead `TableName`).** The regex `ToTablePattern` (`\.ToTable\("([^"]+)"\)`) matches only the single-argument form, so `.ToTable("X", "schema")` silently set no table name. `FluentEntityWalker` corrects this on the context path by taking the **first** string-literal argument as the table name (handling both `.ToTable("X")` and `.ToTable("X", "schema")`), locked by a new unit test. The dead `TableName` field itself and the snapshot-path `ToTable` regex are removed wholesale in Slice 6 (they remain asserted by snapshot tests until then). Lows **#12** (`RelationshipAnalyzer` `fk.Name` mis-strip), **#14** (snapshot-path unquoted-arg fallback), and **#16** (`IsInsideUsingEntityBlock` O(n²)) are **out of scope**: #12 lives in the untouched `RelationshipAnalyzer`; #14/#16 live on the snapshot path (their only remaining callers) and are retired in Slice 6. Note in the PR description that they are deferred.

---

### Task 1: `FluentEntityWalker` foundation — materialize fluent-only entities

**Files:**
- Create: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentEntityWalkerTests.cs`

**Interfaces:**
- Consumes (existing, do not change): `EntityAnalyzer.AnalyzeEntity(INamedTypeSymbol)`, `EfAnalysisConstants.EfMethods.Entity`/`.OwnsOne`/`.OwnsMany`/`.UsingEntity`, `RoslynTestHelper.CreateCompilation`, `RoslynTestHelper.GetTypeSymbol`, `EntityAnalyzer.AnalyzeEntity`.
- Produces (Tasks 2–3 rely on these exact members):
  - `internal static class FluentEntityWalker` with `public static void Apply(MethodDeclarationSyntax method, Dictionary<string, EfEntity> entities, EfModel model, Compilation compilation)`.
  - Private helpers `FindConfigRoots(MethodDeclarationSyntax, string)`, `IsInsideNestedBuilderScope(SyntaxNode)`, `MaterializeEntity(InvocationExpressionSyntax, Dictionary<string, EfEntity>, EfModel, Compilation)`, `EntityNameFromInvocation(InvocationExpressionSyntax)`, `GenericTypeArgumentName(InvocationExpressionSyntax)`, `SimpleName(SimpleNameSyntax)`, `TypeName(TypeSyntax)`, `LastSegment(string)`, and the `NestedBuilderScopes` set.

- [ ] **Step 1: Write the failing tests**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/FluentEntityWalkerTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Unit tests for <see cref="FluentEntityWalker"/>: the Roslyn fluent-chain entity/table walker that
/// replaces the regex entity-materialization and <c>ToTable</c> parsing on the DbContext path.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class FluentEntityWalkerTests
{
    /// <summary>
    /// Compiles <paramref name="source"/>, locates its OnModelCreating method, and seeds the entities
    /// dictionary/model from the named "DbSet" entity classes so the walker can be driven in isolation.
    /// The fluent-only entities under test are deliberately NOT seeded.
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
    public void Apply_FluentOnlyEntity_MaterializedFromGenericEntityCall()
    {
        const string source = """
            using System.Collections.Generic;
            public class Product { public int Id { get; set; } }
            public class Supplier { public int Id { get; set; } }
            public class ProductSupplier { public int ProductId { get; set; } public int SupplierId { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<ProductSupplier>().HasKey(ps => new { ps.ProductId, ps.SupplierId });
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Product", "Supplier");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities.Should().ContainKey("ProductSupplier");
        model.Entities.Should().ContainSingle(e => e.Name == "ProductSupplier");
        entities["ProductSupplier"].Properties.Select(p => p.Name)
            .Should().BeEquivalentTo("ProductId", "SupplierId");
    }

    [Fact]
    public void Apply_EntityAlreadyPresent_NotDuplicatedAndSameInstance()
    {
        const string source = """
            public class Blog { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Blog>().HasKey(b => b.Id);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog");
        var original = entities["Blog"];

        FluentEntityWalker.Apply(method, entities, model, compilation);

        model.Entities.Should().ContainSingle(e => e.Name == "Blog");
        entities["Blog"].Should().BeSameAs(original);
    }

    [Fact]
    public void Apply_StringEntityForm_MaterializesLastNamespaceSegment()
    {
        const string source = """
            namespace My.Ns { public class Widget { public int Id { get; set; } } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity("My.Ns.Widget");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source);

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities.Should().ContainKey("Widget");
        model.Entities.Should().ContainSingle(e => e.Name == "Widget");
    }

    [Fact]
    public void Apply_TypeReferencedOnlyInsideOwnsOne_NotMaterialized()
    {
        const string source = """
            public class Customer { public int Id { get; set; } public Address Address { get; set; } = null!; }
            public class Address { public string City { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Customer>(e =>
                    {
                        e.OwnsOne(c => c.Address, a => a.Property(p => p.City).HasMaxLength(50));
                    });
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Customer");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities.Should().NotContainKey("Address");
        model.Entities.Should().NotContain(e => e.Name == "Address");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~FluentEntityWalkerTests"`
Expected: FAIL to compile — `FluentEntityWalker` does not exist yet.

- [ ] **Step 3: Create `FluentEntityWalker` with the materialization half**

Create `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Walks the Fluent API invocation chains of a configuring method (OnModelCreating) directly on the
/// C# syntax tree to discover entity-level configuration: fluent-only entities declared via
/// <c>modelBuilder.Entity&lt;T&gt;()</c> / <c>Entity("Ns.T")</c> that have no <c>DbSet&lt;T&gt;</c>, and
/// their <c>.ToTable("X")</c> mapping. Replaces the text/regex materialization and <c>ToTable</c> parsing
/// that <see cref="FluentApiConfigurationParser.ApplyConstraintsFromMethod"/> performed on the DbContext
/// path. The receiver expression of each chain determines the owning entity, so configuration never leaks
/// between unrelated statements or into nested owned-type / join-entity builder lambdas.
/// </summary>
internal static class FluentEntityWalker
{
    /// <summary>
    /// Fluent methods that open a nested builder lambda for a *different* target (an owned type or a join
    /// entity). <c>Entity</c>/<c>ToTable</c> calls inside their argument lists configure that nested builder,
    /// not the outer model, and are ignored — mirroring the scoping in <see cref="FluentPropertyWalker"/>
    /// and <see cref="FluentRelationshipWalker"/>.
    /// </summary>
    private static readonly HashSet<string> NestedBuilderScopes = new(StringComparer.Ordinal)
    {
        EfAnalysisConstants.EfMethods.OwnsOne,
        EfAnalysisConstants.EfMethods.OwnsMany,
        EfAnalysisConstants.EfMethods.UsingEntity
    };

    /// <summary>
    /// Materializes fluent-only entities (Task 1) and applies <c>ToTable</c> mappings (Task 2) found in
    /// <paramref name="method"/>.
    /// </summary>
    /// <param name="method">The <c>OnModelCreating</c> method declaration to walk.</param>
    /// <param name="entities">Entities already discovered from DbSets; augmented in place with fluent-only entities.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is populated.</param>
    /// <param name="compilation">The Roslyn compilation for symbol resolution of fluent-only entity types.</param>
    public static void Apply(
        MethodDeclarationSyntax method,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        foreach (var entityInvocation in FindConfigRoots(method, EfAnalysisConstants.EfMethods.Entity))
        {
            MaterializeEntity(entityInvocation, entities, model, compilation);
        }
    }

    /// <summary>
    /// Finds every invocation whose immediate member name is <paramref name="methodName"/>, excluding those
    /// nested inside an owned-type / join-entity builder lambda (see <see cref="NestedBuilderScopes"/>).
    /// </summary>
    /// <param name="method">The method to scan.</param>
    /// <param name="methodName">The simple method name to match (e.g. <c>Entity</c> or <c>ToTable</c>).</param>
    private static IEnumerable<InvocationExpressionSyntax> FindConfigRoots(
        MethodDeclarationSyntax method,
        string methodName)
    {
        return method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma
                          && SimpleName(ma.Name) == methodName
                          && !IsInsideNestedBuilderScope(inv));
    }

    /// <summary>
    /// Determines whether a node is lexically inside the argument list of an owned-type / join-entity builder
    /// invocation (<see cref="NestedBuilderScopes"/>). The argument list — not the whole invocation — is tested
    /// because such a call is itself chained onto the entity being configured.
    /// </summary>
    /// <param name="node">The node to test.</param>
    private static bool IsInsideNestedBuilderScope(SyntaxNode node)
    {
        return node.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma
                        && NestedBuilderScopes.Contains(SimpleName(ma.Name))
                        && inv.ArgumentList.Span.Contains(node.Span));
    }

    /// <summary>
    /// Materializes the entity named by an <c>Entity&lt;T&gt;()</c> / <c>Entity("Ns.T")</c> invocation when it
    /// is not already known, resolving its symbol (or falling back to a bare entity) and adding it to both the
    /// entities dictionary and the model. Mirrors the regex parser's materialization.
    /// </summary>
    /// <param name="entityInvocation">The <c>Entity</c> invocation.</param>
    /// <param name="entities">The known entities, augmented in place.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is augmented.</param>
    /// <param name="compilation">The compilation for symbol resolution.</param>
    private static void MaterializeEntity(
        InvocationExpressionSyntax entityInvocation,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        var entityName = EntityNameFromInvocation(entityInvocation);
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

    /// <summary>Returns the entity name from an <c>Entity&lt;T&gt;()</c> or <c>Entity("NS.T")</c> invocation.</summary>
    /// <param name="invocation">The Entity invocation.</param>
    private static string? EntityNameFromInvocation(InvocationExpressionSyntax invocation)
    {
        var generic = GenericTypeArgumentName(invocation);
        if (generic is not null)
        {
            return generic;
        }

        var arg = invocation.ArgumentList.Arguments.FirstOrDefault();
        return arg?.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? LastSegment(literal.Token.ValueText)
            : null;
    }

    /// <summary>Returns the first generic type argument's simple name for an invocation like <c>Entity&lt;T&gt;()</c>, else <see langword="null"/>.</summary>
    /// <param name="invocation">The invocation.</param>
    private static string? GenericTypeArgumentName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax generic }
            && generic.TypeArgumentList.Arguments.Count >= 1)
        {
            return TypeName(generic.TypeArgumentList.Arguments[0]);
        }

        return null;
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

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~FluentEntityWalkerTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Build and format-check**

Run: `dtk dotnet build ProjGraph.slnx` then `dtk dotnet format ProjGraph.slnx --verify-no-changes`
Expected: build 0 warnings/errors; format reports no changes.

- [ ] **Step 6: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs tests/ProjGraph.Tests.Unit.EntityFramework/FluentEntityWalkerTests.cs
git commit -m "feat(ef): Slice 3 Task 1 — FluentEntityWalker materializes fluent-only entities"
```

---

### Task 2: `FluentEntityWalker` — `ToTable`/`TableName` (with Low #13 schema-overload correction)

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentEntityWalkerTests.cs`

**Interfaces:**
- Consumes (from Task 1): `FluentEntityWalker.FindConfigRoots`, `.SimpleName`, `.EntityNameFromInvocation`, `.GenericTypeArgumentName`, `.LastSegment`.
- Produces (Task 3 relies on): the same `FluentEntityWalker.Apply(...)` entry point now also applying `ToTable`; new private helpers `ApplyTableName(InvocationExpressionSyntax, Dictionary<string, EfEntity>, EfModel)`, `TableNameArgument(InvocationExpressionSyntax)`, `ResolveOwningEntity(InvocationExpressionSyntax)`, `ChainReceiver(InvocationExpressionSyntax)`.

- [ ] **Step 1: Add the failing `ToTable` tests**

Append these methods inside `FluentEntityWalkerTests` (before the closing brace):

```csharp
    [Fact]
    public void Apply_ToTable_SetsTableNameOnOwningEntity()
    {
        const string source = """
            public class Order { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Order>().ToTable("Orders");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities["Order"].TableName.Should().Be("Orders");
        model.Entities.Single(e => e.Name == "Order").TableName.Should().Be("Orders");
    }

    [Fact]
    public void Apply_ToTableSchemaOverload_SetsTableNameFromFirstArgument()
    {
        // Low #13: the old regex only matched single-arg .ToTable("X"); the two-arg schema overload
        // silently set no table name. The walker takes the first string-literal argument.
        const string source = """
            public class Order { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Order>().ToTable("Orders", "sales");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Order");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities["Order"].TableName.Should().Be("Orders");
    }

    [Fact]
    public void Apply_ToTableInsideOwnsOne_DoesNotLeakToOwner()
    {
        const string source = """
            public class Customer { public int Id { get; set; } public Address Address { get; set; } = null!; }
            public class Address { public string City { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Customer>(e =>
                    {
                        e.OwnsOne(c => c.Address, a => a.ToTable("Addresses"));
                    });
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Customer");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities["Customer"].TableName.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ToTableOnFluentOnlyEntity_MaterializesAndSetsTableName()
    {
        const string source = """
            public class Product { public int Id { get; set; } }
            public class Supplier { public int Id { get; set; } }
            public class ProductSupplier { public int ProductId { get; set; } public int SupplierId { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<ProductSupplier>().ToTable("product_supplier");
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Product", "Supplier");

        FluentEntityWalker.Apply(method, entities, model, compilation);

        entities.Should().ContainKey("ProductSupplier");
        entities["ProductSupplier"].TableName.Should().Be("product_supplier");
        model.Entities.Single(e => e.Name == "ProductSupplier").TableName.Should().Be("product_supplier");
    }
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~FluentEntityWalkerTests"`
Expected: FAIL — the four `ToTable` tests fail (`TableName` is empty / entity not materialized-with-table); `Apply` does not yet process `ToTable`.

- [ ] **Step 3: Add the `ToTable` pass to `FluentEntityWalker`**

In `FluentEntityWalker.cs`, extend `Apply` to run the `ToTable` pass after materialization. Replace the body of `Apply` (the single `foreach` over `Entity` roots) with:

```csharp
        foreach (var entityInvocation in FindConfigRoots(method, EfAnalysisConstants.EfMethods.Entity))
        {
            MaterializeEntity(entityInvocation, entities, model, compilation);
        }

        foreach (var toTableInvocation in FindConfigRoots(method, EfAnalysisConstants.EfMethods.ToTable))
        {
            ApplyTableName(toTableInvocation, entities, model);
        }
```

Then add these members (place `ApplyTableName`, `TableNameArgument`, `ResolveOwningEntity`, and `ChainReceiver` after `MaterializeEntity`):

```csharp
    /// <summary>
    /// Applies a <c>.ToTable("X")</c> call to its owning entity, replacing the entity instance with a copy
    /// carrying the table name in both the entities dictionary and the model. Mirrors the regex parser's
    /// table-mapping step; the table name is the call's first string-literal argument, which also covers the
    /// <c>.ToTable("X", "schema")</c> overload (Low #13).
    /// </summary>
    /// <param name="toTableInvocation">The <c>ToTable</c> invocation.</param>
    /// <param name="entities">The known entities.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is updated.</param>
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
        if (entityName is null || !entities.TryGetValue(entityName, out var entity))
        {
            return;
        }

        var updated = new EfEntity
        {
            Name = entity.Name,
            Properties = entity.Properties,
            IsJoinEntity = entity.IsJoinEntity,
            TableName = tableName
        };

        entities[entityName] = updated;
        var index = model.Entities.IndexOf(model.Entities.FirstOrDefault(e => e.Name == entity.Name)!);
        if (index >= 0)
        {
            model.Entities[index] = updated;
        }
    }

    /// <summary>Returns the first string-literal argument of a <c>ToTable</c> call (the table name), else <see langword="null"/>.</summary>
    /// <param name="invocation">The ToTable invocation.</param>
    private static string? TableNameArgument(InvocationExpressionSyntax invocation)
    {
        var arg = invocation.ArgumentList.Arguments.FirstOrDefault();
        return arg?.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;
    }

    /// <summary>
    /// Resolves the entity that owns a configuration call, either from an <c>Entity&lt;T&gt;()</c> earlier in
    /// the same chain (<c>modelBuilder.Entity&lt;T&gt;().ToTable(...)</c>) or from the enclosing
    /// <c>Entity&lt;T&gt;(e =&gt; ...)</c> configuration lambda.
    /// </summary>
    /// <param name="configInvocation">The <c>ToTable</c> invocation.</param>
    private static string? ResolveOwningEntity(InvocationExpressionSyntax configInvocation)
    {
        for (var receiver = ChainReceiver(configInvocation);
             receiver is not null;
             receiver = ChainReceiver(receiver))
        {
            if (receiver.Expression is MemberAccessExpressionSyntax ma
                && SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.Entity)
            {
                return EntityNameFromInvocation(receiver);
            }
        }

        var enclosingEntity = configInvocation.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression is MemberAccessExpressionSyntax ma
                                   && SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.Entity);

        return enclosingEntity is null ? null : EntityNameFromInvocation(enclosingEntity);
    }

    /// <summary>Returns the invocation on the receiver side of a member-access invocation, or <see langword="null"/>.</summary>
    /// <param name="invocation">The invocation whose receiver to inspect.</param>
    private static InvocationExpressionSyntax? ChainReceiver(InvocationExpressionSyntax invocation)
    {
        return (invocation.Expression as MemberAccessExpressionSyntax)?.Expression as InvocationExpressionSyntax;
    }
```

Also update the `Apply` XML `<summary>` if it referenced only materialization, and confirm the class still compiles (the `MaterializeEntity` and helper members from Task 1 are unchanged).

- [ ] **Step 4: Run the walker tests to verify they pass**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FullyQualifiedName~FluentEntityWalkerTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Build and format-check**

Run: `dtk dotnet build ProjGraph.slnx` then `dtk dotnet format ProjGraph.slnx --verify-no-changes`
Expected: build 0 warnings/errors; format reports no changes.

- [ ] **Step 6: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs tests/ProjGraph.Tests.Unit.EntityFramework/FluentEntityWalkerTests.cs
git commit -m "feat(ef): Slice 3 Task 2 — FluentEntityWalker applies ToTable (+#13 schema overload)"
```

---

### Task 3: Wire the walker into the context path and retire the regex call

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs`

**Interfaces:**
- Consumes: `FluentEntityWalker.Apply(MethodDeclarationSyntax, Dictionary<string, EfEntity>, EfModel, Compilation)` (Tasks 1–2).
- Produces: `ApplyFluentApiConstraints` now drives `FluentEntityWalker` → `FluentPropertyWalker` → `FluentRelationshipWalker` (no regex call on the context path). `ApplyConstraintsFromMethod(MethodDeclarationSyntax, Dictionary<string, EfEntity>, EfModel, Compilation)` loses its `includeRelationships`/`includeProperties` parameters (now called only by the snapshot path, always with both concerns enabled).

- [ ] **Step 1: Replace the context-path regex call with the walker**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs`, in `ApplyFluentApiConstraints`, replace the seam comment + call (currently lines 37-43):

```csharp
        // Context path: parse table config and materialize fluent-only entities from text (Slice 3 still
        // handles ToTable/owned/join), but derive property config, primary keys, relationships, and
        // foreign keys from the Roslyn syntax walkers instead of the regex parsers.
        ApplyConstraintsFromMethod(
            methodSyntax, entities, model, compilation, includeRelationships: false, includeProperties: false);
        FluentPropertyWalker.Apply(methodSyntax, entities, compilation);
        FluentRelationshipWalker.Apply(methodSyntax, entities, model, compilation);
```

with (the entity walker runs first so the property/relationship walkers see the full entity set):

```csharp
        // Context path: every concern now flows through the Roslyn syntax walkers. FluentEntityWalker
        // materializes fluent-only entities and applies ToTable; FluentPropertyWalker derives property
        // config + primary keys; FluentRelationshipWalker derives relationships + foreign keys. The regex
        // ApplyConstraintsFromMethod path is used only by the snapshot path now (retired in Slice 6).
        FluentEntityWalker.Apply(methodSyntax, entities, model, compilation);
        FluentPropertyWalker.Apply(methodSyntax, entities, compilation);
        FluentRelationshipWalker.Apply(methodSyntax, entities, model, compilation);
```

- [ ] **Step 2: Remove the now-dead `includeRelationships`/`includeProperties` parameters**

`ApplyConstraintsFromMethod` is now called only by `ModelSnapshotParser` (`ModelSnapshotParser.cs:37`), which passes no flags (both defaulted `true`). Drop the two parameters and make the two concerns unconditional. Apply these edits in `FluentApiConfigurationParser.cs`:

(a) `ApplyConstraintsFromMethod` signature + XML doc — remove the `<param>` docs for `includeRelationships`/`includeProperties` (currently lines 54-63) and the two parameters (lines 69-70), yielding:

```csharp
    /// <summary>
    /// Applies Fluent API constraints from a specific method (e.g., OnModelCreating or BuildModel)
    /// to the specified Entity Framework model. Used by the snapshot path; the context path uses the
    /// Roslyn syntax walkers instead.
    /// </summary>
    /// <param name="methodSyntax">The method declaration syntax to parse.</param>
    /// <param name="entities">The dictionary of entities in the model.</param>
    /// <param name="model">The EF model to apply constraints to.</param>
    /// <param name="compilation">The Roslyn compilation for symbol resolution.</param>
    public static void ApplyConstraintsFromMethod(
        MethodDeclarationSyntax methodSyntax,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
```

(b) Its `ProcessEntityConfigSection` call (currently lines 85-86) becomes:

```csharp
                ProcessEntityConfigSection(entityConfigSections[i], entities, model, compilation);
```

(c) `ProcessEntityConfigSection` signature — remove the two `bool` params (lines 105-106):

```csharp
    private static void ProcessEntityConfigSection(
        string sectionContent,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
```

(d) Its `ParseEntityConfiguration` call (currently lines 118-119) becomes:

```csharp
        var shadowRelationships = ParseEntityConfiguration(section, entities, model, compilation);
```

(e) `ParseEntityConfiguration` signature — remove the two `bool` params (lines 141-142):

```csharp
    private static List<EfRelationship> ParseEntityConfiguration(
        string configSection,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
```

(f) Inside `ParseEntityConfiguration`, make the relationship and property blocks unconditional — replace the guarded blocks (currently lines 185-195):

```csharp
        if (includeRelationships)
        {
            RelationshipConfigParser.ParseShadowRelationships(configSection, entityName, entities, shadowRelationships);
            RelationshipConfigParser.ParseExplicitRelationships(configSection, entityName, entities, shadowRelationships,
                compilation);
        }

        if (includeProperties)
        {
            PropertyConfigParser.ParsePropertyConfigurations(configSection, entity, compilation);
        }
```

with:

```csharp
        RelationshipConfigParser.ParseShadowRelationships(configSection, entityName, entities, shadowRelationships);
        RelationshipConfigParser.ParseExplicitRelationships(configSection, entityName, entities, shadowRelationships,
            compilation);
        PropertyConfigParser.ParsePropertyConfigurations(configSection, entity, compilation);
```

Leave the rest of `ParseEntityConfiguration` (entity materialization + `ToTable` block) unchanged — it stays live for the snapshot path.

- [ ] **Step 3: Build**

Run: `dtk dotnet build ProjGraph.slnx`
Expected: 0 warnings/errors. (`ModelSnapshotParser.cs:37` still compiles — it already omits the removed flags.)

- [ ] **Step 4: Run the full EF unit suite — parity gate**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework`
Expected: PASS. Specifically confirm these parity anchors are green:
- `EfErdGoldenTests.Erd_MatchesGolden` — all 7 golden cases byte-identical (esp. `complex-ecommerce` with fluent-only `ProductSupplier`, and `fixture-owned-join` with `Customer { int Id PK }` + convention join `CustomerProduct`).
- `EfAnalysisAdvancedTests.AnalyzeContextAsync_ShouldShortenDefaultValueNamespaces` — context-path `TableName == "tbl_Assistants"`.
- `EfAnalysisServiceSnapshotTests` — snapshot-path `TableName` still set (`ApplyConstraintsFromMethod` unchanged for snapshots).
- `FluentEntityWalkerTests` — all 8.

- [ ] **Step 5: Run the whole solution test suite**

Run: `dtk dotnet test ProjGraph.slnx`
Expected: PASS (full suite; no regressions in CLI/MCP/integration).

- [ ] **Step 6: Confirm goldens were never regenerated + format-check**

Run: `git status --porcelain tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens` (expected: empty — no golden file changed) and `dtk dotnet format ProjGraph.slnx --verify-no-changes` (expected: no changes).

- [ ] **Step 7: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs
git commit -m "feat(ef): Slice 3 Task 3 — route context-path entity/ToTable config through FluentEntityWalker"
```

---

## Self-Review

**Spec coverage (design §Phase 1, Slice 3):**
- "OwnsOne/OwnsMany (nested builder lambdas scoped by syntax, not paren-balance)" → `FluentEntityWalker.IsInsideNestedBuilderScope` scopes `Entity`/`ToTable` by syntax span; owned-type behavior preserved (renders nothing) and locked by `Apply_TypeReferencedOnlyInsideOwnsOne_NotMaterialized` + `Apply_ToTableInsideOwnsOne_DoesNotLeakToOwner` + the `fixture-owned-join` golden. ✅
- "UsingEntity join-entity synthesis" → synthesis stays in the untouched `RelationshipAnalyzer` convention path; the `UsingEntity` builder lambda is correctly scoped out of entity/table materialization; `fixture-owned-join` golden unchanged. ✅
- Per-slice checklist "delete the corresponding regex code" → the context-path `ApplyConstraintsFromMethod(false,false)` call is removed and the now-dead `includeRelationships`/`includeProperties` parameters + branches are deleted (Task 3). The regex *files* themselves remain for the snapshot path (retired in Slice 6). ✅
- "EF-internal Lows folded here" → #13 corrected on the context path (schema overload) with a locking test; #12/#14/#16 explicitly deferred to Slice 6 with rationale (out of scope: untouched `RelationshipAnalyzer` / snapshot-only callers). ✅
- "golden files reproduce today's output" → zero golden changes; parity gate in Task 3 Step 4/6. ✅

**Placeholder scan:** No TBD/TODO/"add error handling"/"similar to Task N" — every code step contains complete, compilable code and exact commands. ✅

**Type consistency:** `FluentEntityWalker.Apply(MethodDeclarationSyntax, Dictionary<string, EfEntity>, EfModel, Compilation)` is defined identically in Task 1 and consumed verbatim in Task 3. Helper names (`FindConfigRoots`, `IsInsideNestedBuilderScope`, `MaterializeEntity`, `ApplyTableName`, `TableNameArgument`, `ResolveOwningEntity`, `ChainReceiver`, `EntityNameFromInvocation`, `GenericTypeArgumentName`, `SimpleName`, `TypeName`, `LastSegment`) are consistent across tasks. The `ApplyConstraintsFromMethod` parameter removal is threaded through all three call sites (`ProcessEntityConfigSection`, `ParseEntityConfiguration`) and its sole external caller (`ModelSnapshotParser`, which already omits the flags). ✅
