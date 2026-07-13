# EF Rewrite — Slice 1: Relationships (Roslyn fluent-chain walker) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the regex-over-text relationship parser (`RelationshipConfigParser`) on the DbContext `OnModelCreating` path with a Roslyn syntax-tree walker (`FluentRelationshipWalker`) that folds each `modelBuilder.Entity<T>` fluent chain into `EfRelationship`s and foreign-key markings, producing byte-identical ERD output (gated by the Slice-0 golden files).

**Architecture:** A new `internal static FluentRelationshipWalker` enumerates the `InvocationExpressionSyntax` chains rooted at `Entity<T>(...)` inside `OnModelCreating`, and for each chain reads its `HasOne`/`HasMany` + `WithOne`/`WithMany` + `HasForeignKey` + `IsRequired` calls off the syntax spine — so "which entity does this call belong to" is answered by the receiver expression, not by scanning nearby text. It is wired into the context path only (`FluentApiConfigurationParser.ApplyFluentApiConstraints`); the snapshot path keeps `RelationshipConfigParser` until Slice 6. The walker reuses the existing pure helpers (`FluentApiParsingUtilities.GetOrCreateProperty`, `EfPropertyFactory`, `NavigationPropertyAnalyzer`, `RelationshipExtensions.GenerateKey`, and `RelationshipConfigParser.CreateShadowRelationship` for the has/with → relationship mapping) — it only avoids the regex. Reusing `CreateShadowRelationship` (rather than copying its switch) means Slice 6, which deletes `RelationshipConfigParser`, will relocate that one helper into the walker at that point.

**Tech Stack:** C# / .NET 10, Roslyn (`Microsoft.CodeAnalysis.CSharp.Syntax`), xUnit v3, FluentAssertions, `RoslynTestHelper`, `EfErdGoldenTests` (Slice 0).

## Global Constraints

- Target framework `net10.0`; build enforces `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true` — no warnings allowed.
- XML documentation is required on all public APIs. `FluentRelationshipWalker` is `internal`, but keep `<summary>` docs on it and its members for consistency with the surrounding files.
- Use `dtk dotnet build` / `dtk dotnet test` / `dtk dotnet format ProjGraph.slnx --verify-no-changes` (token-optimized wrapper) for all build/test/format commands.
- The Slice-0 golden files (`tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/*.mmd`) are the parity contract. A golden may only change with a reviewer-visible diff justified by a specific fixed finding. This slice targets **zero golden changes**.
- Do NOT touch the snapshot path (`ModelSnapshotParser`, `AnalyzeSnapshotAsync`) or `RelationshipConfigParser` itself in this slice. They are retired in Slice 6.
- Byte-exact behaviors to preserve (verified against source):
  - `CreateShadowRelationship` mapping — `HasOne/WithMany` **swaps** source↔target, OneToMany, default required `true`; `HasMany/WithOne` no swap, OneToMany, default `true`; `HasOne/WithOne` no swap, OneToOne, default `false`; `HasMany/WithMany` no swap, ManyToMany, default `false`; fallback swaps, OneToMany, default `true`.
  - Relationship dedup by `EfRelationship.GenerateKey()` before adding to `model.Relationships`.
  - FK dependent-entity selection: `HasForeignKey<T>` generic override wins; else `HasOne` → source entity, `HasMany` → target entity.
  - Calls inside a `UsingEntity(...)` invocation are ignored (parity with `IsInsideUsingEntityBlock`).
  - A `HasOne`/`HasMany` with **no** paired `WithOne`/`WithMany` in its own chain produces **no** relationship.

---

### Task 1: Walker foundation — entity-scoped chains + basic Has/With relationships

**Files:**
- Create: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentRelationshipWalkerTests.cs`

**Interfaces:**
- Consumes (existing, do not change): `EntityAnalyzer.AnalyzeEntity(INamedTypeSymbol)`, `RelationshipExtensions.GenerateKey(EfRelationship)`, `RelationshipConfigParser.CreateShadowRelationship(string, string, string, string, bool?)`, `EfAnalysisConstants.EfMethods.*`, `RoslynTestHelper.CreateCompilation`, `RoslynTestHelper.GetTypeSymbol`.
- Produces (later tasks rely on these exact members):
  - `internal static class FluentRelationshipWalker` with `public static void Apply(MethodDeclarationSyntax method, Dictionary<string, EfEntity> entities, EfModel model, Compilation compilation)`.
  - Private `FluentChain` nested type exposing `HasNode`, `Calls` (`IReadOnlyList<(string Name, InvocationExpressionSyntax Invocation)>`).

- [ ] **Step 1: Write the failing test**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/FluentRelationshipWalkerTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Unit tests for <see cref="FluentRelationshipWalker"/>: the Roslyn fluent-chain relationship
/// walker that replaces the regex <c>RelationshipConfigParser</c> on the DbContext path.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class FluentRelationshipWalkerTests
{
    /// <summary>
    /// Compiles <paramref name="source"/>, locates its OnModelCreating method, and builds the
    /// entities dictionary from the named entity classes so the walker can be driven in isolation.
    /// </summary>
    private static (MethodDeclarationSyntax Method, Compilation Compilation, Dictionary<string, EfEntity> Entities, EfModel Model)
        Build(string source, params string[] entityNames)
    {
        var compilation = RoslynTestHelper.CreateCompilation(source);
        var method = compilation.SyntaxTrees[0].GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "OnModelCreating");

        var entities = new Dictionary<string, EfEntity>();
        var model = new EfModel();
        foreach (var name in entityNames)
        {
            var symbol = RoslynTestHelper.GetTypeSymbol((CSharpCompilation)compilation, name)!;
            var entity = EntityAnalyzer.AnalyzeEntity(symbol);
            entities[name] = entity;
            model.Entities.Add(entity);
        }

        return (method, compilation, entities, model);
    }

    [Fact]
    public void Apply_HasManyWithOne_CreatesOneToManyWithoutSwap()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = []; }
            public class Post { public int Id { get; set; } public int BlogId { get; set; } public Blog Blog { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Blog>().HasMany(b => b.Posts).WithOne(p => p.Blog);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.SourceEntity.Should().Be("Blog");
        rel.TargetEntity.Should().Be("Post");
        rel.Type.Should().Be(EfRelationshipType.OneToMany);
        rel.IsRequired.Should().BeTrue();
    }

    [Fact]
    public void Apply_HasOneWithMany_SwapsSourceAndTarget()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = []; }
            public class Post { public int Id { get; set; } public Blog Blog { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Post>().HasOne(p => p.Blog).WithMany(b => b.Posts);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.SourceEntity.Should().Be("Blog");   // swapped: target of HasOne becomes source
        rel.TargetEntity.Should().Be("Post");
        rel.Type.Should().Be(EfRelationshipType.OneToMany);
    }

    [Fact]
    public void Apply_HasOneWithOne_CreatesOptionalOneToOne()
    {
        const string source = """
            public class Author { public int Id { get; set; } public Profile Profile { get; set; } = null!; }
            public class Profile { public int Id { get; set; } public Author Author { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Author>().HasOne(a => a.Profile).WithOne(p => p.Author);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Author", "Profile");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.Type.Should().Be(EfRelationshipType.OneToOne);
        rel.IsRequired.Should().BeFalse();      // convention default for 1:1
    }

    [Fact]
    public void Apply_HasOneWithoutWith_CreatesNoRelationship()
    {
        const string source = """
            public class Blog { public int Id { get; set; } public Author Owner { get; set; } = null!; }
            public class Author { public int Id { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Blog>().HasOne(b => b.Owner);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Author");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        model.Relationships.Should().BeEmpty();
    }

    [Fact]
    public void Apply_EntityLambdaForm_ResolvesReceiverEntity()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = []; }
            public class Post { public int Id { get; set; } public Blog Blog { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Blog>(e =>
                    {
                        e.HasMany(b => b.Posts).WithOne(p => p.Blog);
                    });
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.SourceEntity.Should().Be("Blog");
        rel.TargetEntity.Should().Be("Post");
        rel.Type.Should().Be(EfRelationshipType.OneToMany);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~FluentRelationshipWalkerTests"`
Expected: FAIL to compile — `FluentRelationshipWalker` does not exist yet.

- [ ] **Step 3: Create the walker with the foundation implementation**

Create `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using ProjGraph.Lib.EntityFramework.Infrastructure.Extensions;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Walks the Fluent API invocation chains of a configuring method (OnModelCreating) directly on the
/// C# syntax tree to discover entity relationships, replacing the text/regex based
/// <see cref="RelationshipConfigParser"/> for the DbContext path. The receiver expression of each
/// chain determines the owning entity, so configuration never leaks between unrelated statements.
/// </summary>
internal static class FluentRelationshipWalker
{
    /// <summary>
    /// Discovers relationships from every <c>HasOne</c>/<c>HasMany</c> chain in <paramref name="method"/>
    /// and adds the deduplicated results to <paramref name="model"/>.
    /// </summary>
    /// <param name="method">The <c>OnModelCreating</c> method declaration to walk.</param>
    /// <param name="entities">Entities already discovered from DbSets and fluent <c>.Entity&lt;T&gt;</c> calls.</param>
    /// <param name="model">The model whose <see cref="EfModel.Relationships"/> collection is populated.</param>
    /// <param name="compilation">The Roslyn compilation for semantic navigation-property resolution.</param>
    public static void Apply(
        MethodDeclarationSyntax method,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        var semanticModel = compilation.GetSemanticModel(method.SyntaxTree);
        var existingKeys = model.Relationships.Select(r => r.GenerateKey()).ToHashSet();

        foreach (var hasInvocation in FindRelationshipRoots(method))
        {
            var chain = new FluentChain(hasInvocation);

            var sourceEntity = ResolveSourceEntity(chain, entities);
            if (sourceEntity is null)
            {
                continue;
            }

            var relationship = BuildRelationship(chain, sourceEntity, entities, compilation, semanticModel);
            if (relationship is null)
            {
                continue;
            }

            if (existingKeys.Add(relationship.GenerateKey()))
            {
                model.Relationships.Add(relationship);
            }
        }
    }

    /// <summary>Finds every <c>HasOne</c>/<c>HasMany</c> invocation in the method (roots of relationship chains).</summary>
    /// <param name="method">The method to scan.</param>
    private static IEnumerable<InvocationExpressionSyntax> FindRelationshipRoots(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma
                          && SimpleName(ma.Name) is EfAnalysisConstants.EfMethods.HasOne
                              or EfAnalysisConstants.EfMethods.HasMany);
    }

    /// <summary>Resolves the entity that owns a fluent chain from its receiver expression.</summary>
    /// <param name="chain">The fluent chain.</param>
    /// <param name="entities">The known entities.</param>
    private static string? ResolveSourceEntity(FluentChain chain, Dictionary<string, EfEntity> entities)
    {
        // modelBuilder.Entity<T>().HasMany(...): the Entity call is part of this chain's spine.
        foreach (var (name, invocation) in chain.Calls)
        {
            if (name == EfAnalysisConstants.EfMethods.Entity)
            {
                return EntityNameFromInvocation(invocation);
            }
        }

        // Entity<T>(e => e.HasMany(...)): the Has call is inside the Entity configuration lambda.
        var enclosingEntity = chain.HasNode.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression is MemberAccessExpressionSyntax ma
                                   && SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.Entity);

        return enclosingEntity is null ? null : EntityNameFromInvocation(enclosingEntity);
    }

    /// <summary>Builds the relationship for a chain, or <see langword="null"/> when it has no paired <c>With</c> call.</summary>
    /// <param name="chain">The fluent chain.</param>
    /// <param name="sourceEntity">The owning entity name.</param>
    /// <param name="entities">The known entities.</param>
    /// <param name="compilation">The compilation for semantic resolution.</param>
    /// <param name="semanticModel">The semantic model for the method's tree.</param>
    private static EfRelationship? BuildRelationship(
        FluentChain chain,
        string sourceEntity,
        Dictionary<string, EfEntity> entities,
        Compilation compilation,
        SemanticModel semanticModel)
    {
        var (hasMethod, hasInvocation) = chain.Calls
            .First(c => c.Name is EfAnalysisConstants.EfMethods.HasOne or EfAnalysisConstants.EfMethods.HasMany);

        var target = ExtractTarget(hasInvocation, sourceEntity, entities, compilation);
        if (string.IsNullOrEmpty(target))
        {
            return null;
        }

        var withMethod = chain.Calls
            .FirstOrDefault(c => c.Name is EfAnalysisConstants.EfMethods.WithOne or EfAnalysisConstants.EfMethods.WithMany)
            .Name;
        if (withMethod is null)
        {
            return null;
        }

        return RelationshipConfigParser.CreateShadowRelationship(
            sourceEntity, target, hasMethod, withMethod, explicitRequired: null);
    }

    /// <summary>Extracts the target entity of a <c>HasOne</c>/<c>HasMany</c> call from its generic arg or first argument.</summary>
    /// <param name="hasInvocation">The Has invocation.</param>
    /// <param name="sourceEntity">The owning entity (unused until Task 3's semantic resolution).</param>
    /// <param name="entities">The known entities.</param>
    /// <param name="compilation">The compilation (unused until Task 3).</param>
    private static string? ExtractTarget(
        InvocationExpressionSyntax hasInvocation,
        string sourceEntity,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        var generic = GenericTypeArgumentName(hasInvocation);
        if (generic is not null)
        {
            return generic;
        }

        var arg = hasInvocation.ArgumentList.Arguments.FirstOrDefault();
        switch (arg?.Expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                return LastSegment(literal.Token.ValueText);
            case SimpleLambdaExpressionSyntax lambda:
                return NavigationName(lambda);
            default:
                return null;
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

    /// <summary>Returns the first generic type argument's simple name for an invocation like <c>HasOne&lt;T&gt;()</c>, else <see langword="null"/>.</summary>
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

    /// <summary>Returns the navigation property name from a lambda like <c>x =&gt; x.Nav</c>, else <see langword="null"/>.</summary>
    /// <param name="lambda">The lambda expression.</param>
    private static string? NavigationName(SimpleLambdaExpressionSyntax lambda)
    {
        return (lambda.Body as MemberAccessExpressionSyntax)?.Name.Identifier.Text;
    }

    /// <summary>Returns the simple identifier of a name syntax (drops any generic type arguments).</summary>
    /// <param name="name">The name syntax.</param>
    private static string SimpleName(SimpleNameSyntax name) => name.Identifier.Text;

    /// <summary>Returns the simple name of a type syntax (last dotted segment, generics dropped).</summary>
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

    /// <summary>
    /// A single left-to-right fluent chain, captured as its ordered method calls plus the
    /// <c>HasOne</c>/<c>HasMany</c> node that seeded it.
    /// </summary>
    private sealed class FluentChain
    {
        /// <summary>Gets the <c>HasOne</c>/<c>HasMany</c> invocation that seeded this chain.</summary>
        public InvocationExpressionSyntax HasNode { get; }

        /// <summary>Gets the chain's method calls in source (left-to-right) order.</summary>
        public IReadOnlyList<(string Name, InvocationExpressionSyntax Invocation)> Calls { get; }

        /// <summary>Initializes a chain by ascending to its outermost invocation and collecting the spine calls.</summary>
        /// <param name="hasNode">The Has invocation to build the chain around.</param>
        public FluentChain(InvocationExpressionSyntax hasNode)
        {
            HasNode = hasNode;

            var outer = hasNode;
            while (outer.Parent is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax parentInvocation })
            {
                outer = parentInvocation;
            }

            var calls = new List<(string, InvocationExpressionSyntax)>();
            for (ExpressionSyntax cursor = outer;
                 cursor is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation;
                 cursor = member.Expression)
            {
                calls.Add((member.Name.Identifier.Text, invocation));
            }

            calls.Reverse();
            Calls = calls;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~FluentRelationshipWalkerTests"`
Expected: PASS (5 tests). If a test fails, fix the walker — do not change the test expectations (they encode the `CreateShadowRelationship` contract).

- [ ] **Step 5: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/FluentRelationshipWalkerTests.cs
git commit -m "feat(ef): add Roslyn fluent-chain relationship walker foundation"
```

---

### Task 2: Foreign-key marking + explicit IsRequired

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentRelationshipWalkerTests.cs`

**Interfaces:**
- Consumes: `FluentApiParsingUtilities.GetOrCreateProperty(EfEntity, string, string)`, `EfPropertyFactory.CopyWith(EfProperty, EfPropertyOverrides)`, `EfPropertyOverrides { IsForeignKey }` (all existing, unchanged).
- Produces: FK-marked properties on the dependent entity; `IsRequired` honored from `.IsRequired(...)` in the chain.

- [ ] **Step 1: Write the failing tests**

Add to `FluentRelationshipWalkerTests.cs`:

```csharp
    [Fact]
    public void Apply_HasForeignKeyLambda_MarksDependentForeignKeyProperty()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = []; }
            public class Post { public int Id { get; set; } public int BlogId { get; set; } public Blog Blog { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Post>().HasOne(p => p.Blog).WithMany(b => b.Posts).HasForeignKey(p => p.BlogId);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        // HasOne on Post => dependent entity is the source (Post); BlogId is its FK.
        entities["Post"].Properties.Should().Contain(p => p.Name == "BlogId" && p.IsForeignKey);
    }

    [Fact]
    public void Apply_HasManyForeignKey_MarksForeignKeyOnTargetEntity()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = []; }
            public class Post { public int Id { get; set; } public int BlogId { get; set; } public Blog Blog { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Blog>().HasMany(b => b.Posts).WithOne(p => p.Blog).HasForeignKey(p => p.BlogId);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        // HasMany on Blog => dependent entity is the target (Post); BlogId is its FK.
        entities["Post"].Properties.Should().Contain(p => p.Name == "BlogId" && p.IsForeignKey);
    }

    [Fact]
    public void Apply_ExplicitIsRequiredFalse_MakesOneToManyOptional()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public List<Post> Posts { get; set; } = []; }
            public class Post { public int Id { get; set; } public int? BlogId { get; set; } public Blog Blog { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Post>().HasOne(p => p.Blog).WithMany(b => b.Posts)
                        .HasForeignKey(p => p.BlogId).IsRequired(false);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Post");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.Type.Should().Be(EfRelationshipType.OneToMany);
        rel.IsRequired.Should().BeFalse();
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~FluentRelationshipWalkerTests"`
Expected: the three new tests FAIL (FK not marked; `IsRequired(false)` ignored so the relationship is still required); the Task-1 tests still PASS.

- [ ] **Step 3: Wire FK marking and explicit-required into the walker**

In `FluentRelationshipWalker.cs`, replace the `Apply` loop body so it applies the foreign key and threads the explicit-required value. Replace this block inside `Apply`:

```csharp
            var relationship = BuildRelationship(chain, sourceEntity, entities, compilation, semanticModel);
            if (relationship is null)
            {
                continue;
            }

            if (existingKeys.Add(relationship.GenerateKey()))
            {
                model.Relationships.Add(relationship);
            }
```

with:

```csharp
            var relationship = BuildRelationship(chain, sourceEntity, entities, compilation, semanticModel);
            if (relationship is null)
            {
                continue;
            }

            var hasMethod = chain.Calls
                .First(c => c.Name is EfAnalysisConstants.EfMethods.HasOne or EfAnalysisConstants.EfMethods.HasMany)
                .Name;
            ApplyForeignKey(chain, hasMethod, sourceEntity, relationship.TargetEntity, entities);

            if (existingKeys.Add(relationship.GenerateKey()))
            {
                model.Relationships.Add(relationship);
            }
```

Then replace the `BuildRelationship` body's final `return` so it reads the explicit-required value. Change:

```csharp
        return RelationshipConfigParser.CreateShadowRelationship(
            sourceEntity, target, hasMethod, withMethod, explicitRequired: null);
```

to:

```csharp
        var explicitRequired = ExtractExplicitRequired(chain);
        return RelationshipConfigParser.CreateShadowRelationship(
            sourceEntity, target, hasMethod, withMethod, explicitRequired);
```

Add these methods to the class (before the `FluentChain` nested type):

```csharp
    /// <summary>Reads an explicit <c>.IsRequired(...)</c> from the chain: <c>true</c> for no-arg or <c>true</c>, <c>false</c> otherwise; <see langword="null"/> when absent.</summary>
    /// <param name="chain">The fluent chain.</param>
    private static bool? ExtractExplicitRequired(FluentChain chain)
    {
        var call = chain.Calls.FirstOrDefault(c => c.Name == EfAnalysisConstants.EfMethods.IsRequired);
        if (call.Name is null)
        {
            return null;
        }

        var arguments = call.Invocation.ArgumentList.Arguments;
        return arguments.Count == 0 || arguments[0].Expression.IsKind(SyntaxKind.TrueLiteralExpression);
    }

    /// <summary>Marks the foreign-key properties declared by a <c>HasForeignKey(...)</c> call on the dependent entity.</summary>
    /// <param name="chain">The fluent chain.</param>
    /// <param name="hasMethod">The Has method name (selects the default dependent entity).</param>
    /// <param name="sourceEntity">The owning entity name.</param>
    /// <param name="targetEntity">The relationship target entity name.</param>
    /// <param name="entities">The known entities.</param>
    private static void ApplyForeignKey(
        FluentChain chain,
        string hasMethod,
        string sourceEntity,
        string targetEntity,
        Dictionary<string, EfEntity> entities)
    {
        var call = chain.Calls.FirstOrDefault(c => c.Name == EfAnalysisConstants.EfMethods.HasForeignKey);
        if (call.Name is null)
        {
            return;
        }

        var propertyNames = ForeignKeyPropertyNames(call.Invocation);
        if (propertyNames.Count == 0)
        {
            return;
        }

        var dependentEntity = GenericTypeArgumentName(call.Invocation)
                              ?? (hasMethod == EfAnalysisConstants.EfMethods.HasOne ? sourceEntity : targetEntity);

        if (entities.TryGetValue(dependentEntity, out var entity))
        {
            MarkForeignKeys(entity, propertyNames);
        }
    }

    /// <summary>Extracts the property names from a <c>HasForeignKey</c> call (lambda member access, anonymous object, or string literals).</summary>
    /// <param name="invocation">The HasForeignKey invocation.</param>
    private static List<string> ForeignKeyPropertyNames(InvocationExpressionSyntax invocation)
    {
        var names = new List<string>();
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            switch (argument.Expression)
            {
                case SimpleLambdaExpressionSyntax lambda:
                    names.AddRange(lambda.Body.DescendantNodesAndSelf()
                        .OfType<MemberAccessExpressionSyntax>()
                        .Select(m => m.Name.Identifier.Text));
                    break;
                case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                    names.Add(literal.Token.ValueText);
                    break;
            }
        }

        return names;
    }

    /// <summary>Sets <see cref="EfProperty.IsForeignKey"/> on the named properties, creating them if missing (parity with the regex parser).</summary>
    /// <param name="entity">The dependent entity.</param>
    /// <param name="propertyNames">The FK property names.</param>
    private static void MarkForeignKeys(EfEntity entity, List<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = FluentApiParsingUtilities.GetOrCreateProperty(entity, propertyName, "");
            var updated = EfPropertyFactory.CopyWith(property, new EfPropertyOverrides { IsForeignKey = true });
            var index = entity.Properties.IndexOf(property);
            if (index >= 0)
            {
                entity.Properties[index] = updated;
            }
        }
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~FluentRelationshipWalkerTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/FluentRelationshipWalkerTests.cs
git commit -m "feat(ef): walker marks foreign keys and honors explicit IsRequired"
```

---

### Task 3: UsingEntity exclusion + navigation-property target resolution

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentRelationshipWalkerTests.cs`

**Interfaces:**
- Consumes: `NavigationPropertyAnalyzer.IsNavigationProperty(IPropertySymbol, out INamedTypeSymbol?, out bool)`, `Compilation.GetSymbolsWithName(string, SymbolFilter)` (existing, unchanged).
- Produces: relationship-defining calls inside `UsingEntity(...)` are ignored; a `HasOne(x => x.Nav)` whose nav name is not itself an entity resolves to the navigation's entity type.

- [ ] **Step 1: Write the failing tests**

Add to `FluentRelationshipWalkerTests.cs`:

```csharp
    [Fact]
    public void Apply_ManyToManyWithUsingEntity_IgnoresJoinConfigurationChains()
    {
        const string source = """
            using System.Collections.Generic;
            public class Customer { public int Id { get; set; } public List<Product> Products { get; set; } = []; }
            public class Product { public int Id { get; set; } public List<Customer> Customers { get; set; } = []; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Customer>()
                        .HasMany(c => c.Products).WithMany(p => p.Customers)
                        .UsingEntity<Dictionary<string, object>>("CustomerProduct",
                            j => j.HasOne<Product>().WithMany().HasForeignKey("ProductId"),
                            j => j.HasOne<Customer>().WithMany().HasForeignKey("CustomerId"));
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Customer", "Product");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        // Only the outer many-to-many is produced; the two inner UsingEntity chains are ignored.
        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.Type.Should().Be(EfRelationshipType.ManyToMany);
    }

    [Fact]
    public void Apply_NavigationNameDiffersFromEntity_ResolvesTargetViaSymbol()
    {
        const string source = """
            using System.Collections.Generic;
            public class Blog { public int Id { get; set; } public int OwnerId { get; set; } public Author Owner { get; set; } = null!; }
            public class Author { public int Id { get; set; } public List<Blog> Blogs { get; set; } = []; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Blog>().HasOne(b => b.Owner).WithMany().HasForeignKey(b => b.OwnerId);
                }
            }
            """;
        var (method, compilation, entities, model) = Build(source, "Blog", "Author");

        FluentRelationshipWalker.Apply(method, entities, model, compilation);

        // HasOne(b => b.Owner): nav "Owner" resolves to entity "Author"; HasOne/WithMany swaps.
        var rel = model.Relationships.Should().ContainSingle().Which;
        rel.SourceEntity.Should().Be("Author");
        rel.TargetEntity.Should().Be("Blog");
        rel.Type.Should().Be(EfRelationshipType.OneToMany);
        entities["Blog"].Properties.Should().Contain(p => p.Name == "OwnerId" && p.IsForeignKey);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~FluentRelationshipWalkerTests"`
Expected: the UsingEntity test FAILS (inner `HasOne<Product>`/`HasOne<Customer>` produce extra relationships); the resolution test FAILS (nav "Owner" not resolved to "Author", so target is empty/wrong). Task 1–2 tests still PASS.

- [ ] **Step 3: Add UsingEntity exclusion and semantic resolution**

In `FluentRelationshipWalker.cs`, replace `FindRelationshipRoots` with:

```csharp
    /// <summary>Finds every <c>HasOne</c>/<c>HasMany</c> invocation, excluding those inside a <c>UsingEntity(...)</c> call.</summary>
    /// <param name="method">The method to scan.</param>
    private static IEnumerable<InvocationExpressionSyntax> FindRelationshipRoots(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma
                          && SimpleName(ma.Name) is EfAnalysisConstants.EfMethods.HasOne
                              or EfAnalysisConstants.EfMethods.HasMany)
            .Where(inv => !IsInsideUsingEntity(inv));
    }

    /// <summary>Determines whether a node is lexically inside the argument list of a <c>UsingEntity(...)</c> invocation.</summary>
    /// <param name="node">The node to test.</param>
    private static bool IsInsideUsingEntity(SyntaxNode node)
    {
        return node.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma
                        && SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.UsingEntity);
    }
```

Then replace `ExtractTarget` so the lambda branch resolves a non-entity nav name via the source symbol:

```csharp
    /// <summary>Extracts the target entity of a <c>HasOne</c>/<c>HasMany</c> call from its generic arg or first argument.</summary>
    /// <param name="hasInvocation">The Has invocation.</param>
    /// <param name="sourceEntity">The owning entity (used to resolve a navigation-property name to its entity type).</param>
    /// <param name="entities">The known entities.</param>
    /// <param name="compilation">The compilation for semantic resolution.</param>
    private static string? ExtractTarget(
        InvocationExpressionSyntax hasInvocation,
        string sourceEntity,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        var generic = GenericTypeArgumentName(hasInvocation);
        if (generic is not null)
        {
            return generic;
        }

        var arg = hasInvocation.ArgumentList.Arguments.FirstOrDefault();
        switch (arg?.Expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                return LastSegment(literal.Token.ValueText);
            case SimpleLambdaExpressionSyntax lambda:
                var nav = NavigationName(lambda);
                if (string.IsNullOrEmpty(nav) || entities.ContainsKey(nav))
                {
                    return nav;
                }

                return ResolveNavigationToEntity(sourceEntity, nav, entities, compilation) ?? nav;
            default:
                return null;
        }
    }

    /// <summary>Resolves a navigation-property name on the source entity to its target entity type name, or <see langword="null"/>.</summary>
    /// <param name="sourceEntity">The owning entity name.</param>
    /// <param name="navigationName">The navigation property name.</param>
    /// <param name="entities">The known entities.</param>
    /// <param name="compilation">The compilation for symbol lookup.</param>
    private static string? ResolveNavigationToEntity(
        string sourceEntity,
        string navigationName,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        var sourceSymbol = compilation.GetSymbolsWithName(sourceEntity, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault();

        var navProperty = sourceSymbol?.GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(p => p.Name.Equals(navigationName, StringComparison.OrdinalIgnoreCase));

        if (navProperty is null)
        {
            return null;
        }

        return NavigationPropertyAnalyzer.IsNavigationProperty(navProperty, out var targetType, out _)
               && targetType is not null
               && entities.ContainsKey(targetType.Name)
            ? targetType.Name
            : null;
    }
```

Note: the `semanticModel` parameter on `BuildRelationship` is no longer used (resolution goes through `compilation`/symbols like the original `RelationshipConfigParser`). Remove the unused `semanticModel` parameter from `BuildRelationship` and its call site, and stop computing `semanticModel` in `Apply` (delete the `var semanticModel = ...` line and the argument), to keep the build warning-free under `TreatWarningsAsErrors`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~FluentRelationshipWalkerTests"`
Expected: PASS (10 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentRelationshipWalker.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/FluentRelationshipWalkerTests.cs
git commit -m "feat(ef): walker skips UsingEntity blocks and resolves nav-property targets"
```

---

### Task 4: Wire the walker behind the seam (context path) and prove golden parity

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs`

**Interfaces:**
- Consumes: `FluentRelationshipWalker.Apply` (Tasks 1–3).
- Produces: `ApplyFluentApiConstraints` routes relationships through the walker; `ApplyConstraintsFromMethod` gains a `bool includeRelationships = true` parameter (default preserves the snapshot path's behavior). No public signature is removed.

The seam change: the context path stops using `RelationshipConfigParser` and instead (1) runs the existing section loop for **properties + table only**, creating any fluent-only entities, then (2) runs `FluentRelationshipWalker.Apply` for relationships + FK. The snapshot path (`ModelSnapshotParser` → `ApplyConstraintsFromMethod`) is unchanged because `includeRelationships` defaults to `true`.

- [ ] **Step 1: Route the context path through the walker**

In `FluentApiConfigurationParser.cs`, replace `ApplyFluentApiConstraints` (the method body starting at the `var methodSyntax = ...` line) with:

```csharp
        var methodSyntax = FindOnModelCreatingMethod(contextType);
        if (methodSyntax is null || (methodSyntax.Body is null && methodSyntax.ExpressionBody is null))
        {
            return;
        }

        // Context path: parse property/table config from text (Slices 2/3 still), but derive
        // relationships and foreign keys from the Roslyn syntax walker instead of RelationshipConfigParser.
        ApplyConstraintsFromMethod(methodSyntax, entities, model, compilation, includeRelationships: false);
        FluentRelationshipWalker.Apply(methodSyntax, entities, model, compilation);
```

- [ ] **Step 2: Thread `includeRelationships` through the section parser**

In `FluentApiConfigurationParser.cs`, change `ApplyConstraintsFromMethod` to accept the flag and pass it down. Replace its signature and the `ProcessEntityConfigSection` call:

```csharp
    public static void ApplyConstraintsFromMethod(
        MethodDeclarationSyntax methodSyntax,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        bool includeRelationships = true)
    {
        // Accept both block-bodied ({ ... }) and expression-bodied (=> ...) methods; ToString()
        // includes the expression body text in either case.
        if (methodSyntax.Body is null && methodSyntax.ExpressionBody is null)
        {
            return;
        }

        var methodText = methodSyntax.ToString();
        var entityConfigSections = EfAnalysisRegexPatterns.EntitySplitRegex().Split(methodText);

        // Skip the first part (before the first .Entity)
        for (var i = 1; i < entityConfigSections.Length; i++)
        {
            ProcessEntityConfigSection(entityConfigSections[i], entities, model, compilation, includeRelationships);
        }
    }
```

Update `ProcessEntityConfigSection` to accept and forward the flag:

```csharp
    private static void ProcessEntityConfigSection(
        string sectionContent,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        bool includeRelationships)
    {
        // Add back "Entity" which was removed by the split
        var section = EfAnalysisConstants.EfMethods.Entity + sectionContent;

        // Extract just this entity's configuration (up to the next .Entity)
        var entityConfigEnd = EfAnalysisRegexPatterns.EntitySplitRegex().Match(section, 7).Index;
        if (entityConfigEnd > 0)
        {
            section = section[..entityConfigEnd];
        }

        var shadowRelationships = ParseEntityConfiguration(section, entities, model, compilation, includeRelationships);
        AddUniqueRelationships(shadowRelationships, model);
    }
```

Update `ParseEntityConfiguration`: add the `bool includeRelationships` parameter and guard the two `RelationshipConfigParser` calls with it. Change its signature line to:

```csharp
    private static List<EfRelationship> ParseEntityConfiguration(
        string configSection,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        bool includeRelationships)
```

and replace the two relationship-parser calls:

```csharp
        RelationshipConfigParser.ParseShadowRelationships(configSection, entityName, entities, shadowRelationships);
        RelationshipConfigParser.ParseExplicitRelationships(configSection, entityName, entities, shadowRelationships,
            compilation);
        PropertyConfigParser.ParsePropertyConfigurations(configSection, entity, compilation);
```

with:

```csharp
        if (includeRelationships)
        {
            RelationshipConfigParser.ParseShadowRelationships(configSection, entityName, entities, shadowRelationships);
            RelationshipConfigParser.ParseExplicitRelationships(configSection, entityName, entities, shadowRelationships,
                compilation);
        }

        PropertyConfigParser.ParsePropertyConfigurations(configSection, entity, compilation);
```

- [ ] **Step 3: Run the golden + advanced EF suite (parity gate)**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj`
Expected: PASS — in particular `EfErdGoldenTests` (all 7 goldens byte-identical, **no golden regenerated**) and `EfAnalysisAdvancedTests` (`ShouldHandleManyToManyRelationships`, `ShouldHandleFluentApiShadowRelationships`, `ExplicitOptionalOneToMany_ShouldNotBeRequired`, `ShouldMarkCompositePrimaryKeyAsForeignKey`, `ShouldHandleSelfReferencingEntity`, `OwnsOneBuilder_DoesNotCreatePhantomOwned`, `ExpressionBodiedOnModelCreating`, `ShouldNotCreateDuplicateEntitiesOrRelationships`).

If any golden diff appears, DO NOT regenerate the golden. Diagnose the walker against the specific fixture:
- Compare `dotnet run --project src/ProjGraph.Cli -- erd <fixture>` output to the committed `.mmd`.
- The likely culprits and their fixes:
  - Missing many-to-many join table → the outer `HasMany(...).WithMany(...)` chain must still yield a `ManyToMany` relationship (verify `UsingEntity` exclusion only drops the *inner* `j => ...` chains, not the outer one).
  - Extra/duplicate relationship → confirm dedup by `GenerateKey()` and that a bare `HasOne` with no `With` yields nothing.
  - Wrong FK marking → confirm dependent-entity selection (`HasOne`→source, `HasMany`→target, `HasForeignKey<T>` override).
- Fix the walker (Tasks 1–3 files), re-run, and add a regression unit test to `FluentRelationshipWalkerTests` reproducing the fixture's construct.

- [ ] **Step 4: Full suite + format**

Run: `dtk dotnet test ProjGraph.slnx`
Expected: all pass (including `EfAnalysisServiceSnapshotTests` — the snapshot path is untouched, so its relationship extraction is byte-identical).

Run: `dtk dotnet format ProjGraph.slnx --verify-no-changes`
Expected: nothing to format.

- [ ] **Step 5: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs
git commit -m "$(cat <<'MSG'
feat(ef): route OnModelCreating relationships through the Roslyn walker

The DbContext path now derives relationships and foreign keys from
FluentRelationshipWalker (syntax-tree chains) instead of the regex
RelationshipConfigParser. Property and table parsing still run through the
text section loop (Slices 2/3). The snapshot path keeps RelationshipConfigParser
via ApplyConstraintsFromMethod's includeRelationships default (retired in Slice 6).
Golden files unchanged — output is byte-identical.
MSG
)"
```

---

## Notes for the reviewer

- **Scope boundary.** This slice replaces relationship parsing **only on the DbContext `OnModelCreating` path**. `RelationshipConfigParser` and its `ShadowRelationshipRegex` are intentionally retained for the snapshot path (`ModelSnapshotParser` → `ApplyConstraintsFromMethod`), which Slice 6 migrates. This is the strangler-fig "old and new briefly coexist" intermediate state; no regex is deleted yet.
- **Parity contract.** The Slice-0 goldens plus `EfAnalysisAdvancedTests` are the byte-identical gate. Target is zero golden changes. `EfAnalysisRegexPatternsTests`, `RelationshipConfigParserTests`, and `FluentApiParsingUtilitiesTests` remain valid and untouched (the regex they test still backs the snapshot path).
- **Why the section loop runs before the walker.** The property/table loop (`includeRelationships: false`) first materializes any fluent-only entities and their property/table config; the walker then adds relationships and FK marks on top. FK marking and property config touch disjoint `EfProperty` fields via `CopyWith`, so the order is output-neutral — verified by the goldens.
- **Fragility retired (context path).** The walker removes the §4.3 heuristics for this path: the 10-match forward-scan window, paren-balance `IsInsideUsingEntityBlock`, and cross-statement chain-boundary detection are replaced by exact syntax-spine reads. The equivalent snapshot-path fragility is removed in Slice 6.

## Self-review

- **Spec coverage (Slice 1 line):** "`HasOne/HasMany/WithOne/WithMany/HasForeignKey/IsRequired/OnDelete` via chain walking. Retire `RelationshipConfigParser` regex paths." — Has/With/HasForeignKey/IsRequired covered (Tasks 1–3); `RelationshipConfigParser` retired **on the context path** (Task 4), snapshot deferred to Slice 6 with written rationale. **`OnDelete` is not currently parsed by `RelationshipConfigParser`** (it affects no `EfRelationship`/`EfProperty` field and no golden), so there is nothing to preserve; it is intentionally out of scope and noted here rather than given a no-op task. ✔
- **Placeholder scan:** No TBD/TODO; all walker and seam code shown in full; commands and expected output given; the parity-diagnosis step (Task 4 Step 3) lists concrete failure modes and fixes rather than "handle edge cases." ✔
- **Type consistency:** `FluentRelationshipWalker.Apply(MethodDeclarationSyntax, Dictionary<string,EfEntity>, EfModel, Compilation)`, `FluentChain.Calls`/`HasNode`, `ExtractTarget`, `ApplyForeignKey`, `ExtractExplicitRequired`, and `ApplyConstraintsFromMethod(..., bool includeRelationships = true)` are used consistently across Tasks 1–4. The walker delegates has/with → relationship mapping to the existing public `RelationshipConfigParser.CreateShadowRelationship` (no duplication). ✔
- **Scope:** One reviewable deliverable — the walker plus its context-path wiring, gated by existing goldens. Property config (Slice 2), owned/join internals (Slice 3), and the snapshot migration (Slice 6) are separate plans. ✔
