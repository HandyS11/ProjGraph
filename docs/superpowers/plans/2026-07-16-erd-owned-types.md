# ERD Owned-Type Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capture EF Core owned types (`OwnsOne`/`OwnsMany`) from both the DbContext and ModelSnapshot paths and render them in the ERD, in either a physical (MirrorEf, default) or conceptual (Classic) mode.

**Architecture:** `EfModel` records each owned type once as a distinct `EfEntity` (`IsOwned`, `OwnerEntity`, `NavigationName`, `IsCollection`, plus an *effective* `TableName`), never pre-inlined. `MermaidErdRenderer` owns all presentation: it inlines an owned entity onto its owner as `{Nav}_{Prop}` exactly when their effective tables match (MirrorEf), or always draws a box plus an identifying relationship (Classic). Because neither parser path makes a presentation decision, the two paths cannot disagree about one.

**Tech Stack:** .NET 10, Roslyn (`Microsoft.CodeAnalysis.CSharp`), xUnit + FluentAssertions, Spectre.Console.Cli, ModelContextProtocol.Server, Mermaid v11.

**Spec:** `docs/superpowers/specs/2026-07-16-erd-owned-types-design.md`

## Global Constraints

- Build enforces `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true`. XML documentation is required on all public APIs.
- Prefer `dtk` (dotnet-token-killer) over raw `dotnet` for build/test.
- Full test command: `dtk test ProjGraph.slnx`. EF unit tests only: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework`.
- Golden regeneration: `UPDATE_EF_GOLDENS=1 dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "Category=Golden"`. Always review the resulting `git diff` before committing.
- **`EfEntity.Key` is the model's identity, not `Name`.** `Key` is `{Owner}.{Nav}` for an owned entity (e.g. `Order.ShipToAddress`) and `Name` for a root one. To avoid touching the four existing root-entity construction sites (`EntityAnalyzer.AnalyzeEntity`, `FluentSyntax.MaterializeEntity`, `RelationshipAnalyzer.CreateJoinEntity`, and DbSet discovery), `Key` defaults to empty and falls back to `Name`: only owned capture sets it explicitly. Read it through the helper `EfEntity.EffectiveKey` (`string.IsNullOrEmpty(Key) ? Name : Key`) — never read the raw `Key` property for a lookup. `Name` holds the CLR type name and is NOT unique — `Order.ShipToAddress` and `Customer.Address` are distinct EF entity types both named `Address`. The walkers' entity dictionary is keyed by `Key`, `OwnerEntity` stores the owner's `Key`, and EVERY owner/owned lookup (analyzer and renderer alike) matches on `Key`. Matching on `Name` cross-contaminates ownership chains: an owner with two navigations of the same CLR type (`Invoice.ShipTo` and `Invoice.BillTo`, both `InvoiceAddress`) where one owns a nested type would attach that nested type to both. `Name` is for display only.
- An owned collection (`IsCollection`) is never inlined, regardless of table mapping — folding a to-many into flat scalar columns is meaningless. EF never maps an owned collection to the owner's table, so the renderer enforces this as an invariant rather than expecting the case.
- Inlining recursion must be cycle-safe (track visited keys). A self-owning or mutually-owning entity would otherwise raise `StackOverflowException`, which .NET cannot catch — it kills the CLI/MCP process.
- Effective table of an entity = `TableName` when non-empty, else `Name`.
- Identifying relationship syntax: `||--||` for `OwnsOne`, `||--o{` for `OwnsMany`.
- MirrorEf is the default mode everywhere (library default, CLI default, MCP default).
- Do NOT weaken `FluentSyntax.NestedBuilderScopes` for the existing walkers — that fence is what prevents owned config leaking onto the owner, and `ChainedOwnedContext` exists to guard it.
- `UsingEntity` (join entities) stays fenced and out of scope.

---

## File Structure

**Modify:**
- `src/ProjGraph.Core/Models/EfModel.cs` — four new `EfEntity` fields.
- `src/ProjGraph.Lib.Core/Abstractions/DiagramOptions.cs` — new `ErdOwnedMode` option.
- `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentSyntax.cs` — scope-relative traversal.
- `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs` — scope param; preserve new fields on copy.
- `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs` — scope param.
- `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs` — call the owned walker.
- `src/ProjGraph.Lib.EntityFramework/Infrastructure/ModelSnapshotParser.cs` — call the owned walker.
- `src/ProjGraph.Lib.EntityFramework/Rendering/MermaidErdRenderer.cs` — both modes.
- `src/ProjGraph.Cli/Commands/ErdCommand.cs` — `--owned-mode`.
- `src/ProjGraph.Mcp/ProjGraphTools.cs` — `ownedMode` parameter.

**Create:**
- `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentOwnedTypeWalker.cs` — sole owner of owned-type capture, both paths.
- `src/ProjGraph.Lib.EntityFramework/Infrastructure/EfEntityFactory.cs` — `CopyWith` for `EfEntity` (init-only setters make in-place mutation impossible; `FluentEntityWalker` currently hand-copies and would silently drop the new fields).
- `tests/ProjGraph.Tests.Unit.EntityFramework/Rendering/OwnedTypeRenderingTests.cs`
- `tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentOwnedTypeWalkerTests.cs`
- `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/OwnedModesContext.cs`
- `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/OwnedSnapshot.cs`
- `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/CrossPathContext.cs`
- `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/CrossPathSnapshot.cs`
- Goldens: `fixture-owned-modes-mirror.mmd`, `fixture-owned-modes-classic.mmd`, `fixture-owned-snapshot.mmd`.

**Task order rationale:** the renderer becomes owned-aware (Tasks 3–4) *before* capture exists (Tasks 5+). If capture landed first, owned entities would enter `model.Entities` and render as unwanted boxes, breaking every golden mid-plan. Renderer tests build `EfModel` by hand and need no parser.

---

### Task 1: Scope-relative walker traversal (pure refactor)

`FluentSyntax.FindConfigRoots` takes a `MethodDeclarationSyntax` and excludes anything inside any owned/join fence. The owned walker must re-run the same logic *inside* an `OwnsOne` lambda, so discovery must accept any `SyntaxNode` scope and exclude only fences nested strictly **inside that scope** — not the `OwnsOne` fence that opened it.

No behaviour change. The existing goldens are the guard.

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentSyntax.cs:40-64`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs:32-49`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs:32-47`
- Test: `tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentSyntaxScopeTests.cs` (create)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `FluentSyntax.FindConfigRoots(SyntaxNode scope, string methodName)` → `IEnumerable<InvocationExpressionSyntax>`
  - `FluentSyntax.IsInsideNestedBuilderScope(SyntaxNode node, SyntaxNode scope)` → `bool`
  - `FluentEntityWalker.Apply(SyntaxNode scope, Dictionary<string, EfEntity>, EfModel, Compilation, string? ambientEntity = null)`
  - `FluentPropertyWalker.Apply(SyntaxNode scope, Dictionary<string, EfEntity>, Compilation, string? ambientEntity = null)`

`MethodDeclarationSyntax` derives from `SyntaxNode`, so every existing call site compiles unchanged.

- [ ] **Step 1: Write the failing test**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentSyntaxScopeTests.cs`:

```csharp
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework.Infrastructure;

public sealed class FluentSyntaxScopeTests
{
    private const string Source = """
        class C
        {
            void OnModelCreating(object modelBuilder)
            {
                modelBuilder.Entity<Order>(e =>
                {
                    e.Property(o => o.Total);
                    e.OwnsOne(o => o.ShipToAddress, a =>
                    {
                        a.Property(x => x.City);
                    });
                });
            }
        }
        """;

    private static MethodDeclarationSyntax Method() =>
        CSharpSyntaxTree.ParseText(Source).GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

    private static InvocationExpressionSyntax OwnsOneLambdaBody(MethodDeclarationSyntax method)
    {
        var ownsOne = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(i => i.Expression is MemberAccessExpressionSyntax ma
                         && ma.Name.Identifier.Text == "OwnsOne");
        return ownsOne;
    }

    [Fact]
    public void FindConfigRoots_AtMethodScope_ExcludesPropertiesInsideOwnedBuilder()
    {
        var method = Method();

        var roots = FluentSyntax.FindConfigRoots(method, "Property").ToList();

        roots.Should().HaveCount(1, "the City property inside the OwnsOne builder must stay fenced off");
        roots[0].ToString().Should().Contain("o.Total");
    }

    [Fact]
    public void FindConfigRoots_ScopedToOwnedBuilder_FindsOnlyItsOwnProperties()
    {
        var method = Method();
        var ownsOne = OwnsOneLambdaBody(method);

        var roots = FluentSyntax.FindConfigRoots(ownsOne.ArgumentList, "Property").ToList();

        roots.Should().HaveCount(1, "scoping to the owned builder must surface its own Property calls");
        roots[0].ToString().Should().Contain("x.City");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FluentSyntaxScopeTests"`
Expected: FAIL — `FindConfigRoots` has no overload accepting `ArgumentListSyntax` (a `SyntaxNode`), compile error CS1503.

- [ ] **Step 3: Make traversal scope-relative**

In `FluentSyntax.cs`, replace `FindConfigRoots` and `IsInsideNestedBuilderScope`:

```csharp
    /// <summary>
    /// Finds every invocation within <paramref name="scope"/> whose immediate member name is
    /// <paramref name="methodName"/>, excluding those nested inside an owned-type / join-entity builder
    /// lambda that is itself inside <paramref name="scope"/> (see <see cref="NestedBuilderScopes"/>).
    /// Fences enclosing <paramref name="scope"/> are ignored, so a caller may scope directly to an owned
    /// builder's argument list to walk its own configuration.
    /// </summary>
    /// <param name="scope">The syntax node to scan (a method body, or an owned builder's argument list).</param>
    /// <param name="methodName">The simple method name to match (e.g. <c>Entity</c>, <c>Property</c>).</param>
    public static IEnumerable<InvocationExpressionSyntax> FindConfigRoots(
        SyntaxNode scope,
        string methodName)
    {
        return scope.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma
                          && SimpleName(ma.Name) == methodName
                          && !IsInsideNestedBuilderScope(inv, scope));
    }

    /// <summary>
    /// Determines whether a node is lexically inside the argument list of an owned-type / join-entity builder
    /// invocation (<see cref="NestedBuilderScopes"/>) that lies within <paramref name="scope"/>. The argument
    /// list — not the whole invocation — is tested because such a call is itself chained onto the entity being
    /// configured. Fences outside <paramref name="scope"/> do not count.
    /// </summary>
    /// <param name="node">The node to test.</param>
    /// <param name="scope">The scope root; ancestors at or above it are not considered.</param>
    public static bool IsInsideNestedBuilderScope(SyntaxNode node, SyntaxNode scope)
    {
        return node.Ancestors()
            .TakeWhile(ancestor => ancestor != scope && scope.Span.Contains(ancestor.Span))
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma
                        && NestedBuilderScopes.Contains(SimpleName(ma.Name))
                        && inv.ArgumentList.Span.Contains(node.Span));
    }
```

Change the `method` parameter of `FluentEntityWalker.Apply` and `FluentPropertyWalker.Apply` to `SyntaxNode scope` (rename the parameter and its `<param>` doc; update the `FindConfigRoots(method, ...)` calls to `FindConfigRoots(scope, ...)`). Leave `FluentEntityWalker.CollectEntityNames(MethodDeclarationSyntax)` as-is — it deliberately does not exclude fences.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework`
Expected: PASS — both new tests pass and every existing golden still matches (this refactor is behaviour-preserving).

- [ ] **Step 5: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentSyntax.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentSyntaxScopeTests.cs
git commit -m "refactor(ef): make fluent walker traversal scope-relative

Config-root discovery now accepts any SyntaxNode scope and excludes only
owned/join fences nested inside that scope, so an owned-type builder's own
configuration can be walked by scoping to its argument list. No behaviour
change; existing goldens guard it."
```

---

### Task 2: EfModel owned fields + EfEntity copy safety

`FluentEntityWalker.ApplyTableName` hand-constructs a replacement `EfEntity`, copying fields one by one. Adding fields without fixing that silently drops them whenever `ToTable` is applied — which is exactly the owned-type path. Centralise the copy.

**Files:**
- Modify: `src/ProjGraph.Core/Models/EfModel.cs:29-50`
- Create: `src/ProjGraph.Lib.EntityFramework/Infrastructure/EfEntityFactory.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs:102-108`
- Test: `tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/EfEntityFactoryTests.cs` (create)

**Interfaces:**
- Consumes: Task 1's `FluentEntityWalker.Apply(SyntaxNode, ...)`.
- Produces:
  - `EfEntity.IsOwned` (`bool`), `EfEntity.OwnerEntity` (`string?`), `EfEntity.NavigationName` (`string?`), `EfEntity.IsCollection` (`bool`)
  - `EfEntityFactory.CopyWith(EfEntity source, string? tableName = null)` → `EfEntity`

- [ ] **Step 1: Write the failing test**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/EfEntityFactoryTests.cs`:

```csharp
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework.Infrastructure;

public sealed class EfEntityFactoryTests
{
    [Fact]
    public void CopyWith_PreservesOwnedMetadata_WhenOverridingTableName()
    {
        var source = new EfEntity
        {
            Name = "Address",
            IsOwned = true,
            OwnerEntity = "Order",
            NavigationName = "ShipToAddress",
            IsCollection = false,
            TableName = "Orders"
        };
        source.Properties.Add(new EfProperty { Name = "City", Type = "string" });

        var copy = EfEntityFactory.CopyWith(source, "ShipToAddresses");

        copy.TableName.Should().Be("ShipToAddresses");
        copy.IsOwned.Should().BeTrue("owned metadata must survive a ToTable rewrite");
        copy.OwnerEntity.Should().Be("Order");
        copy.NavigationName.Should().Be("ShipToAddress");
        copy.IsCollection.Should().BeFalse();
        copy.Properties.Should().ContainSingle(p => p.Name == "City");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "EfEntityFactoryTests"`
Expected: FAIL — `EfEntity` has no `IsOwned` member and `EfEntityFactory` does not exist (CS0117 / CS0103).

- [ ] **Step 3: Add the fields and the factory**

In `src/ProjGraph.Core/Models/EfModel.cs`, add to `EfEntity` after `TableName`:

```csharp
    /// <summary>
    /// Gets or initializes this entity's identity within the model: <c>{Owner}.{Nav}</c> for an owned
    /// entity, and empty for a root entity (which is identified by its <see cref="Name"/>). Prefer
    /// <see cref="EffectiveKey"/>, which applies that fallback. <see cref="Name"/> holds the CLR type
    /// name and is not unique — two owners may own the same type.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Gets this entity's identity: <see cref="Key"/> when set, otherwise <see cref="Name"/>. Every
    /// owner/owned lookup must match on this rather than on <see cref="Name"/>.
    /// </summary>
    public string EffectiveKey => string.IsNullOrEmpty(Key) ? Name : Key;

    /// <summary>
    /// Gets or initializes a value indicating whether the entity is an EF Core owned type
    /// (configured via <c>OwnsOne</c>/<c>OwnsMany</c>) rather than a root entity.
    /// </summary>
    public bool IsOwned { get; init; }

    /// <summary>
    /// Gets or initializes the <see cref="EffectiveKey"/> of the entity that owns this one, when
    /// <see cref="IsOwned"/> is <see langword="true"/>; otherwise <see langword="null"/>.
    /// </summary>
    public string? OwnerEntity { get; init; }

    /// <summary>
    /// Gets or initializes the owner's navigation property name for this owned type (e.g.
    /// <c>ShipToAddress</c>), when <see cref="IsOwned"/> is <see langword="true"/>; otherwise
    /// <see langword="null"/>. Source of both EF's column prefix and the identifying relationship.
    /// </summary>
    public string? NavigationName { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether this owned type is a collection
    /// (<c>OwnsMany</c>) rather than a reference (<c>OwnsOne</c>).
    /// </summary>
    public bool IsCollection { get; init; }
```

Create `src/ProjGraph.Lib.EntityFramework/Infrastructure/EfEntityFactory.cs`:

```csharp
using ProjGraph.Core.Models;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Factory for <see cref="EfEntity"/> instances. Init-only setters make in-place mutation impossible,
/// so walkers replace entities wholesale; centralising the copy here keeps every field — notably the
/// owned-type metadata — from being silently dropped when a single field is rewritten.
/// </summary>
internal static class EfEntityFactory
{
    /// <summary>
    /// Creates a copy of <paramref name="source"/>, optionally overriding the table name.
    /// </summary>
    /// <param name="source">The entity to copy.</param>
    /// <param name="tableName">The replacement table name, or <see langword="null"/> to keep the source's.</param>
    public static EfEntity CopyWith(EfEntity source, string? tableName = null)
    {
        var copy = new EfEntity
        {
            Name = source.Name,
            IsJoinEntity = source.IsJoinEntity,
            TableName = tableName ?? source.TableName,
            Key = source.Key,
            IsOwned = source.IsOwned,
            OwnerEntity = source.OwnerEntity,
            NavigationName = source.NavigationName,
            IsCollection = source.IsCollection
        };

        foreach (var property in source.Properties)
        {
            copy.Properties.Add(property);
        }

        return copy;
    }
}
```

In `FluentEntityWalker.ApplyTableName`, replace the hand-built copy:

```csharp
        var updated = EfEntityFactory.CopyWith(entity, tableName);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework`
Expected: PASS — new test passes, all goldens unchanged (new fields default to `false`/`null` and nothing reads them yet).

- [ ] **Step 5: Commit**

```bash
git add src/ProjGraph.Core/Models/EfModel.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/EfEntityFactory.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentEntityWalker.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/EfEntityFactoryTests.cs
git commit -m "feat(core): record owned-type metadata on EfEntity

Adds IsOwned/OwnerEntity/NavigationName/IsCollection, and routes entity
copies through EfEntityFactory so a ToTable rewrite cannot drop them."
```

---

### Task 3: ErdOwnedMode option + renderer MirrorEf mode

The renderer decides inline-vs-box by comparing effective tables. Tests build `EfModel` by hand — no parser involved.

This task also adds the `ErdOwnedMode` option the renderer switches on. The option and its first consumer land together because the renderer cannot compile without it.

**Files:**
- Modify: `src/ProjGraph.Lib.Core/Abstractions/DiagramOptions.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Rendering/MermaidErdRenderer.cs:46-64,112-127`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/EfPropertyFactory.cs`
- Test: `tests/ProjGraph.Tests.Unit.EntityFramework/Rendering/OwnedTypeRenderingTests.cs` (create)

**Interfaces:**
- Consumes: Task 2's `EfEntity.IsOwned` / `OwnerEntity` / `NavigationName` / `IsCollection`.
- Produces:
  - `ErdOwnedMode` enum (`MirrorEf = 0`, `Classic = 1`) in `ProjGraph.Lib.Core.Abstractions`
  - `DiagramOptions.ErdOwnedMode` — fourth positional parameter, defaulting to `MirrorEf`
  - `EfPropertyFactory.Rename(EfProperty source, string name)` → `EfProperty`
  - `MermaidErdRenderer.Render(EfModel, DiagramOptions?)` handling owned entities. Private helpers: `EffectiveTable(EfEntity)`, `IsInlined(EfEntity, EfModel, DiagramOptions?)`, `EffectiveProperties`, `DisplayName`, `RenderEntities`, `RenderRelationships`.

- [ ] **Step 1: Write the failing tests**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Rendering/OwnedTypeRenderingTests.cs`:

```csharp
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Rendering;

namespace ProjGraph.Tests.Unit.EntityFramework.Rendering;

public sealed class OwnedTypeRenderingTests
{
    private static EfModel ModelWithOwned(string ownedTable, bool isCollection)
    {
        var order = new EfEntity { Name = "Order", TableName = "Orders" };
        order.Properties.Add(new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true, IsValueType = true });

        var address = new EfEntity
        {
            Name = "Address",
            IsOwned = true,
            OwnerEntity = "Order",
            NavigationName = "ShipToAddress",
            IsCollection = isCollection,
            TableName = ownedTable
        };
        address.Properties.Add(new EfProperty
        {
            Name = "ZipCode", Type = "string", IsExplicitlyRequired = true, IsRequired = true, MaxLength = 18
        });

        var model = new EfModel { ContextName = "Ctx" };
        model.Entities.Add(order);
        model.Entities.Add(address);
        return model;
    }

    private static string Render(EfModel model) =>
        new MermaidErdRenderer().Render(model, new DiagramOptions(false, false));

    [Fact]
    public void MirrorEf_TableSplitOwnsOne_InlinesPrefixedColumnsOntoOwner()
    {
        var output = Render(ModelWithOwned("Orders", isCollection: false));

        output.Should().Contain("ShipToAddress_ZipCode");
        output.Should().Contain("required, max:18");
        output.Should().NotContain("Address {", "a table-split owned type must not get its own box");
        output.Should().NotContain("||--||", "an inlined owned type has no relationship line");
    }

    [Fact]
    public void MirrorEf_OwnsOneWithOwnTable_RendersBoxAndIdentifyingRelationship()
    {
        var output = Render(ModelWithOwned("ShipToAddresses", isCollection: false));

        output.Should().Contain("Address {");
        output.Should().Contain("string ZipCode", "a separate box keeps unprefixed column names");
        output.Should().Contain("Order ||--|| Address");
        output.Should().NotContain("ShipToAddress_ZipCode");
    }

    [Fact]
    public void MirrorEf_OwnsMany_AlwaysRendersBoxWithCollectionRelationship()
    {
        var output = Render(ModelWithOwned("Order_ShipToAddress", isCollection: true));

        output.Should().Contain("Address {");
        output.Should().Contain("Order ||--o{ Address");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "OwnedTypeRenderingTests"`
Expected: FAIL — the renderer ignores owned metadata, so `Address {` is always emitted and no relationship line appears.

- [ ] **Step 3: Add the ErdOwnedMode option**

Replace `src/ProjGraph.Lib.Core/Abstractions/DiagramOptions.cs`:

```csharp
namespace ProjGraph.Lib.Core.Abstractions;

/// <summary>
/// Controls how EF Core owned types (<c>OwnsOne</c>/<c>OwnsMany</c>) appear in a rendered ERD.
/// </summary>
public enum ErdOwnedMode
{
    /// <summary>
    /// The physical view: an owned type that shares its owner's table is inlined onto the owner using
    /// EF's <c>Nav_Property</c> column naming; one on its own table gets its own entity box.
    /// </summary>
    MirrorEf = 0,

    /// <summary>
    /// The conceptual view: every owned type is its own entity box linked to the owner by an identifying
    /// relationship, regardless of table mapping.
    /// </summary>
    Classic = 1
}

/// <summary>
/// Represents options for rendering a diagram.
/// </summary>
/// <param name="ShowTitle">Whether to include the title in the rendered output.</param>
/// <param name="WrapInMarkdownFence">Whether to wrap the output in a ```mermaid code fence. Defaults to <see langword="true"/>.</param>
/// <param name="IncludePackages">Whether to include NuGet package dependencies in the graph.</param>
/// <param name="ErdOwnedMode">How EF Core owned types are represented in an ERD. Defaults to <see cref="ErdOwnedMode.MirrorEf"/>.</param>
public record DiagramOptions(
    bool ShowTitle = true,
    bool WrapInMarkdownFence = true,
    bool IncludePackages = false,
    ErdOwnedMode ErdOwnedMode = ErdOwnedMode.MirrorEf
);
```

The new positional parameter is optional, so every existing `new DiagramOptions(...)` call site compiles unchanged.

- [ ] **Step 4: Implement MirrorEf**

In `MermaidErdRenderer.cs`, replace `RenderEntities` and `RenderRelationships`, and add the helpers:

```csharp
    /// <summary>
    /// Returns the table an entity effectively maps to: its explicit table name, or its entity name
    /// when unmapped (EF's default).
    /// </summary>
    /// <param name="entity">The entity.</param>
    private static string EffectiveTable(EfEntity entity)
        => string.IsNullOrEmpty(entity.TableName) ? entity.Name : entity.TableName;

    /// <summary>
    /// Determines whether an owned entity's columns are folded into its owner rather than drawn as their
    /// own box: true when it shares the owner's table (EF table-splitting). Always false in Classic mode.
    /// </summary>
    /// <param name="entity">The candidate entity.</param>
    /// <param name="model">The model, used to resolve the owner.</param>
    /// <param name="options">The render options carrying the owned mode.</param>
    private static bool IsInlined(EfEntity entity, EfModel model, DiagramOptions? options)
    {
        if (!entity.IsOwned || (options?.ErdOwnedMode ?? ErdOwnedMode.MirrorEf) == ErdOwnedMode.Classic)
        {
            return false;
        }

        // An owned collection is never inlined: folding a to-many into flat scalar columns on the owner
        // is meaningless. EF never maps one to the owner's table, so this enforces the invariant.
        if (entity.IsCollection)
        {
            return false;
        }

        var owner = model.Entities.FirstOrDefault(e => e.EffectiveKey == entity.OwnerEntity);
        return owner is not null && EffectiveTable(owner) == EffectiveTable(entity);
    }

    /// <summary>
    /// Returns the properties rendered for an entity: its own, plus the prefixed columns of every owned
    /// entity inlined into it (recursively, so nested ownership compounds prefixes as EF does).
    /// </summary>
    /// <param name="entity">The entity being rendered.</param>
    /// <param name="model">The model.</param>
    /// <param name="options">The render options.</param>
    private static IEnumerable<EfProperty> EffectiveProperties(
        EfEntity entity, EfModel model, DiagramOptions? options, HashSet<string>? visited = null)
    {
        foreach (var property in entity.Properties)
        {
            yield return property;
        }

        // Nothing in the model type prevents a self-owning or mutually-owning entity, and unbounded
        // recursion would raise StackOverflowException — uncatchable, killing the CLI/MCP process.
        visited ??= [];
        if (!visited.Add(entity.EffectiveKey))
        {
            yield break;
        }

        var inlinedChildren = model.Entities
            .Where(e => e.OwnerEntity == entity.EffectiveKey && IsInlined(e, model, options));

        foreach (var child in inlinedChildren)
        {
            foreach (var property in EffectiveProperties(child, model, options, visited))
            {
                // EF names table-split owned columns Nav_Property; nested ownership compounds the prefix.
                yield return EfPropertyFactory.Rename(property, $"{child.NavigationName}_{property.Name}");
            }
        }
    }
```

Replace `RenderEntities`:

```csharp
    private static void RenderEntities(EfModel model, StringBuilder sb, DiagramOptions? options)
    {
        var rendered = model.Entities
            .Where(e => !IsInlined(e, model, options))
            .OrderBy(e => e.Name);

        foreach (var entity in rendered)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {SanitizeEntityName(DisplayName(entity, model, options))} {{");

            var orderedProperties = EffectiveProperties(entity, model, options)
                .OrderByDescending(p => p.IsPrimaryKey)
                .ThenByDescending(p => p is { IsPrimaryKey: false, IsForeignKey: true })
                .ThenBy(p => p.Name);

            foreach (var propertyLine in orderedProperties.Select(RenderProperty))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {propertyLine}");
            }

            sb.AppendLine("  }");
        }
    }
```

Extend `RenderRelationships` to append derived owned relationships:

```csharp
    private static void RenderRelationships(EfModel model, StringBuilder sb, DiagramOptions? options)
    {
        var sortedRelationships = model.Relationships
            .OrderBy(r => r.SourceEntity)
            .ThenBy(r => r.TargetEntity)
            .ThenBy(r => r.Type);

        foreach (var rel in sortedRelationships)
        {
            var relSyntax = GetRelationshipSyntax(rel);
            var sourceEntity = SanitizeEntityName(rel.SourceEntity.Trim());
            var targetEntity = SanitizeEntityName(rel.TargetEntity.Trim());

            sb.AppendLine(CultureInfo.InvariantCulture, $"  {sourceEntity} {relSyntax} {targetEntity} : \"\"");
        }

        // Owned relationships are derived, never stored: an inlined owned type must have no line, and
        // deriving here keeps that decision in the same place as the inlining decision.
        var ownedBoxes = model.Entities
            .Where(e => e.IsOwned && !IsInlined(e, model, options))
            .OrderBy(e => e.OwnerEntity)
            .ThenBy(e => e.Name);

        foreach (var owned in ownedBoxes)
        {
            var owner = model.Entities.FirstOrDefault(e => e.EffectiveKey == owned.OwnerEntity);
            if (owner is null)
            {
                continue;
            }

            var syntax = owned.IsCollection ? "||--o{" : "||--||";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {SanitizeEntityName(DisplayName(owner, model, options))} {syntax} " +
                $"{SanitizeEntityName(DisplayName(owned, model, options))} : \"{owned.NavigationName}\"");
        }
    }
```

Add `DisplayName` (collision-qualified, per the spec):

```csharp
    /// <summary>
    /// Returns the label for an entity box: its simple name, qualified to <c>{Owner}_{Nav}</c> when another
    /// rendered entity shares that name (two owners may own the same CLR type, which EF treats as distinct
    /// entity types).
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="model">The model, used to detect name collisions.</param>
    /// <param name="options">The render options, used to tell which entities are actually drawn.</param>
    private static string DisplayName(EfEntity entity, EfModel model, DiagramOptions? options)
    {
        if (!entity.IsOwned)
        {
            return entity.Name;
        }

        // Only entities actually drawn can collide on the diagram; an inlined owned entity is never
        // drawn, so it must not force a qualified label onto the only box of that name.
        var collides = model.Entities.Count(e => e.Name == entity.Name && !IsInlined(e, model, options)) > 1;
        return collides ? $"{OwnerNameOf(entity, model)}_{entity.NavigationName}" : entity.Name;
    }

    /// <summary>Returns the CLR name of an owned entity's owner, resolved by key; falls back to the raw key.</summary>
    /// <param name="entity">The owned entity.</param>
    /// <param name="model">The model.</param>
    private static string OwnerNameOf(EfEntity entity, EfModel model)
        => model.Entities.FirstOrDefault(e => e.EffectiveKey == entity.OwnerEntity)?.Name
           ?? entity.OwnerEntity
           ?? "";
```

Update `Render` to thread `options` into both calls:

```csharp
        RenderEntities(model, sb, options);
        RenderRelationships(model, sb, options);
```

Add `Rename` to `EfPropertyFactory` (`src/ProjGraph.Lib.EntityFramework/Infrastructure/EfPropertyFactory.cs`) — `CopyWith` cannot change `Name`:

```csharp
    /// <summary>
    /// Creates a copy of a property under a different name, preserving every other facet. Used to apply
    /// EF's <c>Nav_Property</c> prefix when an owned type is inlined into its owner.
    /// </summary>
    /// <param name="source">The source property.</param>
    /// <param name="name">The new property name.</param>
    public static EfProperty Rename(EfProperty source, string name)
    {
        return new EfProperty
        {
            Name = name,
            Type = source.Type,
            IsPrimaryKey = source.IsPrimaryKey,
            IsForeignKey = source.IsForeignKey,
            IsRequired = source.IsRequired,
            IsValueType = source.IsValueType,
            IsExplicitlyRequired = source.IsExplicitlyRequired,
            MaxLength = source.MaxLength,
            Precision = source.Precision,
            Scale = source.Scale,
            DefaultValue = source.DefaultValue
        };
    }
```

`EfPropertyFactory` is `internal` and `MermaidErdRenderer` is in the same assembly, so no visibility change is needed.

Note: `EfPropertyFactory.Rename` preserves `IsPrimaryKey`. An owned type's shadow PK must not surface as an owner PK — Task 8 (snapshot path) is where shadow keys appear, and it strips them at capture time so the model never carries them. The renderer stays presentation-only.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dtk test ProjGraph.slnx`
Expected: PASS — three new tests pass; all goldens unchanged (no model produces owned entities yet); every existing `DiagramOptions` call site still compiles.

- [ ] **Step 6: Commit**

```bash
git add src/ProjGraph.Lib.Core/Abstractions/DiagramOptions.cs \
        src/ProjGraph.Lib.EntityFramework/Rendering/MermaidErdRenderer.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/EfPropertyFactory.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Rendering/OwnedTypeRenderingTests.cs
git commit -m "feat(ef): render owned types in MirrorEf mode

Adds DiagramOptions.ErdOwnedMode (MirrorEf default, Classic), following the
IncludePackages precedent for feature-specific render options. Owned entities
sharing their owner's table inline as Nav_Property columns; those on their own
table get a box plus a derived identifying relationship. Presentation lives
entirely in the renderer."
```

---

### Task 4: Renderer — Classic mode

Task 3's `IsInlined` already short-circuits on `ErdOwnedMode.Classic`, so Classic mode should work the moment the option exists. This task proves it, and is where it gets fixed if it does not.

**Files:**
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/Rendering/OwnedTypeRenderingTests.cs`
- Modify (only if the tests fail): `src/ProjGraph.Lib.EntityFramework/Rendering/MermaidErdRenderer.cs`

**Interfaces:**
- Consumes: Task 3's `ErdOwnedMode`, `DiagramOptions.ErdOwnedMode`, and renderer.
- Produces: nothing new — Classic mode is verified behaviour of Task 3's renderer.

- [ ] **Step 1: Write the failing test**

Append to `OwnedTypeRenderingTests.cs`:

```csharp
    [Fact]
    public void Classic_TableSplitOwnsOne_RendersBoxAndIdentifyingRelationship()
    {
        var model = ModelWithOwned("Orders", isCollection: false);

        var output = new MermaidErdRenderer().Render(
            model, new DiagramOptions(false, false, false, ErdOwnedMode.Classic));

        output.Should().Contain("Address {", "classic mode always gives an owned type its own box");
        output.Should().Contain("string ZipCode", "classic mode never prefixes columns");
        output.Should().Contain("Order ||--|| Address");
        output.Should().NotContain("ShipToAddress_ZipCode");
    }

    [Fact]
    public void Classic_OwnerDoesNotGainNavigationColumn()
    {
        var model = ModelWithOwned("Orders", isCollection: false);

        var output = new MermaidErdRenderer().Render(
            model, new DiagramOptions(false, false, false, ErdOwnedMode.Classic));

        var orderBlock = output.Split("Address {")[0];
        orderBlock.Should().NotContain("ShipToAddress",
            "the relationship line carries the navigation; the owner gets no reference column");
    }
```

- [ ] **Step 2: Run the tests**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "OwnedTypeRenderingTests"`

These tests are expected to PASS immediately: Task 3 built Classic mode's behaviour into `IsInlined` but never proved it. That is the point of this task — a characterization test for an untested branch.

If either test FAILS, that is a real Task 3 defect. Fix it in `MermaidErdRenderer` (do not weaken the test), then re-run. The likely culprits: `IsInlined` not short-circuiting before the owner lookup, or `DisplayName` collision-qualifying when it should not.

- [ ] **Step 3: Run the full suite**

Run: `dtk test ProjGraph.slnx`
Expected: PASS — no golden may move; nothing in this task changes MirrorEf behaviour.

- [ ] **Step 4: Commit**

```bash
git add tests/ProjGraph.Tests.Unit.EntityFramework/Rendering/OwnedTypeRenderingTests.cs \
        src/ProjGraph.Lib.EntityFramework/Rendering/MermaidErdRenderer.cs
git commit -m "test(ef): cover Classic owned-type ERD mode

Classic mode gives every owned type its own box with an identifying
relationship and never prefixes columns, regardless of table mapping."
```

---

### Task 5: Capture OwnsOne — DbContext path, builder-lambda form

**Files:**
- Create: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentOwnedTypeWalker.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs:37-44`
- Test: `tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentOwnedTypeWalkerTests.cs` (create)
- Golden: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-owned-join.mmd` (regenerate)

**Interfaces:**
- Consumes: Task 1's scope-relative `FluentSyntax`/`FluentPropertyWalker`; Task 2's `EfEntity` fields and `EfEntityFactory`; Task 3's renderer.
- Produces: `FluentOwnedTypeWalker.Apply(SyntaxNode scope, Dictionary<string, EfEntity> entities, EfModel model, Compilation compilation, string? ambientEntity = null)`.

- [ ] **Step 1: Write the failing test**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentOwnedTypeWalkerTests.cs`. Use the existing golden-fixture harness pattern (`EfGoldenRunner` shows the wiring) to analyze `OwnedAndJoinContext.cs`:

```csharp
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework.Infrastructure;

public sealed class FluentOwnedTypeWalkerTests
{
    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Golden", "fixtures", fileName);

    internal static EfModel Analyze(string fileName, string contextName)
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        var service = new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));
#pragma warning disable VSTHRD002 // Deliberate sync-over-async bridge, as in EfGoldenRunner.
        return service.AnalyzeContextAsync(FixturePath(fileName), contextName).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    [Fact]
    public void OwnsOne_LambdaForm_CapturesOwnedEntityWithOwnerMetadata()
    {
        var model = Analyze("OwnedAndJoinContext.cs", "OwnedAndJoinContext");

        var address = model.Entities.Should().ContainSingle(e => e.IsOwned).Subject;
        address.Name.Should().Be("Address");
        address.OwnerEntity.Should().Be("Customer");
        address.NavigationName.Should().Be("Address");
        address.IsCollection.Should().BeFalse();
        address.TableName.Should().Be("Customer",
            "OwnsOne without ToTable is table-splitting, so it maps to the owner's effective table");
    }

    [Fact]
    public void OwnsOne_LambdaForm_AppliesNestedPropertyConfigurationToOwnedNotOwner()
    {
        var model = Analyze("OwnedAndJoinContext.cs", "OwnedAndJoinContext");

        var address = model.Entities.Single(e => e.IsOwned);
        address.Properties.Should().ContainSingle(p => p.Name == "City")
            .Which.MaxLength.Should().Be(50);

        var customer = model.Entities.Single(e => e.Name == "Customer");
        customer.Properties.Should().NotContain(p => p.Name == "City",
            "nested owned config must never leak onto the owner");
        customer.Properties.Should().NotContain(p => p.Name == "Address",
            "the owned navigation is not a column");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FluentOwnedTypeWalkerTests"`
Expected: FAIL — `ContainSingle(e => e.IsOwned)` finds no owned entity; owned types are still dropped.

- [ ] **Step 3: Implement the walker (OwnsOne lambda form)**

Create `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentOwnedTypeWalker.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Captures EF Core owned types (<c>OwnsOne</c>/<c>OwnsMany</c>) from a configuring method, on both the
/// DbContext path (lambda navigations, e.g. <c>OwnsOne(o =&gt; o.ShipToAddress, b =&gt; ...)</c>) and the
/// snapshot path (string literals, e.g. <c>OwnsOne("Ns.Address", "ShipToAddress", b1 =&gt; ...)</c>).
/// The other walkers fence owned builders off so their configuration cannot leak onto the owner; this
/// walker is what then records the owned type, keyed <c>{Owner}.{Nav}</c>, with its effective table
/// resolved so the renderer alone decides inline-vs-box.
/// </summary>
internal static class FluentOwnedTypeWalker
{
    /// <summary>
    /// Captures every owned type configured within <paramref name="scope"/>.
    /// </summary>
    /// <param name="scope">The configuring method (or nested builder scope) to walk.</param>
    /// <param name="entities">The known entities, augmented in place with owned entities.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is augmented.</param>
    /// <param name="compilation">The compilation for owned-type symbol resolution.</param>
    /// <param name="ambientEntity">The owning entity to fall back to when the chain has no <c>Entity&lt;T&gt;()</c> call.</param>
    public static void Apply(
        SyntaxNode scope,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        string? ambientEntity = null)
    {
        foreach (var owns in FluentSyntax.FindConfigRoots(scope, EfAnalysisConstants.EfMethods.OwnsOne))
        {
            Capture(owns, entities, model, compilation, ambientEntity, isCollection: false);
        }

        foreach (var owns in FluentSyntax.FindConfigRoots(scope, EfAnalysisConstants.EfMethods.OwnsMany))
        {
            Capture(owns, entities, model, compilation, ambientEntity, isCollection: true);
        }
    }

    private static void Capture(
        InvocationExpressionSyntax owns,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        string? ambientEntity,
        bool isCollection)
    {
        var ownerKey = FluentSyntax.ResolveOwningEntity(owns, ambientEntity);
        if (ownerKey is null || !entities.TryGetValue(ownerKey, out var owner))
        {
            return;
        }

        var navigation = NavigationName(owns);
        if (string.IsNullOrEmpty(navigation))
        {
            return;
        }

        var key = $"{ownerKey}.{navigation}";
        var owned = GetOrCreateOwned(key, owns, owner, navigation, isCollection, entities, model, compilation);

        // The owned builder's own configuration: either a builder lambda argument, or calls chained onto
        // the OwnsOne invocation. Both are walked with the owned entity as the ambient target.
        FluentPropertyWalker.Apply(owns.ArgumentList, entities, compilation, key);
        FluentEntityWalker.Apply(owns.ArgumentList, entities, model, compilation, key);

        ResolveEffectiveTable(key, owner, entities, model);
    }

    /// <summary>
    /// Returns the navigation name from an owned-type call: the lambda member access
    /// (<c>o =&gt; o.ShipToAddress</c>) on the DbContext path, or the second string literal
    /// (<c>OwnsOne("Ns.Address", "ShipToAddress", ...)</c>) on the snapshot path.
    /// </summary>
    /// <param name="owns">The <c>OwnsOne</c>/<c>OwnsMany</c> invocation.</param>
    private static string? NavigationName(InvocationExpressionSyntax owns)
    {
        var args = owns.ArgumentList.Arguments;
        if (args.Count == 0)
        {
            return null;
        }

        var stringLiterals = args
            .Select(a => a.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression))
            .ToList();

        // Snapshot form: OwnsOne("Ns.Address", "ShipToAddress", b1 => ...) — the nav is the second literal.
        if (stringLiterals.Count >= 2)
        {
            return stringLiterals[1].Token.ValueText;
        }

        return args[0].Expression switch
        {
            SimpleLambdaExpressionSyntax { Body: MemberAccessExpressionSyntax ma } => ma.Name.Identifier.Text,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1, Body: MemberAccessExpressionSyntax ma }
                => ma.Name.Identifier.Text,
            _ => null
        };
    }

    /// <summary>
    /// Returns the owned entity for <paramref name="key"/>, creating it on first sight. Repeated calls
    /// targeting the same navigation (as in the chained form spread across statements) merge into one
    /// entity rather than duplicating it.
    /// </summary>
    private static EfEntity GetOrCreateOwned(
        string key,
        InvocationExpressionSyntax owns,
        EfEntity owner,
        string navigation,
        bool isCollection,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        if (entities.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var ownedType = ResolveOwnedType(owns, owner, navigation, isCollection, compilation);

        // Seed scalar columns from the CLR type where resolvable. The snapshot path declares every
        // Property<T> in the block instead, and unresolvable types degrade to a bare entity rather than
        // throwing — the same graceful fallback FluentSyntax.MaterializeEntity uses.
        var seeded = ownedType is not null ? EntityAnalyzer.AnalyzeEntity(ownedType) : null;

        var owned = new EfEntity
        {
            Name = ownedType?.Name ?? navigation,
            Key = key,
            IsOwned = true,
            OwnerEntity = owner.EffectiveKey,
            NavigationName = navigation,
            IsCollection = isCollection
        };

        foreach (var property in seeded?.Properties ?? [])
        {
            owned.Properties.Add(property);
        }

        entities[key] = owned;
        model.Entities.Add(owned);
        return owned;
    }

    /// <summary>
    /// Resolves the owned CLR type: from the explicit type-name literal on the snapshot path, else from the
    /// owner symbol's navigation property (unwrapping the collection element type for <c>OwnsMany</c>).
    /// </summary>
    private static INamedTypeSymbol? ResolveOwnedType(
        InvocationExpressionSyntax owns,
        EfEntity owner,
        string navigation,
        bool isCollection,
        Compilation compilation)
    {
        // Snapshot form: the first string literal is the owned type's (namespace-qualified) name.
        var firstLiteral = owns.ArgumentList.Arguments
            .Select(a => a.Expression)
            .OfType<LiteralExpressionSyntax>()
            .FirstOrDefault(l => l.IsKind(SyntaxKind.StringLiteralExpression));

        if (firstLiteral is not null && owns.ArgumentList.Arguments.Count >= 2)
        {
            var typeName = FluentSyntax.LastSegment(firstLiteral.Token.ValueText);
            return compilation.GetSymbolsWithName(typeName, SymbolFilter.Type)
                .OfType<INamedTypeSymbol>()
                .FirstOrDefault();
        }

        // Explicit generic form: OwnsOne<Address>(...).
        var generic = FluentSyntax.GenericTypeArgumentName(owns);
        if (generic is not null)
        {
            return compilation.GetSymbolsWithName(generic, SymbolFilter.Type)
                .OfType<INamedTypeSymbol>()
                .FirstOrDefault();
        }

        // Lambda form: take the owner symbol's navigation property type.
        var ownerSymbol = compilation.GetSymbolsWithName(owner.Name, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault();

        var navProperty = ownerSymbol?.GetMembers(navigation).OfType<IPropertySymbol>().FirstOrDefault();
        if (navProperty?.Type is not INamedTypeSymbol navType)
        {
            return null;
        }

        return isCollection && navType.TypeArguments.FirstOrDefault() is INamedTypeSymbol element
            ? element
            : navType;
    }

    /// <summary>
    /// Resolves the owned entity's effective table when its builder declared no <c>ToTable</c>:
    /// <c>OwnsOne</c> shares the owner's table (EF table-splitting); <c>OwnsMany</c> gets EF's default
    /// <c>{OwnerTable}_{Nav}</c>, which never equals the owner's, so it always renders as its own box.
    /// </summary>
    private static void ResolveEffectiveTable(
        string key,
        EfEntity owner,
        Dictionary<string, EfEntity> entities,
        EfModel model)
    {
        var owned = entities[key];
        if (!string.IsNullOrEmpty(owned.TableName))
        {
            return;
        }

        var ownerTable = string.IsNullOrEmpty(owner.TableName) ? owner.Name : owner.TableName;
        var table = owned.IsCollection ? $"{ownerTable}_{owned.NavigationName}" : ownerTable;

        var updated = EfEntityFactory.CopyWith(owned, table);
        entities[key] = updated;

        var index = model.Entities.IndexOf(owned);
        if (index >= 0)
        {
            model.Entities[index] = updated;
        }
    }
}
```

Wire it into `FluentApiConfigurationParser.ApplyFluentApiConstraints`, after `FluentEntityWalker.Apply` (which must have materialized owners and applied their `ToTable` first, so owner-table resolution is correct) and before the property walker:

```csharp
        FluentEntityWalker.Apply(methodSyntax, entities, model, compilation);
        FluentOwnedTypeWalker.Apply(methodSyntax, entities, model, compilation);
        FluentPropertyWalker.Apply(methodSyntax, entities, compilation);
        FluentRelationshipWalker.Apply(methodSyntax, entities, model, compilation);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FluentOwnedTypeWalkerTests"`
Expected: PASS — both tests pass.

- [ ] **Step 5: Regenerate and review the affected golden**

Run: `UPDATE_EF_GOLDENS=1 dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "Category=Golden"`
Then: `git diff tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/`

Expected: only `fixture-owned-join.mmd` changes — `Customer` gains `string Address_City "max:50"`, and no `Address` box appears (table-split `OwnsOne` inlines). If any other golden moved, stop and investigate: it means owned capture leaked into an unrelated model.

- [ ] **Step 6: Run the full suite**

Run: `dtk test ProjGraph.slnx`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentOwnedTypeWalker.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentOwnedTypeWalkerTests.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-owned-join.mmd
git commit -m "feat(ef): capture OwnsOne owned types on the DbContext path

FluentOwnedTypeWalker records owned types keyed {Owner}.{Nav} with their
effective table resolved, so table-split OwnsOne now inlines onto the owner
as Nav_Property instead of being dropped."
```

---

### Task 6: Chained form + repeated-call merge

`ChainedOwnedContext` configures one owned type across two statements with no builder lambda:
`OwnsOne(c => c.Address).Property(a => a.City).HasMaxLength(50)` and `OwnsOne(c => c.Address).ToTable("ShopperAddresses")`. Task 5 scopes capture to `owns.ArgumentList`, which misses configuration *chained onto* the call. Both statements must fold into one owned entity.

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentSyntax.cs:74-107`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentOwnedTypeWalkerTests.cs`
- Golden: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-chained-owned.mmd` (regenerate)

**Interfaces:**
- Consumes: Task 5's `FluentOwnedTypeWalker`.
- Produces: `FluentSyntax.ResolveOwningEntity` returning the owned entity's `{Owner}.{Nav}` key (instead of `null`) when a chain crosses an `OwnsOne`/`OwnsMany` fence. `UsingEntity` still returns `null`.

- [ ] **Step 1: Write the failing test**

Append to `FluentOwnedTypeWalkerTests.cs`:

```csharp
    [Fact]
    public void OwnsOne_ChainedForm_MergesRepeatedCallsIntoOneOwnedEntity()
    {
        var model = Analyze("ChainedOwnedContext.cs", "ChainedOwnedContext");

        var owned = model.Entities.Should().ContainSingle(e => e.IsOwned).Subject;
        owned.Name.Should().Be("PostalAddress");
        owned.OwnerEntity.Should().Be("Shopper");
        owned.NavigationName.Should().Be("Address");
        owned.TableName.Should().Be("ShopperAddresses",
            "the chained ToTable configures the owned type, not the owner");
        owned.Properties.Should().ContainSingle(p => p.Name == "City")
            .Which.MaxLength.Should().Be(50);
    }

    [Fact]
    public void OwnsOne_ChainedForm_DoesNotLeakOntoOwner()
    {
        var model = Analyze("ChainedOwnedContext.cs", "ChainedOwnedContext");

        var shopper = model.Entities.Single(e => e.Name == "Shopper");
        shopper.Properties.Should().NotContain(p => p.Name == "City");
        shopper.TableName.Should().BeEmpty("ToTable(\"ShopperAddresses\") targets the owned type");
        shopper.Properties.Should().Contain(p => p.Name == "Tags",
            "the EF 8+ primitive collection must survive as a scalar column");
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FluentOwnedTypeWalkerTests"`
Expected: FAIL — `PostalAddress` has no `City` (chained config isn't in the argument list) and `TableName` is `Shopper`, not `ShopperAddresses`.

- [ ] **Step 3: Resolve chained config to the owned entity**

In `FluentSyntax.ResolveOwningEntity`, replace the fence branch that returns `null`. Instead of stopping, resolve to the owned type's key so chained configuration lands on it:

```csharp
            // A chained owned-type builder (e.g. Entity<T>().OwnsOne(o => o.Nav).Property(...)) configures
            // the owned type, not the owner. Resolve to the owned entity's {Owner}.{Nav} key so its
            // configuration lands there — and never on the outer entity the receiver chain reaches.
            if (callName is EfAnalysisConstants.EfMethods.OwnsOne or EfAnalysisConstants.EfMethods.OwnsMany)
            {
                var ownerKey = ResolveOwningEntity(receiver, ambientEntity);
                var navigation = OwnedNavigationName(receiver);
                return ownerKey is not null && navigation is not null ? $"{ownerKey}.{navigation}" : null;
            }

            // A join-entity builder (UsingEntity) is out of scope: stop rather than leak onto the owner.
            if (callName == EfAnalysisConstants.EfMethods.UsingEntity)
            {
                return null;
            }
```

`NestedBuilderScopes` stays intact and unchanged — `FindConfigRoots` still fences these calls off from the outer walk, which is what keeps `City` off `Shopper`. Only the *resolution* of an already-fenced chain changes.

Move the navigation-name extraction into `FluentSyntax` so both it and `FluentOwnedTypeWalker` share one implementation, and have `FluentOwnedTypeWalker.NavigationName` delegate to it:

```csharp
    /// <summary>
    /// Returns the navigation name configured by an <c>OwnsOne</c>/<c>OwnsMany</c> invocation: the lambda
    /// member access (<c>o =&gt; o.ShipToAddress</c>) on the DbContext path, or the second string literal
    /// (<c>OwnsOne("Ns.Address", "ShipToAddress", ...)</c>) on the snapshot path.
    /// </summary>
    /// <param name="owns">The owned-type invocation.</param>
    public static string? OwnedNavigationName(InvocationExpressionSyntax owns)
    {
        var args = owns.ArgumentList.Arguments;
        if (args.Count == 0)
        {
            return null;
        }

        var stringLiterals = args
            .Select(a => a.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression))
            .ToList();

        if (stringLiterals.Count >= 2)
        {
            return stringLiterals[1].Token.ValueText;
        }

        return args[0].Expression switch
        {
            SimpleLambdaExpressionSyntax { Body: MemberAccessExpressionSyntax ma } => ma.Name.Identifier.Text,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1, Body: MemberAccessExpressionSyntax ma }
                => ma.Name.Identifier.Text,
            _ => null
        };
    }
```

Delete the private `NavigationName` from `FluentOwnedTypeWalker` and call `FluentSyntax.OwnedNavigationName(owns)` at both of its use sites (in `Capture`).

In `FluentOwnedTypeWalker.Capture`, the chained `ToTable`/`Property` calls are now resolved to the owned key by the *outer* walkers' passes, so scoping to `owns.ArgumentList` covers only the lambda form. Both are needed. Because the outer `FluentPropertyWalker.Apply(methodSyntax, ...)` pass already resolves chained calls to `{Owner}.{Nav}`, the entity must exist in the dictionary *before* that pass runs — which the Task 5 ordering (`FluentOwnedTypeWalker` before `FluentPropertyWalker`) already guarantees.

`ResolveEffectiveTable` runs inside `Capture`, i.e. before the outer property/entity passes apply the chained `ToTable`. Move the call so it runs after them: in `FluentApiConfigurationParser`, add a finalizing pass after `FluentEntityWalker`:

```csharp
        FluentEntityWalker.Apply(methodSyntax, entities, model, compilation);
        FluentOwnedTypeWalker.Apply(methodSyntax, entities, model, compilation);
        FluentPropertyWalker.Apply(methodSyntax, entities, compilation);
        FluentEntityWalker.Apply(methodSyntax, entities, model, compilation);
        FluentOwnedTypeWalker.ResolveTables(entities, model);
        FluentRelationshipWalker.Apply(methodSyntax, entities, model, compilation);
```

The second `FluentEntityWalker.Apply` pass applies `ToTable` calls whose owning entity is an owned type that did not exist during the first pass. It is idempotent: materialization is a no-op for known entities and `ToTable` rewrites are the same value twice.

In `FluentOwnedTypeWalker`, remove the `ResolveEffectiveTable(key, owner, entities, model)` call from `Capture` and expose a finalizing pass instead:

```csharp
    /// <summary>
    /// Resolves the effective table of every captured owned type that declared no <c>ToTable</c>. Runs after
    /// all configuration passes so a chained <c>ToTable</c> is already applied and is not overwritten.
    /// </summary>
    /// <param name="entities">The known entities.</param>
    /// <param name="model">The model whose entities are updated in place.</param>
    public static void ResolveTables(Dictionary<string, EfEntity> entities, EfModel model)
    {
        foreach (var (key, owned) in entities.Where(e => e.Value.IsOwned).ToList())
        {
            if (!string.IsNullOrEmpty(owned.TableName))
            {
                continue;
            }

            if (owned.OwnerEntity is null || !entities.TryGetValue(owned.OwnerEntity, out var owner))
            {
                continue;
            }

            var ownerTable = string.IsNullOrEmpty(owner.TableName) ? owner.Name : owner.TableName;
            var table = owned.IsCollection ? $"{ownerTable}_{owned.NavigationName}" : ownerTable;

            var updated = EfEntityFactory.CopyWith(owned, table);
            entities[key] = updated;

            var index = model.Entities.IndexOf(owned);
            if (index >= 0)
            {
                model.Entities[index] = updated;
            }
        }
    }
```

Delete the now-unused private `ResolveEffectiveTable`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FluentOwnedTypeWalkerTests"`
Expected: PASS — all four tests pass.

- [ ] **Step 5: Regenerate and review goldens**

Run: `UPDATE_EF_GOLDENS=1 dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "Category=Golden"`
Then: `git diff tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/`

Expected: `fixture-chained-owned.mmd` gains a `PostalAddress` box (`City "max:50"`, `Country`) and a `Shopper ||--|| PostalAddress : "Address"` line, because `ToTable("ShopperAddresses")` puts it on its own table. `Shopper` still shows only `Id`/`Name`/`Tags` — the no-leak guarantee the fixture was written for. No other golden may move.

- [ ] **Step 6: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentSyntax.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentOwnedTypeWalker.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentOwnedTypeWalkerTests.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-chained-owned.mmd
git commit -m "feat(ef): capture chained-form owned types

A chain crossing an OwnsOne/OwnsMany fence now resolves to the owned type's
{Owner}.{Nav} key instead of stopping, so chained Property/ToTable calls
configure the owned type. Repeated calls merge into one entity. UsingEntity
still stops. The fence itself is unchanged."
```

---

### Task 7: OwnsMany + nested ownership

**Files:**
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentOwnedTypeWalkerTests.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/OwnedModesContext.cs`

**Interfaces:**
- Consumes: Tasks 5–6.
- Produces: `OwnedModesContext` fixture covering table-split `OwnsOne`, `OwnsOne` + `ToTable`, `OwnsMany`, and nested `OwnsOne`-within-`OwnsOne`.

- [ ] **Step 1: Write the fixture**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/OwnedModesContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
namespace Fixtures;

// Golden fixture covering every owned-type shape in one model, rendered in BOTH ERD modes:
//   ShipTo   - OwnsOne, no ToTable  -> table-split, inlines in MirrorEf
//   BillTo   - OwnsOne + ToTable    -> own table, a box in both modes
//   Lines    - OwnsMany             -> own table, a box in both modes
//   ShipTo.Geo - OwnsOne nested in an owned builder -> compounding prefixes
public class OwnedModesContext : DbContext
{
    public DbSet<Invoice> Invoices { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(e =>
        {
            e.OwnsOne(i => i.ShipTo, a =>
            {
                a.Property(p => p.Street).IsRequired().HasMaxLength(180);
                a.Property(p => p.ZipCode).HasMaxLength(18);
                a.OwnsOne(p => p.Geo, g => g.Property(x => x.Latitude).HasPrecision(9, 6));
            });

            e.OwnsOne(i => i.BillTo, a =>
            {
                a.Property(p => p.Street).HasMaxLength(180);
                a.ToTable("BillingAddresses");
            });

            e.OwnsMany(i => i.Lines, l =>
            {
                l.Property(p => p.Description).IsRequired().HasMaxLength(240);
                l.Property(p => p.Amount).HasPrecision(18, 2);
            });
        });
    }
}

public class Invoice
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public InvoiceAddress ShipTo { get; set; } = null!;
    public InvoiceAddress BillTo { get; set; } = null!;
    public List<InvoiceLine> Lines { get; set; } = [];
}

public class InvoiceAddress
{
    public string Street { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public GeoPoint Geo { get; set; } = null!;
}

public class GeoPoint
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

public class InvoiceLine
{
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}
```

Register it for copy-to-output the same way the existing fixtures are (check `tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj` — if fixtures are matched by a wildcard `None Include` on `Golden/fixtures/**`, no change is needed; if listed individually, add this file).

- [ ] **Step 2: Write the failing tests**

Append to `FluentOwnedTypeWalkerTests.cs`:

```csharp
    [Fact]
    public void OwnsMany_CapturesCollectionOwnedTypeOnItsOwnTable()
    {
        var model = Analyze("OwnedModesContext.cs", "OwnedModesContext");

        var lines = model.Entities.Single(e => e.NavigationName == "Lines");
        lines.Name.Should().Be("InvoiceLine");
        lines.IsOwned.Should().BeTrue();
        lines.IsCollection.Should().BeTrue();
        lines.OwnerEntity.Should().Be("Invoice");
        lines.TableName.Should().Be("Invoice_Lines",
            "an owned collection never shares the owner's table; EF's default is {OwnerTable}_{Nav}");
        lines.Properties.Should().ContainSingle(p => p.Name == "Amount")
            .Which.Precision.Should().Be(18);
    }

    [Fact]
    public void OwnsOne_WithToTable_GetsItsOwnTable()
    {
        var model = Analyze("OwnedModesContext.cs", "OwnedModesContext");

        var billTo = model.Entities.Single(e => e.NavigationName == "BillTo");
        billTo.TableName.Should().Be("BillingAddresses");
        billTo.IsCollection.Should().BeFalse();
    }

    [Fact]
    public void OwnsOne_WithoutToTable_SharesOwnerTable()
    {
        var model = Analyze("OwnedModesContext.cs", "OwnedModesContext");

        var shipTo = model.Entities.Single(e => e.NavigationName == "ShipTo");
        shipTo.TableName.Should().Be("Invoice", "table-splitting maps the owned type to the owner's table");
        shipTo.Properties.Should().ContainSingle(p => p.Name == "Street")
            .Which.MaxLength.Should().Be(180);
    }

    [Fact]
    public void OwnsOne_NestedInOwnedBuilder_IsOwnedByTheOwnedType()
    {
        var model = Analyze("OwnedModesContext.cs", "OwnedModesContext");

        var geo = model.Entities.Single(e => e.NavigationName == "Geo");
        geo.IsOwned.Should().BeTrue();
        geo.OwnerEntity.Should().Be("Invoice.ShipTo",
            "nested ownership chains through the owned type's KEY, not its CLR name — ShipTo and BillTo " +
            "are both InvoiceAddress, so a name-keyed owner would attach Geo to both");
        geo.Properties.Should().ContainSingle(p => p.Name == "Latitude")
            .Which.Precision.Should().Be(9);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FluentOwnedTypeWalkerTests"`
Expected: FAIL on the nested case — `FindConfigRoots(methodSyntax, "OwnsOne")` fences off the nested `OwnsOne` inside the `ShipTo` builder, so `Geo` is never captured.

- [ ] **Step 4: Recurse into owned builders**

In `FluentOwnedTypeWalker.Capture`, after applying the owned builder's own configuration, recurse so owned-within-owned is captured with the owned entity as ambient:

```csharp
        FluentPropertyWalker.Apply(owns.ArgumentList, entities, compilation, key);
        FluentEntityWalker.Apply(owns.ArgumentList, entities, model, compilation, key);
        Apply(owns.ArgumentList, entities, model, compilation, key);
```

`FindConfigRoots(owns.ArgumentList, ...)` is scope-relative (Task 1), so the nested `OwnsOne` is found relative to this scope while any fence deeper still excludes its children — the recursion handles those in turn.

`GetOrCreateOwned` sets `OwnerEntity = owner.Name`. For the nested case the ambient key is `Invoice.ShipTo`, whose `entities[...]` entry has `Name = "InvoiceAddress"` — so `geo.OwnerEntity` is `InvoiceAddress`, matching the test and the renderer's `e.OwnerEntity == entity.Name` lookup.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dtk test ProjGraph.slnx`
Expected: PASS — all `FluentOwnedTypeWalkerTests` pass and no existing golden moves (`OwnedModesContext` has no golden registered yet; that is Task 9).

- [ ] **Step 6: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentOwnedTypeWalker.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentOwnedTypeWalkerTests.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/OwnedModesContext.cs
git commit -m "feat(ef): capture OwnsMany and nested owned types

Owned builders recurse, so OwnsOne nested inside an owned builder is owned
by the owned type and its column prefixes compound as EF's do."
```

---

### Task 8: Snapshot path

Snapshots use string literals, declare every `Property<T>` in the block, always emit an explicit `ToTable`, and add `WithOwner().HasForeignKey(...)` plus a shadow PK. `FluentOwnedTypeWalker` already handles the string-literal form (Tasks 5–6). What is missing: wiring it into `ModelSnapshotParser`, and stripping the shadow PK so it never reaches the renderer.

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/ModelSnapshotParser.cs:41-43`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentOwnedTypeWalker.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/OwnedSnapshot.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs`
- Create golden: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-owned-snapshot.mmd`

**Interfaces:**
- Consumes: Tasks 5–7.
- Produces: snapshot-path owned capture; `FluentOwnedTypeWalker.ResolveTables` unchanged (snapshots always declare `ToTable`, so it no-ops).

- [ ] **Step 1: Write the snapshot fixture**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/OwnedSnapshot.cs`, modelled on real `dotnet ef migrations` output:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fixtures;

// Golden fixture for the snapshot path's owned-type shape: OwnsOne("Type", "nav", b1 => {...}) with the
// explicit ToTable, WithOwner/HasForeignKey and shadow key that `dotnet ef` always generates.
// ShipToAddress maps to the owner's table (table-splitting); Notes maps to its own (OwnsMany).
[DbContext(typeof(BillingContext))]
public class BillingContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("Fixtures.Receipt", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd();
            b.Property<string>("Reference").IsRequired().HasMaxLength(64);
            b.HasKey("Id");
            b.ToTable("Receipts");

            b.OwnsOne("Fixtures.ReceiptAddress", "ShipToAddress", b1 =>
            {
                b1.Property<int>("ReceiptId");
                b1.Property<string>("City").IsRequired().HasMaxLength(100);
                b1.Property<string>("ZipCode").HasMaxLength(18);
                b1.HasKey("ReceiptId");
                b1.ToTable("Receipts");
                b1.WithOwner().HasForeignKey("ReceiptId");
            });

            b.OwnsMany("Fixtures.ReceiptNote", "Notes", b1 =>
            {
                b1.Property<int>("Id").ValueGeneratedOnAdd();
                b1.Property<int>("ReceiptId");
                b1.Property<string>("Text").HasMaxLength(500);
                b1.HasKey("Id");
                b1.ToTable("ReceiptNotes");
                b1.WithOwner().HasForeignKey("ReceiptId");
            });
        });
    }
}
```

- [ ] **Step 2: Write the failing test**

Append to `FluentOwnedTypeWalkerTests.cs`:

```csharp
    internal static EfModel AnalyzeSnapshot(string fileName, string snapshotName)
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        var service = new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));
#pragma warning disable VSTHRD002 // Deliberate sync-over-async bridge, as in EfGoldenRunner.
        return service.AnalyzeSnapshotAsync(FixturePath(fileName), snapshotName).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    [Fact]
    public void Snapshot_OwnsOne_CapturesOwnedTypeFromStringLiteralForm()
    {
        var model = AnalyzeSnapshot("OwnedSnapshot.cs", "BillingContextModelSnapshot");

        var shipTo = model.Entities.Single(e => e.NavigationName == "ShipToAddress");
        shipTo.Name.Should().Be("ReceiptAddress");
        shipTo.OwnerEntity.Should().Be("Receipt");
        shipTo.IsCollection.Should().BeFalse();
        shipTo.TableName.Should().Be("Receipts", "the snapshot's explicit ToTable matches the owner's");
        shipTo.Properties.Should().ContainSingle(p => p.Name == "City").Which.MaxLength.Should().Be(100);
    }

    [Fact]
    public void Snapshot_OwnedShadowKey_IsNotRecordedAsPrimaryKey()
    {
        var model = AnalyzeSnapshot("OwnedSnapshot.cs", "BillingContextModelSnapshot");

        var shipTo = model.Entities.Single(e => e.NavigationName == "ShipToAddress");
        shipTo.Properties.Should().NotContain(p => p.IsPrimaryKey,
            "an owned type's shadow key is an EF implementation detail, not a modelled column");
    }

    [Fact]
    public void Snapshot_TableSplitOwnedType_DropsTheOwnerForeignKeyColumn()
    {
        var model = AnalyzeSnapshot("OwnedSnapshot.cs", "BillingContextModelSnapshot");

        var shipTo = model.Entities.Single(e => e.NavigationName == "ShipToAddress");
        shipTo.Properties.Should().NotContain(p => p.Name == "ReceiptId",
            "a table-split owned type's FK is the owner's own PK column re-projected, not an extra " +
            "column; the DbContext path cannot see it at all, so keeping it would break cross-path parity");
        shipTo.Properties.Should().Contain(p => p.Name == "City");
    }

    [Fact]
    public void Snapshot_OwnsMany_CapturesCollectionOnItsOwnTableAndKeepsItsForeignKey()
    {
        var model = AnalyzeSnapshot("OwnedSnapshot.cs", "BillingContextModelSnapshot");

        var notes = model.Entities.Single(e => e.NavigationName == "Notes");
        notes.IsCollection.Should().BeTrue();
        notes.TableName.Should().Be("ReceiptNotes");
        notes.Properties.Should().ContainSingle(p => p.Name == "ReceiptId")
            .Which.IsForeignKey.Should().BeTrue(
                "an owned type on its own table has a real, separate FK column back to the owner");
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FluentOwnedTypeWalkerTests"`
Expected: FAIL — `ModelSnapshotParser` never calls the owned walker, so no owned entity exists.

- [ ] **Step 4: Wire the snapshot path and strip shadow keys**

In `ModelSnapshotParser.Parse`, mirror the context path's pass order exactly:

```csharp
        FluentEntityWalker.Apply(buildModelMethod, entities, model, compilation);
        FluentOwnedTypeWalker.Apply(buildModelMethod, entities, model, compilation);
        FluentPropertyWalker.Apply(buildModelMethod, entities, compilation);
        FluentEntityWalker.Apply(buildModelMethod, entities, model, compilation);
        FluentOwnedTypeWalker.ResolveTables(entities, model);
        FluentRelationshipWalker.Apply(buildModelMethod, entities, model, compilation);
        FluentOwnedTypeWalker.StripShadowKeys(entities, model);
```

Add to `FluentOwnedTypeWalker`:

```csharp
    /// <summary>
    /// Normalises the EF implementation details a snapshot's owned block declares. Two rules:
    /// <list type="bullet">
    /// <item>An owned type's key is a shadow property EF invents to make the owned row addressable. It is
    /// not part of the modelled schema, and surfacing it would put a spurious PK on the owner once the
    /// owned type is inlined — so PK markers are cleared.</item>
    /// <item>A table-split owned type's FK back to the owner IS the owner's own PK column re-projected,
    /// not an extra column — so it is dropped. The DbContext path cannot see that column at all, so
    /// keeping it would break cross-path parity. For an owned type on its own table
    /// (<c>OwnsMany</c>, <c>OwnsOne</c>+<c>ToTable</c>) the FK is a real, separate column and is kept.</item>
    /// </list>
    /// </summary>
    /// <param name="entities">The known entities.</param>
    /// <param name="model">The model whose entities are updated in place.</param>
    public static void StripShadowKeys(Dictionary<string, EfEntity> entities, EfModel model)
    {
        foreach (var (key, owned) in entities.Where(e => e.Value.IsOwned).ToList())
        {
            var sharesOwnerTable = owned.OwnerEntity is not null
                                   && entities.TryGetValue(owned.OwnerEntity, out var owner)
                                   && EffectiveTable(owner) == EffectiveTable(owned);

            var stripped = EfEntityFactory.CopyWith(owned);
            stripped.Properties.Clear();

            foreach (var property in owned.Properties)
            {
                if (sharesOwnerTable && property.IsForeignKey)
                {
                    continue;
                }

                stripped.Properties.Add(property.IsPrimaryKey
                    ? EfPropertyFactory.CopyWith(property, new EfPropertyOverrides { IsPrimaryKey = false })
                    : property);
            }

            entities[key] = stripped;

            var index = model.Entities.IndexOf(owned);
            if (index >= 0)
            {
                model.Entities[index] = stripped;
            }
        }
    }

    /// <summary>Returns an entity's effective table: its explicit table name, or its entity name when unmapped.</summary>
    /// <param name="entity">The entity.</param>
    private static string EffectiveTable(EfEntity entity)
        => string.IsNullOrEmpty(entity.TableName) ? entity.Name : entity.TableName;
```

Also call `FluentOwnedTypeWalker.StripShadowKeys(entities, model)` as the final line of `FluentApiConfigurationParser.ApplyFluentApiConstraints` (after `EntityConfigurationWalker.Apply`), so both paths agree — a context-path `HasKey` inside an owned builder must be treated identically.

`StripShadowKeys` keys the FK-drop off `IsForeignKey`, which the existing `FluentRelationshipWalker` sets from `WithOwner().HasForeignKey("ReceiptId")`. If `ReceiptId` is not marked `IsForeignKey` after that pass, mark it in `StripShadowKeys` by reading the `HasForeignKey` string literal from inside the owned builder instead. Do NOT add an `EfRelationship` for owned types — the spec forbids it, and the renderer derives the line.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "FluentOwnedTypeWalkerTests"`
Expected: PASS.

- [ ] **Step 6: Register the snapshot golden**

In `EfErdGoldenTests.cs`, add after `OneToOneSnapshotErd_MatchesGolden`:

```csharp
    [Fact]
    public void OwnedSnapshotErd_MatchesGolden()
    {
        var actual = EfGoldenRunner.RenderSnapshot(
            FixturePath("OwnedSnapshot.cs"), "BillingContextModelSnapshot");
        EfGoldenRunner.Verify("fixture-owned-snapshot", actual);
    }
```

Run: `UPDATE_EF_GOLDENS=1 dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "Category=Golden"`
Then: `git diff tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/`

Expected: new `fixture-owned-snapshot.mmd` where `Receipt` carries inlined `ShipToAddress_City` and `ShipToAddress_ZipCode` columns (but NOT `ShipToAddress_ReceiptId` — the table-split FK is dropped), plus a `ReceiptNote` box keeping its own `ReceiptId` FK column and a `Receipt ||--o{ ReceiptNote : "Notes"` line. No pre-existing golden may move.

- [ ] **Step 7: Run the full suite and commit**

Run: `dtk test ProjGraph.slnx`
Expected: PASS.

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/ModelSnapshotParser.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentOwnedTypeWalker.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/OwnedSnapshot.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/fixture-owned-snapshot.mmd \
        tests/ProjGraph.Tests.Unit.EntityFramework/Infrastructure/FluentOwnedTypeWalkerTests.cs
git commit -m "feat(ef): capture owned types on the snapshot path

Wires FluentOwnedTypeWalker into ModelSnapshotParser with the same pass
order as the context path, and strips owned shadow keys on both paths so an
EF implementation detail never surfaces as a column."
```

---

### Task 9: Dual-mode goldens + cross-path agreement

The cross-path test is the one that closes the drift gap the two-path design otherwise leaves open: no existing test pins the context and snapshot paths to agree, because their fixtures are unrelated models.

**Files:**
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/EfErdGoldenTests.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/CrossPathContext.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/CrossPathSnapshot.cs`
- Create goldens: `fixture-owned-modes-mirror.mmd`, `fixture-owned-modes-classic.mmd`

**Interfaces:**
- Consumes: Tasks 3–8.
- Produces: `EfGoldenRunner.RenderContext(string samplePath, string? contextName, ErdOwnedMode mode = ErdOwnedMode.MirrorEf)`.

- [ ] **Step 1: Add mode support to the golden runner**

In `EfGoldenRunner`, thread the mode through:

```csharp
    public static string RenderContext(string samplePath, string? contextName,
        ErdOwnedMode mode = ErdOwnedMode.MirrorEf)
    {
        var service = CreateService();

#pragma warning disable VSTHRD002 // Deliberate sync-over-async bridge: harness API is pinned to a synchronous
        // signature (see task brief); no SynchronizationContext deadlock risk under xUnit.
        var model = service.AnalyzeContextAsync(samplePath, contextName).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        return Render(model, mode);
    }

    private static string Render(EfModel model, ErdOwnedMode mode = ErdOwnedMode.MirrorEf)
        => Normalize(new MermaidErdRenderer().Render(model, new DiagramOptions(true, false, false, mode)));
```

Update `RenderSnapshot`'s `Render(model)` call to `Render(model, mode)` with the same optional parameter.

- [ ] **Step 2: Write the dual-mode golden tests**

In `EfErdGoldenTests.cs`, add:

```csharp
    [Fact]
    public void OwnedModesErd_MirrorEf_MatchesGolden()
    {
        var actual = EfGoldenRunner.RenderContext(
            FixturePath("OwnedModesContext.cs"), "OwnedModesContext", ErdOwnedMode.MirrorEf);
        EfGoldenRunner.Verify("fixture-owned-modes-mirror", actual);
    }

    [Fact]
    public void OwnedModesErd_Classic_MatchesGolden()
    {
        var actual = EfGoldenRunner.RenderContext(
            FixturePath("OwnedModesContext.cs"), "OwnedModesContext", ErdOwnedMode.Classic);
        EfGoldenRunner.Verify("fixture-owned-modes-classic", actual);
    }
```

- [ ] **Step 3: Write the cross-path fixtures**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/CrossPathContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
namespace Fixtures;

// One model, expressed twice: here as a DbContext, and in CrossPathSnapshot.cs as the snapshot EF would
// generate for it. The cross-path test asserts both render to the same ERD — the DbContext path infers
// table-splitting from the ABSENCE of ToTable while the snapshot path reads an explicit one, so this is
// what stops the two detections from drifting apart.
public class CrossPathContext : DbContext
{
    public DbSet<Ticket> Tickets { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(e =>
        {
            e.ToTable("Tickets");
            e.Property(t => t.Code).IsRequired().HasMaxLength(32);
            e.OwnsOne(t => t.Seat, s =>
            {
                s.Property(p => p.Row).IsRequired().HasMaxLength(4);
                s.Property(p => p.Number).IsRequired();
            });
        });
    }
}

public class Ticket
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public SeatLocation Seat { get; set; } = null!;
}

public class SeatLocation
{
    public string Row { get; set; } = "";
    public int Number { get; set; }
}
```

Create `tests/ProjGraph.Tests.Unit.EntityFramework/Golden/fixtures/CrossPathSnapshot.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fixtures;

// The snapshot EF would generate for CrossPathContext. Kept byte-for-byte equivalent in meaning, not form:
// the owned block carries the explicit ToTable("Tickets") + shadow key + WithOwner that `dotnet ef` emits.
[DbContext(typeof(CrossPathContext))]
public class CrossPathContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("Fixtures.Ticket", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd();
            b.Property<string>("Code").IsRequired().HasMaxLength(32);
            b.HasKey("Id");
            b.ToTable("Tickets");

            b.OwnsOne("Fixtures.SeatLocation", "Seat", b1 =>
            {
                b1.Property<int>("TicketId");
                b1.Property<string>("Row").IsRequired().HasMaxLength(4);
                b1.Property<int>("Number").IsRequired();
                b1.HasKey("TicketId");
                b1.ToTable("Tickets");
                b1.WithOwner().HasForeignKey("TicketId");
            });
        });
    }
}
```

- [ ] **Step 4: Write the failing cross-path test**

In `EfErdGoldenTests.cs`, add:

```csharp
    [Fact]
    public void ContextAndSnapshotPaths_RenderTheSameErd_ForTheSameModel()
    {
        var fromContext = EfGoldenRunner.RenderContext(
            FixturePath("CrossPathContext.cs"), "CrossPathContext");
        var fromSnapshot = EfGoldenRunner.RenderSnapshot(
            FixturePath("CrossPathSnapshot.cs"), "CrossPathContextModelSnapshot");

        // Titles differ by construction (context name vs snapshot-derived name); compare the diagram body.
        static string Body(string mmd) => string.Join('\n',
            mmd.Split('\n').SkipWhile(l => !l.StartsWith("erDiagram", StringComparison.Ordinal)));

        Body(fromSnapshot).Should().Be(Body(fromContext),
            "the DbContext path infers table-splitting from the absence of ToTable while the snapshot " +
            "path reads an explicit one; both must resolve to the same recorded fact and render identically");
    }
```

Task 8's `StripShadowKeys` already drops the table-split FK (`TicketId`) and clears the shadow PK, which is exactly what makes this test passable — the snapshot declares both and the DbContext path can see neither. If this test fails, the difference it reports is the specification of the bug: read the two rendered bodies and fix the *capture* side that diverges. Do not special-case the renderer, and do not relax the assertion to a substring check.

- [ ] **Step 5: Run tests to verify they pass**

Run: `UPDATE_EF_GOLDENS=1 dtk test tests/ProjGraph.Tests.Unit.EntityFramework --filter "Category=Golden"`
Then: `git diff tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/`

Review both new mode goldens. Expected in `fixture-owned-modes-mirror.mmd`: `Invoice` carries `ShipTo_Street`, `ShipTo_ZipCode`, `ShipTo_Geo_Latitude`, `ShipTo_Geo_Longitude` (compounded nested prefix); `InvoiceAddress` box for `BillTo` on `BillingAddresses`; `InvoiceLine` box; `Invoice ||--|| InvoiceAddress` and `Invoice ||--o{ InvoiceLine` lines. Expected in `fixture-owned-modes-classic.mmd`: `Invoice` carries only `Id`/`Number`; separate boxes for both addresses (name-collision-qualified to `Invoice_ShipTo` / `Invoice_BillTo`, since both are `InvoiceAddress`), `GeoPoint`, and `InvoiceLine`.

Then: `dtk test ProjGraph.slnx`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add tests/ProjGraph.Tests.Unit.EntityFramework/Golden/
git commit -m "test(ef): pin both ERD owned modes and cross-path agreement

OwnedModesContext renders to mirror and classic goldens. CrossPathContext
and its snapshot assert both parser paths render the same ERD for the same
model, which nothing previously guarded."
```

---

### Task 10: CLI and MCP surfaces

**Files:**
- Modify: `src/ProjGraph.Cli/Commands/ErdCommand.cs`
- Modify: `src/ProjGraph.Mcp/ProjGraphTools.cs:301`
- Test: `tests/ProjGraph.Tests.Integration.Cli` (add to the existing ERD command test class)

**Interfaces:**
- Consumes: Task 3's `ErdOwnedMode` and `DiagramOptions.ErdOwnedMode`.
- Produces: `--owned-mode <mirror|classic>` CLI option; `ownedMode` MCP parameter. Both default to mirror.

- [ ] **Step 1: Write the failing CLI test**

In the existing ERD CLI integration test class, add a test asserting `--owned-mode classic` yields a separate owned box. Follow the class's established invocation pattern (locate it with `grep -rn "erd" tests/ProjGraph.Tests.Integration.Cli`), asserting on stdout:

```csharp
    [Fact]
    public async Task Erd_WithOwnedModeClassic_RendersOwnedTypeAsSeparateEntity()
    {
        var result = await RunAsync("erd", FixturePath("ChainedOwnedContext.cs"),
            "--owned-mode", "classic", "--show-title", "false");

        result.ExitCode.Should().Be(0);
        result.Output.Should().Contain("PostalAddress {");
        result.Output.Should().Contain("Shopper ||--|| PostalAddress");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dtk test tests/ProjGraph.Tests.Integration.Cli --filter "OwnedMode"`
Expected: FAIL — non-zero exit; Spectre reports the unknown option `--owned-mode`.

- [ ] **Step 3: Add the CLI option**

In `ErdCommand.Settings`, after `ShowTitle`, add the option. It is bound as a **string**, not as the `ErdOwnedMode` enum: Spectre binds enums by member name, so `--owned-mode mirror` would not bind to `MirrorEf`, and exposing `--owned-mode mirroref` to users is worse than converting by hand.

```csharp
        /// <summary>
        /// Gets or sets how EF Core owned types are represented in the diagram.
        /// </summary>
        [CommandOption("--owned-mode <mirror|classic>")]
        [Description(
            "How EF Core owned types are shown: 'mirror' inlines table-split owned types onto the owner as " +
            "EF names them (default), 'classic' gives every owned type its own entity")]
        [DefaultValue("mirror")]
        public string OwnedMode { get; init; } = "mirror";

        /// <summary>Gets the parsed owned-type render mode.</summary>
        internal ErdOwnedMode ResolvedOwnedMode =>
            OwnedMode.Equals("classic", StringComparison.OrdinalIgnoreCase)
                ? ErdOwnedMode.Classic
                : ErdOwnedMode.MirrorEf;
```

Extend the settings' existing `Validate()` override to reject anything else:

```csharp
            if (!OwnedMode.Equals("mirror", StringComparison.OrdinalIgnoreCase) &&
                !OwnedMode.Equals("classic", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Error($"Invalid --owned-mode '{OwnedMode}'. Expected 'mirror' or 'classic'.");
            }
```

At `ErdCommand.cs:138`, pass it through:

```csharp
                mermaidRenderer.Render(model, new DiagramOptions(
                    settings.ShowTitle, wrapInMarkdownFence, false, settings.ResolvedOwnedMode));
```

- [ ] **Step 4: Add the MCP parameter**

In `ProjGraphTools.cs`, add a parameter to the ERD tool method alongside `showTitle`, matching the file's existing `[Description]` convention:

```csharp
        [Description("How EF Core owned types are shown: 'mirror' (default) inlines table-split owned types onto the owner as EF names them; 'classic' gives every owned type its own entity")]
        string ownedMode = "mirror",
```

At line 301:

```csharp
        var mode = ownedMode.Equals("classic", StringComparison.OrdinalIgnoreCase)
            ? ErdOwnedMode.Classic
            : ErdOwnedMode.MirrorEf;
        var diagram = renderers.ErdRenderer.Render(model, new DiagramOptions(showTitle, false, false, mode));
```

Add `using ProjGraph.Lib.Core.Abstractions;` if not already present.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dtk test ProjGraph.slnx`
Expected: PASS — including `Tests.Contract`, which validates MCP tool schemas. If a contract test pins the ERD tool's parameter list, update its expectation to include `ownedMode`.

- [ ] **Step 6: Commit**

```bash
git add src/ProjGraph.Cli/Commands/ErdCommand.cs \
        src/ProjGraph.Mcp/ProjGraphTools.cs \
        tests/ProjGraph.Tests.Integration.Cli/
git commit -m "feat(cli,mcp): expose --owned-mode / ownedMode for ERDs

Both surfaces default to mirror."
```

---

### Task 11: Mermaid v11 validation

Every generated golden must parse with real Mermaid, not just match a string. `mermaid.parse` is the authority — Mermaid rejects constructs that look plausible (this is why `SanitizeEntityName` exists).

**Files:**
- No source changes expected. Fixes land in `MermaidErdRenderer.cs` if validation fails.

- [ ] **Step 1: Validate every changed and new golden**

Using the browser MCP tools, load Mermaid v11 from a CDN and call `mermaid.parse` on the contents of each of:

- `fixture-owned-join.mmd`
- `fixture-chained-owned.mmd`
- `fixture-owned-modes-mirror.mmd`
- `fixture-owned-modes-classic.mmd`
- `fixture-owned-snapshot.mmd`

For each, `mermaid.parse(text)` must resolve without throwing. Pay particular attention to:
- The relationship label: `Order ||--|| Address : "ShipToAddress"` — the existing renderer emits `: ""`, and a non-empty quoted label is new for this codebase.
- Collision-qualified box names (`Invoice_ShipTo`) — underscores are bare-identifier-safe, so `SanitizeEntityName` should leave them unquoted.
- Prefixed column names (`ShipTo_Geo_Latitude`).
- `decimal` columns rendering `precision(18,2)` inside a constraint comment.

- [ ] **Step 2: Fix any parse failures**

If `mermaid.parse` rejects a construct, fix it in `MermaidErdRenderer` (sanitization or label emission), regenerate the goldens, and re-validate. Do not adjust a golden by hand.

- [ ] **Step 3: Run the full suite**

Run: `dtk test ProjGraph.slnx`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "test(ef): validate owned-type goldens against Mermaid v11"
```

---

## Self-Review

**Spec coverage:**

| Spec section | Task(s) |
| --- | --- |
| `EfEntity` owned fields | 2 |
| Effective-table resolution rules (all four) | 5 (OwnsOne split), 6 (chained ToTable), 7 (OwnsMany default), 8 (snapshot explicit) |
| No synthesized `EfRelationship` | 3 (derived in renderer), 8 (explicitly forbidden) |
| Entity keying `{Owner}.{Nav}` | 5, 6 |
| Capture: DbContext path (steps 1–4) | 5, 6, 7 |
| Capture: nav suppression (step 5) | Verified pre-existing — asserted in Task 5's `AppliesNestedPropertyConfigurationToOwnedNotOwner` |
| Scope-relative traversal refactor | 1 |
| Capture: snapshot path | 8 |
| `ErdOwnedMode` option on `DiagramOptions` | 3 |
| Rendering: MirrorEf | 3 |
| Rendering: Classic | 3 (behaviour), 4 (tests) |
| Display naming / collisions | 3 (`DisplayName`), 9 (classic golden exercises it) |
| Surfaces (CLI, MCP) | 10 |
| Error handling (graceful degradation) | 5 (`GetOrCreateOwned` falls back to a bare entity) |
| Testing: goldens, dual mode, cross-path | 5, 6, 8, 9 |
| Mermaid v11 validation | 11 |
| Out of scope: `UsingEntity` | 6 (explicitly returns `null`) |

**Type consistency:** `FluentOwnedTypeWalker.Apply/ResolveTables/StripShadowKeys`, `EfEntityFactory.CopyWith`, `EfPropertyFactory.Rename`, `FluentSyntax.OwnedNavigationName`, `ErdOwnedMode.MirrorEf|Classic`, and `DiagramOptions`' fourth positional parameter are used consistently across Tasks 2–11.

**Pre-flight conflicts, resolved before execution (2026-07-16):**

Three defects the plan itself authored were found by the pre-flight scan and fixed rather than left for the review loop:

1. **Task 3 referenced `DiagramOptions.ErdOwnedMode` before Task 4 added it.** Resolved by folding the `DiagramOptions` edit into Task 3, which is its first consumer and cannot compile without it. Task 4 is now purely a characterization test for Classic mode — an untested branch of Task 3's `IsInlined`, which is why its tests are expected to pass on arrival rather than fail first.
2. **Task 8 mandated an FK assertion that Task 9 overturned.** The table-split rule (an owned type's FK back to its owner is the owner's own PK re-projected, so it is dropped; an owned type on its own table keeps a real FK column) is now stated once, correctly, in Task 8's `StripShadowKeys` and its tests. Task 9's cross-path test consumes that rule instead of discovering it.
3. **Task 10 carried two versions of the CLI option.** The dead enum-bound version is deleted; only the string-bound one remains, with the reason it is not enum-bound stated inline.
