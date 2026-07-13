# EF Rewrite — Slice 2: Property Config (Roslyn fluent-chain walker) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the regex-over-text property parser (`PropertyConfigParser`) on the DbContext `OnModelCreating` path with a Roslyn syntax-tree walker (`FluentPropertyWalker`) that folds each `.Property(...)` chain and `.HasKey(...)` call into the owning `EfEntity`'s properties, producing byte-identical ERD output (gated by the Slice-0 golden files).

**Architecture:** A new `internal static FluentPropertyWalker` finds the `.Property`/`.HasKey` `InvocationExpressionSyntax` nodes in `OnModelCreating`, resolves the owning entity from each call's receiver (chain form `modelBuilder.Entity<T>().Property(...)`) or enclosing `Entity<T>(e => ...)` lambda, and applies the trailing config calls (`IsRequired`/`HasMaxLength`/`HasPrecision`/`HasColumnType`/`HasDefaultValue`/`HasDefaultValueSql`) plus primary-key marking. "Which entity/property does this call belong to" is answered by the syntax spine, not by a forward text scan. It is wired into the context path only (`FluentApiConfigurationParser.ApplyFluentApiConstraints`); the snapshot path keeps `PropertyConfigParser` until Slice 6. The walker delegates the config *value* application to the existing `PropertyConfigParser.ApplyConfiguration` switch (exposed `internal`, fed the syntax argument text) — so it only avoids the fragile forward-scan regex, and value parsing stays shared with the snapshot path until Slice 6 (exactly as Slice 1 reused `RelationshipConfigParser.CreateShadowRelationship`).

**Tech Stack:** C# / .NET 10, Roslyn (`Microsoft.CodeAnalysis.CSharp.Syntax`), xUnit v3, FluentAssertions, `RoslynTestHelper`, `EfErdGoldenTests` (Slice 0).

## Global Constraints

- Target framework `net10.0`; build enforces `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true` — no warnings allowed.
- XML documentation is required on all public APIs. `FluentPropertyWalker` is `internal`, but keep `<summary>` docs on it and its members for consistency with the surrounding files (`FluentRelationshipWalker` is the model).
- Use `dtk dotnet build` / `dtk dotnet test` / `dtk dotnet format ProjGraph.slnx --verify-no-changes` (token-optimized wrapper) for all build/test/format commands.
- The Slice-0 golden files (`tests/ProjGraph.Tests.Unit.EntityFramework/Golden/goldens/*.mmd`) are the parity contract. A golden may only change with a reviewer-visible diff justified by a specific fixed finding. **This slice targets zero golden changes.**
- Do NOT touch the snapshot path (`ModelSnapshotParser`, `AnalyzeSnapshotUseCase`) or `PropertyConfigParser`'s parsing logic in this slice beyond exposing `ApplyConfiguration`. They are retired in Slice 6.
- Do NOT touch `FluentRelationshipWalker` (Slice 1). A handful of tiny syntax helpers (`SimpleName`, `TypeName`, `LastSegment`, `GenericTypeArgumentName`, `EntityNameFromInvocation`) are duplicated between the two walkers; consolidating them is deferred to Slice 6, matching the Slice-1 plan's "old and new briefly coexist, relocate at Slice 6" stance.

### Byte-exact behaviors to preserve (verified against `PropertyConfigParser` source)

- **Property declaration.** `.Property(a => a.Name)` or `.Property("Name")` resolves the property name (single member access, or the string literal); `.Property<T>(...)` sets the type from the generic argument. Property lookup/creation goes through `FluentApiParsingUtilities.GetOrCreateProperty(entity, name, type)` (unchanged).
- **Config application.** `IsRequired`, `HasMaxLength`, `HasPrecision`, `HasColumnType`, `HasDefaultValue`, `HasDefaultValueSql` are applied by `PropertyConfigParser.ApplyConfiguration(property, method, argText, compilation)` where `argText` = `invocation.ArgumentList.Arguments.ToString()` (equals the regex parser's captured argument group: e.g. `18, 2` for `HasPrecision(18, 2)`, `"char(8)"` for `HasColumnType("char(8)")`, `""` for a no-arg `IsRequired()`). Updated property instances replace the original in `entity.Properties` (only when the returned instance differs, mirroring `ReplaceProperty`).
- **HasKey.** `.HasKey(a => a.Id)`, `.HasKey(a => new { a.X, a.Y })`, and `.HasKey("X", "Y")` mark each named property `IsPrimaryKey = true` via `GetOrCreateProperty(entity, name, "")` + `EfPropertyFactory.CopyWith` (identical to `ApplyKeyConfiguration`).
- **Nested-builder exclusion.** `.Property`/`.HasKey` calls lexically inside an `OwnsOne(...)`, `OwnsMany(...)`, or `UsingEntity(...)` argument list are ignored (owned-type / join-entity config is Slice 3). This reproduces the regex parser, whose single-level argument capture absorbed such nested calls so they were never applied to the outer entity — e.g. `OwnedAndJoinContext` renders `Customer` with only `Id`, no phantom `City`.
- **Entity/property field independence.** Property config (`IsRequired`/`MaxLength`/…) and FK marking (Slice 1's relationship walker) touch disjoint `EfProperty` fields via `CopyWith`, and PK vs FK are independent flags — so the property walker running before the relationship walker is output-neutral (verified by goldens).

---

### Task 1: Walker foundation — entity-scoped `Property` chains + config application

**Files:**
- Create: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs`
- Create: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentPropertyWalkerTests.cs`
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/Constants/EfAnalysisConstants.cs` (add `OwnsOne`/`OwnsMany`)
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/PropertyConfigParser.cs` (expose `ApplyConfiguration`)

**Interfaces:**
- Consumes (existing, do not change): `EntityAnalyzer.AnalyzeEntity(INamedTypeSymbol)`, `FluentApiParsingUtilities.GetOrCreateProperty(EfEntity, string, string)`, `EfPropertyFactory.CopyWith`, `EfPropertyOverrides`, `EfAnalysisConstants.EfMethods.*`, `RoslynTestHelper.CreateCompilation`, `RoslynTestHelper.GetTypeSymbol`.
- Produces (Task 2 relies on these exact members):
  - `internal static class FluentPropertyWalker` with `public static void Apply(MethodDeclarationSyntax method, Dictionary<string, EfEntity> entities, Compilation compilation)`.
  - Private helpers `FindConfigRoots(MethodDeclarationSyntax, string)`, `IsInsideNestedBuilderScope(SyntaxNode)`, `ResolveOwningEntity(InvocationExpressionSyntax)`, `TrailingCalls(InvocationExpressionSyntax)`, `ReplaceProperty(EfEntity, EfProperty, EfProperty)`, `SingleArgumentName`, `EntityNameFromInvocation`, `GenericTypeArgumentName`, `SimpleName`, `TypeName`, `LastSegment`, and the `NestedBuilderScopes` set.
  - `PropertyConfigParser.ApplyConfiguration(EfProperty, string, string, Compilation)` (was `private ApplyPropertyConfiguration`).
  - `EfAnalysisConstants.EfMethods.OwnsOne` / `.OwnsMany` string constants.

- [ ] **Step 1: Add the `OwnsOne`/`OwnsMany` constants**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/Constants/EfAnalysisConstants.cs`, inside `internal static class EfMethods`, add the two constants next to `UsingEntity`:

```csharp
        public const string UsingEntity = "UsingEntity";
        public const string OwnsOne = "OwnsOne";
        public const string OwnsMany = "OwnsMany";
```

- [ ] **Step 2: Expose `PropertyConfigParser.ApplyConfiguration`**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/PropertyConfigParser.cs`, change the private `ApplyPropertyConfiguration` to an `internal` method named `ApplyConfiguration`, and update its single internal caller. Replace the method signature line:

```csharp
    private static EfProperty ApplyPropertyConfiguration(EfProperty property, string configMethod, string configArg,
        Compilation compilation)
```

with:

```csharp
    /// <summary>
    /// Applies a single property-configuration call to a property, dispatching on the fluent method name.
    /// Shared by the text parser (snapshot path) and <see cref="FluentPropertyWalker"/> (context path);
    /// returns the same instance for unrecognized methods.
    /// </summary>
    /// <param name="property">The property to configure.</param>
    /// <param name="configMethod">The configuration method name (e.g. <c>HasMaxLength</c>).</param>
    /// <param name="configArg">The raw argument text captured between the call's parentheses.</param>
    /// <param name="compilation">The Roslyn compilation for constant/enum resolution.</param>
    internal static EfProperty ApplyConfiguration(EfProperty property, string configMethod, string configArg,
        Compilation compilation)
```

Then update the caller inside `ParsePropertyConfigurations` (currently `var updated = ApplyPropertyConfiguration(currentProperty, methodName, args, compilation);`) to:

```csharp
                var updated = ApplyConfiguration(currentProperty, methodName, args, compilation);
```

(Delete the now-duplicated XML `<summary>` block that previously preceded `ApplyPropertyConfiguration`, since the new one above replaces it — do not leave two doc comments.)

- [ ] **Step 3: Write the failing tests**

Create `tests/ProjGraph.Tests.Unit.EntityFramework/FluentPropertyWalkerTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Unit tests for <see cref="FluentPropertyWalker"/>: the Roslyn fluent-chain property/key walker that
/// replaces the regex <c>PropertyConfigParser</c> on the DbContext path.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class FluentPropertyWalkerTests
{
    /// <summary>
    /// Compiles <paramref name="source"/>, locates its OnModelCreating method, and builds the entities
    /// dictionary from the named entity classes so the walker can be driven in isolation.
    /// </summary>
    private static (MethodDeclarationSyntax Method, Compilation Compilation, Dictionary<string, EfEntity> Entities)
        Build(string source, params string[] entityNames)
    {
        var compilation = RoslynTestHelper.CreateCompilation(source);
        var method = compilation.SyntaxTrees[0].GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "OnModelCreating");

        var entities = new Dictionary<string, EfEntity>();
        foreach (var name in entityNames)
        {
            var symbol = RoslynTestHelper.GetTypeSymbol((CSharpCompilation)compilation, name)!;
            entities[name] = EntityAnalyzer.AnalyzeEntity(symbol);
        }

        return (method, compilation, entities);
    }

    private static EfProperty Property(Dictionary<string, EfEntity> entities, string entity, string property)
        => entities[entity].Properties.Single(p => p.Name == property);

    [Fact]
    public void Apply_LambdaForm_AppliesRequiredAndMaxLength()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e =>
                    {
                        e.Property(a => a.Name).IsRequired().HasMaxLength(200);
                    });
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        var name = Property(entities, "Account", "Name");
        name.IsRequired.Should().BeTrue();
        name.MaxLength.Should().Be(200);
    }

    [Fact]
    public void Apply_ChainForm_ResolvesEntityFromEntityCall()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                {
                    modelBuilder.Entity<Account>().Property(a => a.Name).HasMaxLength(50);
                }
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "Name").MaxLength.Should().Be(50);
    }

    [Fact]
    public void Apply_HasPrecision_SetsPrecisionAndScale()
    {
        const string source = """
            public class Account { public int Id { get; set; } public decimal Balance { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => a.Balance).HasPrecision(18, 2));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        var balance = Property(entities, "Account", "Balance");
        balance.Precision.Should().Be(18);
        balance.Scale.Should().Be(2);
    }

    [Fact]
    public void Apply_HasColumnType_InfersMaxLengthFromParens()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Code { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => a.Code).HasColumnType("char(8)"));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "Code").MaxLength.Should().Be(8);
    }

    [Fact]
    public void Apply_HasDefaultValue_ResolvesEnumConstant()
    {
        const string source = """
            public enum Status { Inactive = 0, Active = 1 }
            public class User { public int Id { get; set; } public Status Status { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<User>(e => e.Property(u => u.Status).HasDefaultValue(Status.Active));
            }
            """;
        var (method, compilation, entities) = Build(source, "User");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "User", "Status").DefaultValue.Should().Be("1");
    }

    [Fact]
    public void Apply_HasDefaultValueSql_TrimsQuotes()
    {
        const string source = """
            public class Account { public int Id { get; set; } public System.DateTime CreatedAt { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.Property(a => a.CreatedAt).HasDefaultValueSql("GETUTCDATE()"));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "CreatedAt").DefaultValue.Should().Be("GETUTCDATE()");
    }

    [Fact]
    public void Apply_PropertyInsideOwnsOne_IsIgnored()
    {
        // The regex parser absorbed nested Property calls (single-level arg capture) so they never
        // reached the outer entity; the walker must skip them too (owned types are Slice 3).
        const string source = """
            public class Address { public string City { get; set; } = ""; }
            public class Customer { public int Id { get; set; } public Address Address { get; set; } = null!; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Customer>(e =>
                    {
                        e.OwnsOne(c => c.Address, a => a.Property(p => p.City).HasMaxLength(50));
                    });
            }
            """;
        var (method, compilation, entities) = Build(source, "Customer");

        FluentPropertyWalker.Apply(method, entities, compilation);

        entities["Customer"].Properties.Should().NotContain(p => p.Name == "City");
    }

    [Fact]
    public void Apply_UnknownEntity_DoesNothing()
    {
        const string source = """
            public class Account { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Unknown>(e => e.Property(a => a.Name).HasMaxLength(10));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        var act = () => FluentPropertyWalker.Apply(method, entities, compilation);

        act.Should().NotThrow();
        entities["Account"].Properties.Should().NotContain(p => p.Name == "Name" && p.MaxLength == 10);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~FluentPropertyWalkerTests"`
Expected: FAIL to compile — `FluentPropertyWalker` does not exist yet.

- [ ] **Step 5: Create `FluentPropertyWalker` (Property path only)**

Create `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Walks the Fluent API invocation chains of a configuring method (OnModelCreating) directly on the
/// C# syntax tree to discover per-property configuration
/// (<c>Property</c>/<c>HasKey</c>/<c>IsRequired</c>/<c>HasMaxLength</c>/<c>HasPrecision</c>/
/// <c>HasColumnType</c>/<c>HasDefaultValue</c>/<c>HasDefaultValueSql</c>), replacing the text/regex based
/// <see cref="PropertyConfigParser"/> for the DbContext path. The receiver expression of each chain
/// determines the owning entity, so configuration never leaks between unrelated statements or into
/// nested owned-type / join-entity builder lambdas.
/// </summary>
internal static class FluentPropertyWalker
{
    /// <summary>
    /// Fluent methods that open a nested builder lambda for a *different* target (an owned type or a
    /// join entity). <c>Property</c>/<c>HasKey</c> calls inside their argument lists configure that
    /// nested builder, not the outer entity, and are out of scope for this slice (owned types and join
    /// entities are handled in Slice 3). This mirrors the regex parser, whose single-level argument
    /// capture absorbed such nested calls so they were never applied to the outer entity.
    /// </summary>
    private static readonly IReadOnlySet<string> NestedBuilderScopes = new HashSet<string>(StringComparer.Ordinal)
    {
        EfAnalysisConstants.EfMethods.OwnsOne,
        EfAnalysisConstants.EfMethods.OwnsMany,
        EfAnalysisConstants.EfMethods.UsingEntity
    };

    /// <summary>
    /// Applies every <c>Property</c> configuration found in <paramref name="method"/> to the matching
    /// entity in <paramref name="entities"/>.
    /// </summary>
    /// <param name="method">The <c>OnModelCreating</c> method declaration to walk.</param>
    /// <param name="entities">Entities already discovered from DbSets and fluent <c>.Entity&lt;T&gt;</c> calls.</param>
    /// <param name="compilation">The Roslyn compilation for constant/enum default-value resolution.</param>
    public static void Apply(
        MethodDeclarationSyntax method,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        foreach (var propertyRoot in FindConfigRoots(method, EfAnalysisConstants.EfMethods.Property))
        {
            ApplyPropertyChain(propertyRoot, entities, compilation);
        }
    }

    /// <summary>
    /// Finds every invocation whose immediate member name is <paramref name="methodName"/>, excluding
    /// those nested inside an owned-type / join-entity builder lambda (see <see cref="NestedBuilderScopes"/>).
    /// </summary>
    /// <param name="method">The method to scan.</param>
    /// <param name="methodName">The simple method name to match (e.g. <c>Property</c> or <c>HasKey</c>).</param>
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
    /// Determines whether a node is lexically inside the argument list of an owned-type / join-entity
    /// builder invocation (<see cref="NestedBuilderScopes"/>). The argument list — not the whole
    /// invocation — is tested because such a call is itself chained onto the entity being configured.
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
    /// Resolves the owning entity for a <c>Property</c> chain, folds the chain's configuration calls
    /// into the property, and writes each updated property back into the entity.
    /// </summary>
    /// <param name="propertyRoot">The <c>Property</c> invocation seeding the chain.</param>
    /// <param name="entities">The known entities.</param>
    /// <param name="compilation">The compilation for constant/enum resolution.</param>
    private static void ApplyPropertyChain(
        InvocationExpressionSyntax propertyRoot,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        var entityName = ResolveOwningEntity(propertyRoot);
        if (entityName is null || !entities.TryGetValue(entityName, out var entity))
        {
            return;
        }

        var propertyName = SingleArgumentName(propertyRoot);
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        var type = GenericTypeArgumentName(propertyRoot) ?? "";
        var current = FluentApiParsingUtilities.GetOrCreateProperty(entity, propertyName, type);

        foreach (var (name, invocation) in TrailingCalls(propertyRoot))
        {
            var argText = invocation.ArgumentList.Arguments.ToString();
            var updated = PropertyConfigParser.ApplyConfiguration(current, name, argText, compilation);
            if (ReferenceEquals(updated, current))
            {
                continue;
            }

            ReplaceProperty(entity, current, updated);
            current = updated;
        }
    }

    /// <summary>Replaces <paramref name="original"/> with <paramref name="replacement"/> in the entity's property list.</summary>
    /// <param name="entity">The entity whose property list to update.</param>
    /// <param name="original">The property instance to replace.</param>
    /// <param name="replacement">The new property instance.</param>
    private static void ReplaceProperty(EfEntity entity, EfProperty original, EfProperty replacement)
    {
        var index = entity.Properties.IndexOf(original);
        if (index >= 0)
        {
            entity.Properties[index] = replacement;
        }
    }

    /// <summary>
    /// Resolves the entity that owns a configuration call, either from an <c>Entity&lt;T&gt;()</c> earlier
    /// in the same chain (<c>modelBuilder.Entity&lt;T&gt;().Property(...)</c>) or from the enclosing
    /// <c>Entity&lt;T&gt;(e =&gt; ...)</c> configuration lambda.
    /// </summary>
    /// <param name="configInvocation">The <c>Property</c>/<c>HasKey</c> invocation.</param>
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

    /// <summary>Enumerates the invocation calls chained after <paramref name="root"/>, in source order.</summary>
    /// <param name="root">The chain-seeding invocation (e.g. a <c>Property</c> call).</param>
    private static IEnumerable<(string Name, InvocationExpressionSyntax Invocation)> TrailingCalls(
        InvocationExpressionSyntax root)
    {
        for (SyntaxNode? cursor = root.Parent;
             cursor is MemberAccessExpressionSyntax member && member.Parent is InvocationExpressionSyntax invocation;
             cursor = invocation.Parent)
        {
            yield return (member.Name.Identifier.Text, invocation);
        }
    }

    /// <summary>Returns the property name from a single-argument config call: a lambda <c>x =&gt; x.Prop</c> or a string literal.</summary>
    /// <param name="invocation">The invocation (e.g. a <c>Property</c> call).</param>
    private static string? SingleArgumentName(InvocationExpressionSyntax invocation)
    {
        return invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression switch
        {
            SimpleLambdaExpressionSyntax lambda => (lambda.Body as MemberAccessExpressionSyntax)?.Name.Identifier.Text,
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression)
                => literal.Token.ValueText,
            _ => null
        };
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

    /// <summary>Returns the first generic type argument's simple name for an invocation like <c>Property&lt;T&gt;()</c>, else <see langword="null"/>.</summary>
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
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~FluentPropertyWalkerTests"`
Expected: PASS (8 tests).

- [ ] **Step 7: Build + format**

Run: `dtk dotnet build ProjGraph.slnx`
Expected: build succeeds, no warnings (`TreatWarningsAsErrors`).

Run: `dtk dotnet format ProjGraph.slnx --verify-no-changes`
Expected: nothing to format.

- [ ] **Step 8: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/FluentPropertyWalkerTests.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/Constants/EfAnalysisConstants.cs \
        src/ProjGraph.Lib.EntityFramework/Infrastructure/PropertyConfigParser.cs
git commit -m "feat(ef): Roslyn property-config walker (Property chains) for the DbContext path"
```

---

### Task 2: `HasKey` primary-key marking (single + composite)

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs`
- Modify: `tests/ProjGraph.Tests.Unit.EntityFramework/FluentPropertyWalkerTests.cs`

**Interfaces:**
- Consumes: `FluentPropertyWalker.FindConfigRoots`, `ResolveOwningEntity`, `ReplaceProperty` (Task 1); `FluentApiParsingUtilities.GetOrCreateProperty`; `EfPropertyFactory.CopyWith`; `EfPropertyOverrides`.
- Produces: `Apply` also processes `HasKey`; private `ApplyKey(InvocationExpressionSyntax, Dictionary<string, EfEntity>)` and `KeyPropertyNames(InvocationExpressionSyntax)`.

- [ ] **Step 1: Write the failing tests**

Append to `tests/ProjGraph.Tests.Unit.EntityFramework/FluentPropertyWalkerTests.cs` (inside the class, before the closing brace):

```csharp
    [Fact]
    public void Apply_HasKey_SingleProperty_MarksPrimaryKey()
    {
        const string source = """
            public class Account { public int Id { get; set; } public int LegacyId { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Account>(e => e.HasKey(a => a.LegacyId));
            }
            """;
        var (method, compilation, entities) = Build(source, "Account");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "Account", "LegacyId").IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_HasKey_CompositeAnonymousObject_MarksAllPrimaryKeys()
    {
        const string source = """
            public class ProductSupplier { public int ProductId { get; set; } public int SupplierId { get; set; } }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<ProductSupplier>().HasKey(ps => new { ps.ProductId, ps.SupplierId });
            }
            """;
        var (method, compilation, entities) = Build(source, "ProductSupplier");

        FluentPropertyWalker.Apply(method, entities, compilation);

        Property(entities, "ProductSupplier", "ProductId").IsPrimaryKey.Should().BeTrue();
        Property(entities, "ProductSupplier", "SupplierId").IsPrimaryKey.Should().BeTrue();
    }

    [Fact]
    public void Apply_HasKeyInsideUsingEntity_IsIgnored()
    {
        // Join-entity key config inside UsingEntity belongs to the join builder, not the outer entity (Slice 3).
        const string source = """
            using System.Collections.Generic;
            public class Product { public int Id { get; set; } public List<Customer> Customers { get; set; } = []; }
            public class Customer { public int Id { get; set; } public List<Product> Products { get; set; } = []; }
            public class Ctx
            {
                void OnModelCreating(dynamic modelBuilder)
                    => modelBuilder.Entity<Customer>(e =>
                        e.HasMany(c => c.Products).WithMany(p => p.Customers)
                            .UsingEntity<Dictionary<string, object>>("CustomerProduct",
                                j => j.HasOne<Product>().WithMany().HasForeignKey("ProductId"),
                                j => j.HasOne<Customer>().WithMany().HasForeignKey("CustomerId"),
                                j => j.HasKey("ProductId", "CustomerId")));
            }
            """;
        var (method, compilation, entities) = Build(source, "Customer", "Product");

        FluentPropertyWalker.Apply(method, entities, compilation);

        entities["Customer"].Properties.Should().NotContain(p => p.Name == "ProductId");
        entities["Customer"].Properties.Should().NotContain(p => p.Name == "CustomerId");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~FluentPropertyWalkerTests"`
Expected: FAIL — `HasKey_SingleProperty` and `HasKey_CompositeAnonymousObject` fail (PK not marked) because `Apply` does not process `HasKey` yet. (`HasKeyInsideUsingEntity` already passes — the nested-scope skip from Task 1 covers it — but it stays as a regression guard.)

- [ ] **Step 3: Add `HasKey` processing to the walker**

In `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs`, add a second loop to `Apply` (after the existing `Property` loop):

```csharp
        foreach (var propertyRoot in FindConfigRoots(method, EfAnalysisConstants.EfMethods.Property))
        {
            ApplyPropertyChain(propertyRoot, entities, compilation);
        }

        foreach (var keyRoot in FindConfigRoots(method, EfAnalysisConstants.EfMethods.HasKey))
        {
            ApplyKey(keyRoot, entities);
        }
```

Then add the two private methods (place them after `ApplyPropertyChain`):

```csharp
    /// <summary>
    /// Resolves the owning entity for a <c>HasKey</c> call and marks each named property as a primary key.
    /// </summary>
    /// <param name="keyRoot">The <c>HasKey</c> invocation.</param>
    /// <param name="entities">The known entities.</param>
    private static void ApplyKey(InvocationExpressionSyntax keyRoot, Dictionary<string, EfEntity> entities)
    {
        var entityName = ResolveOwningEntity(keyRoot);
        if (entityName is null || !entities.TryGetValue(entityName, out var entity))
        {
            return;
        }

        foreach (var propertyName in KeyPropertyNames(keyRoot))
        {
            var property = FluentApiParsingUtilities.GetOrCreateProperty(entity, propertyName, "");
            var updated = EfPropertyFactory.CopyWith(property, new EfPropertyOverrides { IsPrimaryKey = true });
            ReplaceProperty(entity, property, updated);
        }
    }

    /// <summary>
    /// Extracts primary-key property names from a <c>HasKey</c> argument: a single lambda member access
    /// (<c>a =&gt; a.Id</c>), an anonymous-object lambda (<c>a =&gt; new { a.X, a.Y }</c>), or string literals.
    /// </summary>
    /// <param name="invocation">The <c>HasKey</c> invocation.</param>
    private static IEnumerable<string> KeyPropertyNames(InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            switch (argument.Expression)
            {
                case SimpleLambdaExpressionSyntax lambda:
                    foreach (var member in lambda.Body.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
                    {
                        yield return member.Name.Identifier.Text;
                    }

                    break;
                case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                    yield return literal.Token.ValueText;
                    break;
            }
        }
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj --filter "FullyQualifiedName~FluentPropertyWalkerTests"`
Expected: PASS (11 tests).

- [ ] **Step 5: Build + format**

Run: `dtk dotnet build ProjGraph.slnx`
Expected: build succeeds, no warnings.

Run: `dtk dotnet format ProjGraph.slnx --verify-no-changes`
Expected: nothing to format.

- [ ] **Step 6: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentPropertyWalker.cs \
        tests/ProjGraph.Tests.Unit.EntityFramework/FluentPropertyWalkerTests.cs
git commit -m "feat(ef): property walker marks primary keys from HasKey (single + composite)"
```

---

### Task 3: Wire the walker behind the seam (context path) and prove golden parity

**Files:**
- Modify: `src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs`

**Interfaces:**
- Consumes: `FluentPropertyWalker.Apply` (Tasks 1–2).
- Produces: `ApplyFluentApiConstraints` routes property/key config through `FluentPropertyWalker`; `ApplyConstraintsFromMethod` gains a `bool includeProperties = true` parameter (default preserves the snapshot path). No public signature is removed.

The seam change: the context path stops using `PropertyConfigParser` and instead (1) runs the existing section loop for **table mapping + fluent-only entity creation only**, then (2) runs `FluentPropertyWalker.Apply` for properties + primary keys, then (3) runs `FluentRelationshipWalker.Apply` (Slice 1) for relationships + FK. The snapshot path (`ModelSnapshotParser` → `ApplyConstraintsFromMethod`) is unchanged because both `includeRelationships` and `includeProperties` default to `true`.

- [ ] **Step 1: Route the context path through the property walker**

In `FluentApiConfigurationParser.cs`, replace the tail of `ApplyFluentApiConstraints` (the two-line comment plus the two calls after the null-guard) with:

```csharp
        // Context path: parse table config and materialize fluent-only entities from text (Slice 3 still
        // handles ToTable/owned/join), but derive property config, primary keys, relationships, and
        // foreign keys from the Roslyn syntax walkers instead of the regex parsers.
        ApplyConstraintsFromMethod(
            methodSyntax, entities, model, compilation, includeRelationships: false, includeProperties: false);
        FluentPropertyWalker.Apply(methodSyntax, entities, compilation);
        FluentRelationshipWalker.Apply(methodSyntax, entities, model, compilation);
```

- [ ] **Step 2: Thread `includeProperties` through the section parser**

In `FluentApiConfigurationParser.cs`, add the flag to `ApplyConstraintsFromMethod` and forward it. Change its signature to:

```csharp
    public static void ApplyConstraintsFromMethod(
        MethodDeclarationSyntax methodSyntax,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        bool includeRelationships = true,
        bool includeProperties = true)
```

and its loop body call to:

```csharp
            ProcessEntityConfigSection(
                entityConfigSections[i], entities, model, compilation, includeRelationships, includeProperties);
```

Update the `<param>` XML doc block of `ApplyConstraintsFromMethod` to add, after the existing `includeRelationships` `<param>`:

```csharp
    /// <param name="includeProperties">
    /// When <see langword="true"/> (the default, used by the snapshot path), property and key config is
    /// parsed via <see cref="PropertyConfigParser"/>. The context path passes <see langword="false"/> and
    /// derives it from <see cref="FluentPropertyWalker"/> instead.
    /// </param>
```

Update `ProcessEntityConfigSection` to accept and forward the flag:

```csharp
    private static void ProcessEntityConfigSection(
        string sectionContent,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        bool includeRelationships,
        bool includeProperties)
    {
        // Add back "Entity" which was removed by the split
        var section = EfAnalysisConstants.EfMethods.Entity + sectionContent;

        // Extract just this entity's configuration (up to the next .Entity)
        var entityConfigEnd = EfAnalysisRegexPatterns.EntitySplitRegex().Match(section, 7).Index;
        if (entityConfigEnd > 0)
        {
            section = section[..entityConfigEnd];
        }

        var shadowRelationships = ParseEntityConfiguration(
            section, entities, model, compilation, includeRelationships, includeProperties);
        AddUniqueRelationships(shadowRelationships, model);
    }
```

- [ ] **Step 3: Gate the property parser in `ParseEntityConfiguration`**

In `FluentApiConfigurationParser.cs`, add the `bool includeProperties` parameter to `ParseEntityConfiguration`:

```csharp
    private static List<EfRelationship> ParseEntityConfiguration(
        string configSection,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        bool includeRelationships,
        bool includeProperties)
```

and guard the `PropertyConfigParser` call. Replace:

```csharp
        PropertyConfigParser.ParsePropertyConfigurations(configSection, entity, compilation);
```

with:

```csharp
        if (includeProperties)
        {
            PropertyConfigParser.ParsePropertyConfigurations(configSection, entity, compilation);
        }
```

(Entity creation and the `ToTable` block in `ParseEntityConfiguration` are unaffected — they run regardless of the flags, so fluent-only entities and table names are still materialized before the walkers run.)

- [ ] **Step 4: Run the golden + advanced EF suite (parity gate)**

Run: `dtk dotnet test tests/ProjGraph.Tests.Unit.EntityFramework/ProjGraph.Tests.Unit.EntityFramework.csproj`
Expected: PASS — in particular:
- `EfErdGoldenTests` — all 7 goldens byte-identical, **no golden regenerated**. `fixture-property-config` (IsRequired/HasMaxLength/HasPrecision/HasDefaultValue/HasColumnType), `complex-ecommerce` (`ProductSupplier` composite `HasKey` → `PK,FK`), and `fixture-owned-join` (`Customer` has no phantom `City`) are the load-bearing cases.
- `EnumDefaultValueTests` (`ShouldUseEnumValueForDefaultValue` → `"1"`, `ShouldStillShortenNamesWhenNotResolvable` → `"Guest"`) and `ConstStringDefaultValueTests` — these call `ApplyFluentApiConstraints` directly and now exercise the walker's `HasDefaultValue`/`HasDefaultValueSql` path.
- `EfAnalysisAdvancedTests` (`ShouldMarkCompositePrimaryKeyAsForeignKey` in particular, which depends on `HasKey` PK marking + relationship-walker FK marking coexisting on the same property).

If any golden diff appears, DO NOT regenerate the golden. Diagnose the walker against the specific fixture:
- Compare `dotnet run --project src/ProjGraph.Cli -- erd <fixture>` output to the committed `.mmd`.
- Likely culprits and fixes:
  - **Phantom property on the outer entity** (e.g. `City` on `Customer`) → the `Property`/`HasKey` is inside `OwnsOne`/`OwnsMany`/`UsingEntity`; confirm `IsInsideNestedBuilderScope` matches its enclosing call and that the constant set includes that method.
  - **Missing PK/config** → confirm `ResolveOwningEntity` finds the entity (chain form vs. enclosing-lambda form) and that the entity is in the dict (it is created by `ApplyConstraintsFromMethod`'s section loop, which still runs).
  - **Wrong default value / max-length** → confirm `argText` (`ArgumentList.Arguments.ToString()`) equals the regex parser's captured group (quotes included for string literals) and that it is passed to `PropertyConfigParser.ApplyConfiguration`.
- Fix the walker (Tasks 1–2 files), re-run, and add a regression unit test to `FluentPropertyWalkerTests` reproducing the fixture's construct.

- [ ] **Step 5: Full suite + format**

Run: `dtk dotnet test ProjGraph.slnx`
Expected: all pass — including `EfAnalysisServiceSnapshotTests` (the snapshot path is untouched; `includeProperties` defaults to `true`, so `PropertyConfigParser` still runs there and its output is byte-identical) and the integration/contract suites.

Run: `dtk dotnet format ProjGraph.slnx --verify-no-changes`
Expected: nothing to format.

- [ ] **Step 6: Commit**

```bash
git add src/ProjGraph.Lib.EntityFramework/Infrastructure/FluentApiConfigurationParser.cs
git commit -m "$(cat <<'MSG'
feat(ef): route OnModelCreating property config through the Roslyn walker

The DbContext path now derives property config and primary keys from
FluentPropertyWalker (syntax-tree chains) instead of the regex
PropertyConfigParser. Table mapping and fluent-only entity creation still run
through the text section loop (Slice 3). The snapshot path keeps
PropertyConfigParser via ApplyConstraintsFromMethod's includeProperties default
(retired in Slice 6). Golden files unchanged — output is byte-identical.
MSG
)"
```

---

## Notes for the reviewer

- **Scope boundary.** This slice replaces property/key parsing **only on the DbContext `OnModelCreating` path**. `PropertyConfigParser` is intentionally retained for the snapshot path (`ModelSnapshotParser` → `ApplyConstraintsFromMethod`), which Slice 6 migrates. This is the strangler-fig "old and new briefly coexist" state; no regex file is deleted yet.
- **Why delegate value application to `PropertyConfigParser.ApplyConfiguration`.** The §4.3 fragility class is the *forward text scan* (the 10-match `MethodCallRegex` window, the paren-balance `IsInsideUsingEntityBlock`, cross-statement `currentProperty` bleed) — the walker eliminates all of it by reading the syntax spine. The remaining leaf-level value parsing (`HasPrecision` comma split, `HasColumnType` parens, `HasDefaultValue` constant resolution) is not fragile and is shared with the snapshot path via `ApplyConfiguration`; it is retired with `PropertyConfigParser` in Slice 6. This mirrors Slice 1 reusing `RelationshipConfigParser.CreateShadowRelationship`.
- **Duplicated syntax helpers.** `SimpleName`/`TypeName`/`LastSegment`/`GenericTypeArgumentName`/`EntityNameFromInvocation` (and the `IsInside…` shape) exist in both walkers. Consolidating into a shared `FluentChain`/syntax helper is deferred to Slice 6, when `RelationshipConfigParser`/`PropertyConfigParser` are deleted and the walkers are the sole owners — matching the Slice-1 plan's explicit deferral.
- **Why the property walker runs before the relationship walker.** Property config and FK marking touch disjoint `EfProperty` fields via `CopyWith`, and PK/FK are independent flags, so the order is output-neutral; the property-then-relationship order preserves Slice 1's ordering exactly. Verified by `complex-ecommerce` (`ProductSupplier.ProductId` renders `PK,FK`).
- **Nested-builder exclusion is a parity requirement, not a new feature.** Without it, `OwnedAndJoinContext` would gain a phantom `City` property on `Customer`; the regex parser hid this by absorbing the nested `.Property` into the `OwnsOne` argument match. `UsingEntity` is included in the set for symmetry with Slice 1 and forward-compatibility with the Slice-3 join-entity work.

## Self-review

- **Spec coverage (Slice 2 line):** "`Property/HasKey/HasMaxLength/HasColumnType/HasDefaultValue[Sql]/HasPrecision/IsRequired`. Retire `PropertyConfigParser` regex." — `Property`, `HasKey`, and all six config methods covered (Tasks 1–2 via `ApplyPropertyChain` + `ApplyKey`, delegating values to `ApplyConfiguration`); `PropertyConfigParser` retired **on the context path** (Task 3), snapshot deferred to Slice 6 with written rationale. The EF-internal Low `#13` (`ToTable` schema overload) lives in the `ToTable` text block, which is untouched here and belongs to Slice 3. ✔
- **Placeholder scan:** No TBD/TODO; all walker, constant, seam, and test code shown in full; commands and expected output given; the parity-diagnosis step (Task 3 Step 4) lists concrete failure modes and fixes rather than "handle edge cases." ✔
- **Type consistency:** `FluentPropertyWalker.Apply(MethodDeclarationSyntax, Dictionary<string,EfEntity>, Compilation)`, `FindConfigRoots`, `ResolveOwningEntity`, `ApplyPropertyChain`, `ApplyKey`, `KeyPropertyNames`, `ReplaceProperty`, `PropertyConfigParser.ApplyConfiguration(EfProperty, string, string, Compilation)`, `EfAnalysisConstants.EfMethods.OwnsOne/OwnsMany`, and `ApplyConstraintsFromMethod(..., bool includeRelationships = true, bool includeProperties = true)` are used consistently across Tasks 1–3. The walker delegates config-value application to the existing `PropertyConfigParser.ApplyConfiguration` (no duplication of the value switch). ✔
- **Scope:** One reviewable deliverable — the property/key walker plus its context-path wiring, gated by existing goldens. Table mapping + owned/join internals (Slice 3) and the snapshot migration (Slice 6) are separate plans. ✔
