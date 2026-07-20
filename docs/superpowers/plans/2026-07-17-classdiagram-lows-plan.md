# Class-diagram Lows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the ten remaining class-diagram audit findings (spec: `docs/superpowers/specs/2026-07-17-classdiagram-lows-design.md`).

**Architecture:** All changes live in `ProjGraph.Lib.ClassDiagram` (analyzer, resolver, discovery, renderer, two use cases). Each task is one finding: failing test → minimal fix → green → commit. No public API or CLI surface changes.

**Tech Stack:** .NET 10, Roslyn (`Microsoft.CodeAnalysis`), xUnit + AwesomeAssertions (`.Should()`), NSubstitute, `RoslynTestHelper` / `TestDirectory` from `ProjGraph.Tests.Shared`.

## Global Constraints

- `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`; XML docs required on all public APIs.
- Test conventions: `[Trait("Category", "Unit")]`, test names `Method_Scenario_ShouldOutcome`.
- `ProjGraph.Lib.ClassDiagram` has `InternalsVisibleTo` for the unit test project — internals are directly testable.
- Run tests with: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "<ClassName>"`.
- Commit after every task; commit messages end with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Task order matters: Task 1 (F10) removes code that Task 2 (F7) would otherwise have to stub; Task 4 (F2) must precede Task 5 (F1) so cardinality assertions hold.

---

### Task 1: F10 — single deterministic workspace scan

**Files:**
- Modify: `src/ProjGraph.Lib.ClassDiagram/Infrastructure/WorkspaceTypeDiscovery.cs:28-53`
- Test: `tests/ProjGraph.Tests.Unit.ClassDiagram/WorkspaceTypeDiscoveryTests.cs` (create)

**Interfaces:**
- Consumes: `PhysicalFileSystem` (public, `ProjGraph.Lib.Core.Infrastructure`), `TestDirectory` (`ProjGraph.Tests.Shared.Helpers`).
- Produces: `FindTypeDefinitionFileAsync(string typeName, string startDirectory)` unchanged signature; now always a single root scan with path-sorted tie-break.

- [ ] **Step 1: Write the failing test**

Create `tests/ProjGraph.Tests.Unit.ClassDiagram/WorkspaceTypeDiscoveryTests.cs`:

```csharp
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.ClassDiagram;

/// <summary>
/// Tests for <see cref="WorkspaceTypeDiscovery"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkspaceTypeDiscoveryTests
{
    [Fact]
    public async Task FindTypeDefinitionFileAsync_TypeInMultipleDirectories_ShouldPickPathSortedFirst()
    {
        // The former optimistic "common directory" pass (Models/, Entities/, ...) returned its
        // hit directly, overriding the deterministic path-sort tie-break of the root scan.
        // Resolution must be deterministic regardless of which directory contains the type.
        using var dir = new TestDirectory();
        dir.CreateFile("Models/X.cs", "namespace M; public class X { }");
        dir.CreateFile("Api/X.cs", "namespace A; public class X { }");
        var sut = new WorkspaceTypeDiscovery(new PhysicalFileSystem());

        var found = await sut.FindTypeDefinitionFileAsync("X", dir.DirectoryPath);

        found.Should().Be(Path.Combine(dir.DirectoryPath, "Api", "X.cs"));
    }
}
```

(If `PhysicalFileSystem` needs different construction, check `src/ProjGraph.Lib.Core/Infrastructure/PhysicalFileSystem.cs` — it is the production `IFileSystem`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "WorkspaceTypeDiscoveryTests"`
Expected: FAIL — actual is `.../Models/X.cs` (optimistic pass hit).

- [ ] **Step 3: Implement**

In `WorkspaceTypeDiscovery.cs`, delete the optimistic loop so `FindTypeDefinitionFileAsync` becomes:

```csharp
    public async Task<string?> FindTypeDefinitionFileAsync(string typeName, string startDirectory)
    {
        var root = WorkspaceRootResolver.FindWorkspaceRoot(startDirectory) ?? startDirectory;

        // A single scan from the workspace root keeps resolution deterministic: a partial
        // "common directory" pre-pass would return its first hit and override the path-sorted
        // tie-break applied below. Lookups are memoized per type name by the caller.
        return await SearchDirectoryForTypeAsync(root, typeName);
    }
```

Update the class/method XML doc summary accordingly (no more "common subdirectories first"). Remove the now-unused `fileSystem.Combine`/`DirectoryExists` usages if nothing else references them.

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram`
Expected: PASS (all).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "fix(classdiagram): deterministic single-pass workspace type scan

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: F7 — guard directory enumeration in workspace scan

**Files:**
- Modify: `src/ProjGraph.Lib.ClassDiagram/Infrastructure/WorkspaceTypeDiscovery.cs` (`CollectTypeMatchesAsync`)
- Test: `tests/ProjGraph.Tests.Unit.ClassDiagram/WorkspaceTypeDiscoveryTests.cs`

**Interfaces:**
- Consumes: `IFileSystem.EnumerateFiles/EnumerateDirectories(string, string, EnumerationOptions)`, `FilePathGuard.CSharpFilesPattern` (`"*.cs"`, `ProjGraph.Lib.Core.Abstractions`).
- Produces: `CollectTypeMatchesAsync` never lets `IOException`/`UnauthorizedAccessException` from enumeration escape; partial results before the failure are kept.

- [ ] **Step 1: Write the failing test**

Add to `WorkspaceTypeDiscoveryTests.cs` (add `using NSubstitute;`, `using ProjGraph.Lib.Core.Abstractions;`):

```csharp
    [Fact]
    public async Task FindTypeDefinitionFileAsync_EnumerationThrowsMidScan_ShouldKeepPartialResults()
    {
        // A directory deleted mid-scan (or a symlink cycle) surfaces IOException from the lazy
        // enumeration itself, not from a file read. That must degrade to a partial scan, not
        // abort the whole analysis.
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EnumerationOptions>())
            .Returns(_ => OneFileThenThrow("/ws/A.cs"));
        fileSystem.EnumerateDirectories(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EnumerationOptions>())
            .Returns([]);
        fileSystem.ReadAllTextAsync("/ws/A.cs").Returns("namespace W; public class Target { }");
        var sut = new WorkspaceTypeDiscovery(fileSystem);

        var found = await sut.FindTypeDefinitionFileAsync("Target", "/ws");

        found.Should().Be("/ws/A.cs");
    }

    private static IEnumerable<string> OneFileThenThrow(string file)
    {
        yield return file;
        throw new IOException("directory removed during enumeration");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "WorkspaceTypeDiscoveryTests"`
Expected: FAIL — unhandled `IOException` propagates out of `FindTypeDefinitionFileAsync`.

- [ ] **Step 3: Implement**

In `CollectTypeMatchesAsync`, materialize both enumerations defensively, keeping whatever was yielded before the failure:

```csharp
        var files = new List<string>();
        try
        {
            files.AddRange(fileSystem.EnumerateFiles(directory, FilePathGuard.CSharpFilesPattern,
                enumerationOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Enumeration itself can fail mid-iteration (directory deleted, symlink cycle).
            // Keep the entries already yielded and degrade instead of aborting the analysis.
        }

        foreach (var file in files)
        {
            // ... existing per-file body unchanged ...
        }

        var subDirectories = new List<string>();
        try
        {
            subDirectories.AddRange(fileSystem.EnumerateDirectories(directory, "*", enumerationOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Same degradation for the subdirectory walk.
        }

        foreach (var subDir in subDirectories)
        {
            if (DirectoryFilters.ShouldSkipDirectory(subDir))
            {
                continue;
            }

            await CollectTypeMatchesAsync(subDir, typeName, matches);
        }
```

Note: `List<string>.AddRange` still throws when the underlying iterator throws — the entries added before the throw are preserved because `AddRange` over an `IEnumerable` adds one at a time. Verify with the test; if the framework's `AddRange` proves all-or-nothing for this shape, fall back to an explicit `foreach { files.Add(f); }` inside the `try`.

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram`
Expected: PASS (all).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "fix(classdiagram): survive IO errors during workspace directory enumeration

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: F6 — array unwrap for method return/parameter types

**Files:**
- Modify: `src/ProjGraph.Lib.ClassDiagram/Infrastructure/RelationshipAnalyzer.cs:83-88,116-133`
- Test: `tests/ProjGraph.Tests.Unit.ClassDiagram/RelationshipAnalyzerTests.cs`

**Interfaces:**
- Produces: private helper `static ITypeSymbol UnwrapArrayElementType(ITypeSymbol type)` used by both the member path and the method path (Tasks 4-6 leave it untouched).

- [ ] **Step 1: Write the failing test**

Add to `RelationshipAnalyzerTests.cs`:

```csharp
    [Fact]
    public void AddDependencyRelationships_ArrayMethodReturnAndParameter_ShouldAddDependency()
    {
        // The array unwrap added for members must also apply to method signatures:
        // Order[] GetAll() / Save(Order[] batch) are dependencies exactly like List<Order>.
        const string code = """
                            namespace Test;
                            public class Order { }
                            public class Repository
                            {
                                public Order[] GetAll() => [];
                                public void Save(Order[] batch) { }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Repository")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r =>
            r.Kind == RelationshipKind.Dependency &&
            r.Symbol.Name == "Order");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "RelationshipAnalyzerTests"`
Expected: FAIL — no `Order` dependency (arrays skipped by the `INamedTypeSymbol` guard).

- [ ] **Step 3: Implement**

Add the helper and use it in both paths:

```csharp
    /// <summary>
    /// Unwraps an array type (including jagged arrays) to its innermost element type.
    /// Non-array types are returned unchanged.
    /// </summary>
    /// <param name="type">The type to unwrap.</param>
    /// <returns>The innermost element type, or the input type when it is not an array.</returns>
    private static ITypeSymbol UnwrapArrayElementType(ITypeSymbol type)
    {
        while (type is IArrayTypeSymbol arrayType)
        {
            type = arrayType.ElementType;
        }

        return type;
    }
```

Method loop (line 83-88) — unwrap before the guard:

```csharp
        foreach (var type in methodReturnTypes.Concat(methodParamTypes))
        {
            if (UnwrapArrayElementType(type) is not INamedTypeSymbol { SpecialType: SpecialType.None } namedType)
            {
                continue;
            }
            // ... rest unchanged ...
```

`ProcessMemberType` array branch — replace the inline `while` with the helper:

```csharp
        if (type is IArrayTypeSymbol)
        {
            if (UnwrapArrayElementType(type) is not INamedTypeSymbol { SpecialType: SpecialType.None } arrayElement)
            {
                return;
            }

            cardinality = "*";
            extractedTypes = ExtractTypesFromGeneric(arrayElement);
        }
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram`
Expected: PASS (all).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "fix(classdiagram): array-typed method returns/parameters produce dependency edges

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: F2 — semantic collection detection

**Files:**
- Modify: `src/ProjGraph.Lib.ClassDiagram/Infrastructure/RelationshipAnalyzer.cs:136-144`
- Test: `tests/ProjGraph.Tests.Unit.ClassDiagram/RelationshipAnalyzerTests.cs`

**Interfaces:**
- Produces: private helper `static bool IsCollectionType(INamedTypeSymbol type)`.

- [ ] **Step 1: Write the failing tests**

```csharp
    [Fact]
    public void AddDependencyRelationships_NonCollectionGenericContainingSet_ShouldHaveSingleCardinality()
    {
        // "Settings" contains the substring "Set" — the old name heuristic wrongly classified
        // it as a collection. Collection-ness must come from IEnumerable, not the type name.
        const string code = """
                            namespace Test;
                            public class Theme { }
                            public class Settings<T> { }
                            public class App
                            {
                                public Settings<Theme> Config { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "App")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r => r.Symbol.Name == "Theme" && r.Cardinality == "1");
    }

    [Fact]
    public void AddDependencyRelationships_CustomEnumerableGeneric_ShouldHaveStarCardinality()
    {
        // A user collection whose name matches no magic substring must still count as one.
        const string code = """
                            namespace Test;
                            public class Item { }
                            public class ItemBag<T> : System.Collections.Generic.List<T> { }
                            public class Order
                            {
                                public ItemBag<Item> Items { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r => r.Symbol.Name == "Item" && r.Cardinality == "*");
    }

    [Fact]
    public void AddDependencyRelationships_UnresolvedCollectionNamedType_ShouldKeepStarCardinality()
    {
        // Unresolved (error) symbols carry no interface info; the name heuristic remains the
        // best-effort fallback so existing behavior for unresolved collections is preserved.
        const string code = """
                            namespace Test;
                            public class Item { }
                            public class Order
                            {
                                public MyCollection<Item> Items { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r => r.Symbol.Name == "Item" && r.Cardinality == "*");
    }
```

- [ ] **Step 2: Run tests to verify expected failures**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "RelationshipAnalyzerTests"`
Expected: `NonCollectionGenericContainingSet` FAILS (gets "\*"), `CustomEnumerableGeneric` FAILS (gets "1"), `UnresolvedCollectionNamedType` PASSES (documents preserved behavior).

- [ ] **Step 3: Implement**

Replace the substring check in `ProcessMemberType`:

```csharp
        else if (type is INamedTypeSymbol { SpecialType: SpecialType.None } namedType)
        {
            cardinality = IsCollectionType(namedType) ? "*" : "1";
            extractedTypes = ExtractTypesFromGeneric(namedType);
        }
```

Add the helper:

```csharp
    /// <summary>
    /// Determines whether a member type represents a collection (rendered with '*' cardinality).
    /// A resolved type is a collection iff it is, or implements, <see cref="System.Collections.IEnumerable"/>.
    /// Unresolved (error) symbols carry no interface information, so the legacy name heuristic
    /// is kept as a best-effort fallback for them.
    /// </summary>
    /// <param name="type">The member type to classify.</param>
    private static bool IsCollectionType(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Error)
        {
            return type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                   || type.AllInterfaces.Any(i => i.SpecialType == SpecialType.System_Collections_IEnumerable);
        }

        return type.IsGenericType &&
               (type.Name.Contains("List", StringComparison.Ordinal) ||
                type.Name.Contains("Collection", StringComparison.Ordinal) ||
                type.Name.Contains("IEnumerable", StringComparison.Ordinal) ||
                type.Name.Contains("Array", StringComparison.Ordinal) ||
                type.Name.Contains("Set", StringComparison.Ordinal));
    }
```

(`string` never reaches this path — the `SpecialType.None` guard excludes it. `TypeKind` here is the aliased `Microsoft.CodeAnalysis.TypeKind` — the file already has `using TypeKind = Microsoft.CodeAnalysis.TypeKind;`.)

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram`
Expected: PASS (all). If an existing test pinned the substring behavior for a resolved type, re-examine it against the spec — semantic classification wins.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "fix(classdiagram): classify collections by IEnumerable, not name substrings

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: F1 — keep user-defined generic outer types

**Files:**
- Modify: `src/ProjGraph.Lib.ClassDiagram/Infrastructure/RelationshipAnalyzer.cs` (`ExtractTypesFromGeneric`)
- Test: `tests/ProjGraph.Tests.Unit.ClassDiagram/RelationshipAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void AddDependencyRelationships_UserGenericProperty_ShouldKeepOuterAndArgumentTypes()
    {
        // Result<Order> must produce edges to BOTH the user-defined container Result<T> and the
        // argument Order. Only BCL containers (List<T>, ...) are reduced to their arguments.
        const string code = """
                            namespace Test;
                            public class Order { }
                            public class Result<T> { }
                            public class Handler
                            {
                                public Result<Order> Outcome { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Handler")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r => r.Symbol.Name == "Result" && r.Label == "Outcome");
        related.Should().Contain(r => r.Symbol.Name == "Order" && r.Label == "Outcome");
    }

    [Fact]
    public void AddDependencyRelationships_BclGenericProperty_ShouldNotAddContainerType()
    {
        const string code = """
                            namespace Test;
                            public class Item { }
                            public class Order
                            {
                                public System.Collections.Generic.List<Item> Items { get; set; }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Order")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Should().Contain(r => r.Symbol.Name == "Item");
        related.Should().NotContain(r => r.Symbol.Name == "List");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "RelationshipAnalyzerTests"`
Expected: `UserGenericProperty` FAILS (no `Result` edge); `BclGenericProperty` PASSES (pins existing behavior).

- [ ] **Step 3: Implement**

In `ExtractTypesFromGeneric`, add the outer type before recursing into arguments:

```csharp
        if (type is { IsGenericType: true, TypeArguments.Length: > 0 })
        {
            // A user-defined generic container is itself a participant in the relationship:
            // Result<Order> keeps an edge to Result<T>, not only to Order. BCL containers
            // stay reduced to their arguments — we don't want List<T> nodes in the diagram.
            if (!TypeFilter.IsSystemType(type))
            {
                result.Add(type.OriginalDefinition);
            }

            foreach (var typeArg in type.TypeArguments)
            {
                // ... existing body unchanged ...
```

Update the `ExtractTypesFromGeneric` XML doc (`Result<Order>` example) to describe the new contract.

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram`
Expected: PASS (all).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "fix(classdiagram): keep user-defined generic container types in relationships

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 6: F3 — fully-qualified dedupe keys

**Files:**
- Modify: `src/ProjGraph.Lib.ClassDiagram/Infrastructure/RelationshipAnalyzer.cs:53,56,93,155`
- Test: `tests/ProjGraph.Tests.Unit.ClassDiagram/RelationshipAnalyzerTests.cs`

**Interfaces:**
- Consumes: `TypeAnalyzer.GetFullyQualifiedName(INamedTypeSymbol)` (public static, same assembly).

- [ ] **Step 1: Write the failing tests**

```csharp
    [Fact]
    public void AddDependencyRelationships_SameNameDifferentNamespaces_MethodTypes_ShouldAddBoth()
    {
        // Dedupe keyed on simple names dropped the second A.Order/B.Order edge silently.
        const string code = """
                            namespace A { public class Order { } }
                            namespace B { public class Order { } }
                            namespace Test
                            {
                                public class Report
                                {
                                    public A.Order GetFirst() => new();
                                    public B.Order GetSecond() => new();
                                }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Report")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Where(r => r.Kind == RelationshipKind.Dependency && r.Symbol.Name == "Order")
            .Should().HaveCount(2);
    }

    [Fact]
    public void AddDependencyRelationships_SameNameDifferentNamespaces_GenericArguments_ShouldAddBoth()
    {
        const string code = """
                            namespace A { public class Order { } }
                            namespace B { public class Order { } }
                            namespace Test
                            {
                                public class Mapping
                                {
                                    public System.Collections.Generic.Dictionary<A.Order, B.Order> Map { get; set; }
                                }
                            }
                            """;
        var (compilation, _) = RoslynTestHelper.CreateCompilationWithModel(code);
        var symbol = RoslynTestHelper.GetTypeSymbol(compilation, "Mapping")!;
        var related =
            new List<(INamedTypeSymbol Symbol, RelationshipKind Kind, string? Label, string? Cardinality)>();

        RelationshipAnalyzer.AddDependencyRelationships(symbol, related);

        related.Where(r => r.Symbol.Name == "Order" && r.Label == "Map")
            .Should().HaveCount(2);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "RelationshipAnalyzerTests"`
Expected: both FAIL with count 1.

- [ ] **Step 3: Implement**

Key both sets on the fully qualified name. Line 93:

```csharp
                    .Where(extracted => seenMethodTypes.Add(TypeAnalyzer.GetFullyQualifiedName(extracted)) &&
                                        !TypeFilter.IsSystemType(extracted))
```

Line 155:

```csharp
                .Where(extracted =>
                    seenCombinations.Add((TypeAnalyzer.GetFullyQualifiedName(extracted), memberName)) &&
                    !TypeFilter.IsSystemType(extracted))
```

Update the two comments at lines 52-56 ("Track unique type+label combinations…") to say the keys are fully-qualified names so cross-namespace same-named types stay distinct.

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram`
Expected: PASS (all).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "fix(classdiagram): dedupe relationship targets by fully-qualified name

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 7: F8 — external-type nodes keep generic arity

**Files:**
- Modify: `src/ProjGraph.Lib.ClassDiagram/Infrastructure/TypeAnalyzer.cs` (expose short-name helper)
- Modify: `src/ProjGraph.Lib.ClassDiagram/Infrastructure/SymbolResolver.cs:139-145`
- Test: `tests/ProjGraph.Tests.Unit.ClassDiagram/SymbolResolverTests.cs`

**Interfaces:**
- Produces: `TypeAnalyzer.GetShortName(INamedTypeSymbol symbol)` — `public static string`, returns `symbol.ToDisplayString(ShortNameFormat)` (e.g. `Result<T>`; renderer converts `<>` to `~~`).

- [ ] **Step 1: Write the failing test**

Add to `SymbolResolverTests.cs` (existing class; `_discovery` and `_fileSystem` substitutes and `CreateContext` helper already exist):

```csharp
    [Fact]
    public async Task ResolveRelatedSymbolAsync_UnresolvedGenericExternalType_ShouldKeepArityInNodeName()
    {
        // An external AbstractValidator<T> must not collapse to "AbstractValidator": in-source
        // generics render with their type parameters, external nodes must match.
        var compilation = RoslynTestHelper.CreateCompilation(
            "namespace MyApp; public class Person { }",
            "namespace MyApp; public class PersonValidator : AbstractValidator<Person> { }");
        var derived = RoslynTestHelper.GetTypeSymbol(compilation, "PersonValidator")!;
        var baseSymbol = derived.BaseType!; // error symbol: AbstractValidator<Person>
        _discovery.FindTypeDefinitionFileAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns((string?)null);
        var sut = new SymbolResolver(_discovery, _fileSystem);
        var context = CreateContext(compilation);

        var resolved = await sut.ResolveRelatedSymbolAsync(baseSymbol, context);

        resolved.Should().BeNull();
        context.Types.Should().ContainSingle(t => t.Name.StartsWith("AbstractValidator<"));
    }
```

(The compilation warns about the unresolved base only at emit; if `CreateCompilation` treats errors strictly, check how the existing error-symbol tests in this file construct theirs and mirror that. Assert on the actual display form observed — `AbstractValidator<Person>` or the open form — as long as arity is present.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "SymbolResolverTests"`
Expected: FAIL — node name is bare `AbstractValidator`.

- [ ] **Step 3: Implement**

In `TypeAnalyzer`, add below `ShortNameFormat`:

```csharp
    /// <summary>
    /// Gets the short display name of a type, including its generic type parameters
    /// (e.g. <c>Result&lt;T&gt;</c>) — the same format used for in-source type nodes.
    /// </summary>
    /// <param name="symbol">The type symbol to format.</param>
    /// <returns>The short display name.</returns>
    public static string GetShortName(INamedTypeSymbol symbol)
    {
        return symbol.ToDisplayString(ShortNameFormat);
    }
```

Optionally reuse it at `AnalyzeType`'s `symbol.ToDisplayString(ShortNameFormat)` call for the type name (not the member types — keep the diff minimal).

In `SymbolResolver.AddExternalType`, replace `relatedSymbol.Name`:

```csharp
        context.Types.Add(new TypeDefinition(
            TypeAnalyzer.GetShortName(relatedSymbol),
            relatedSymbol.ContainingNamespace.ToDisplayString(),
            fullName,
            TypeAnalyzer.MapKind(relatedSymbol),
            [],
            relatedSymbol.IsAbstract));
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram`
Expected: PASS (all).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "fix(classdiagram): external type nodes keep generic arity in display name

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 8: F4 — collision-aware Mermaid node IDs

**Files:**
- Modify: `src/ProjGraph.Lib.ClassDiagram/Rendering/MermaidClassDiagramRenderer.cs`
- Test: `tests/ProjGraph.Tests.Unit.ClassDiagram/MermaidClassDiagramRendererTests.cs`

**Interfaces:**
- Produces: private `static Dictionary<string, string> BuildNodeIds(ClassModel model)` (FullName → unique Mermaid ID) and `static string ResolveId(Dictionary<string, string> ids, string fullName)`; `RenderType`/`RenderRelationship` gain an `ids` parameter.

- [ ] **Step 1: Write the failing test**

Add to `MermaidClassDiagramRendererTests.cs` (match the file's existing model-construction style):

```csharp
    [Fact]
    public void Render_SanitizeCollision_ShouldKeepDistinctNodeIds()
    {
        // Ns.Foo_Bar and Ns.Foo.Bar both sanitize to Ns_Foo_Bar; without collision handling
        // the two types merge into a single Mermaid node and relationships cross-wire.
        var model = new ClassModel(
            "collision",
            [
                new TypeDefinition("Foo_Bar", "Ns", "Ns.Foo_Bar", TypeKind.Class, [], false),
                new TypeDefinition("Bar", "Ns.Foo", "Ns.Foo.Bar", TypeKind.Class, [], false)
            ],
            [
                new Relationship("Ns.Foo.Bar", "Ns.Foo_Bar", RelationshipKind.Association, "Target", "1")
            ]);
        var renderer = new MermaidClassDiagramRenderer();

        var output = renderer.Render(model);

        output.Should().Contain("class Ns_Foo_Bar [\"Foo_Bar\"]");
        output.Should().Contain("class Ns_Foo_Bar_2 [\"Bar\"]");
        output.Should().Contain("Ns_Foo_Bar_2 --> \"1\" Ns_Foo_Bar : Target");
    }
```

(Check the existing tests' `TypeDefinition`/`Relationship` constructor argument order in this file and match it exactly.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "MermaidClassDiagramRendererTests"`
Expected: FAIL — both types render as `Ns_Foo_Bar`, no `Ns_Foo_Bar_2`.

- [ ] **Step 3: Implement**

```csharp
    public string Render(ClassModel model, DiagramOptions? options = null)
    {
        var sb = new StringBuilder();
        var nodeIds = BuildNodeIds(model);

        MermaidFenceHelper.AppendFenceStart(sb, options, model.Title);

        sb.AppendLine("classDiagram");

        foreach (var type in model.Types)
        {
            RenderType(sb, type, nodeIds);
        }

        foreach (var relationship in model.Relationships)
        {
            RenderRelationship(sb, relationship, nodeIds);
        }

        MermaidFenceHelper.AppendFenceEnd(sb, options);

        return sb.ToString();
    }

    /// <summary>
    /// Assigns each type a unique Mermaid node ID. Sanitizing full names is lossy
    /// (<c>Ns.Foo_Bar</c> and <c>Ns.Foo.Bar</c> both map to <c>Ns_Foo_Bar</c>), so distinct
    /// full names that collide after sanitization get a deterministic numeric suffix in
    /// model order. Output only changes when a real collision exists.
    /// </summary>
    /// <param name="model">The model whose types receive IDs.</param>
    /// <returns>A map from type full name to unique Mermaid node ID.</returns>
    private static Dictionary<string, string> BuildNodeIds(ClassModel model)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in model.Types)
        {
            if (ids.ContainsKey(type.FullName))
            {
                continue;
            }

            var baseId = Sanitize(type.FullName);
            var id = baseId;
            var suffix = 2;
            while (!used.Add(id))
            {
                id = $"{baseId}_{suffix++}";
            }

            ids[type.FullName] = id;
        }

        return ids;
    }

    /// <summary>
    /// Resolves the Mermaid node ID for a type full name, falling back to plain sanitization
    /// for endpoints that are not part of the model's type list.
    /// </summary>
    /// <param name="ids">The ID map built by <see cref="BuildNodeIds"/>.</param>
    /// <param name="fullName">The type full name to resolve.</param>
    /// <returns>The Mermaid node ID.</returns>
    private static string ResolveId(Dictionary<string, string> ids, string fullName)
    {
        return ids.TryGetValue(fullName, out var id) ? id : Sanitize(fullName);
    }
```

`RenderType`: signature `(StringBuilder sb, TypeDefinition type, Dictionary<string, string> nodeIds)`, first line `var sanitizedName = ResolveId(nodeIds, type.FullName);`. `RenderRelationship`: signature `(StringBuilder sb, Relationship relationship, Dictionary<string, string> nodeIds)`, `var from = ResolveId(nodeIds, relationship.From); var to = ResolveId(nodeIds, relationship.To);`. Everything else unchanged.

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram`
Expected: PASS (all — non-colliding outputs are byte-identical, so no other renderer test moves).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "fix(classdiagram): disambiguate Mermaid node IDs on sanitize collisions

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 9: F5 — bare relative filename no longer aborts analysis

**Files:**
- Modify: `src/ProjGraph.Lib.ClassDiagram/Application/UseCases/AnalyzeFileUseCase.cs:38`
- Test: `tests/ProjGraph.Tests.Unit.ClassDiagram/AnalyzeFileUseCaseTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `AnalyzeFileUseCaseTests.cs` (reuse the file's `SetupCompilationFactory` helper; mirror `SetupFileSystem` manually because the path arguments differ):

```csharp
    [Fact]
    public async Task ExecuteAsync_BareRelativeFileName_ShouldResolveStartDirectoryFromFullPath()
    {
        // GetDirectoryName("Widget.cs") is "" (not null), which used to flow into
        // WorkspaceRootResolver as an empty start directory and throw ArgumentException.
        const string filePath = "Widget.cs";
        const string code = """
                            namespace Test;
                            public class Widget { }
                            """;
        _fileSystem.FileExists(filePath).Returns(true);
        _fileSystem.ReadAllTextAsync(filePath).Returns(code);
        _fileSystem.GetFullPath(filePath).Returns("/work/dir/Widget.cs");
        _fileSystem.GetDirectoryName("/work/dir/Widget.cs").Returns("/work/dir");
        _fileSystem.GetDirectoryName(filePath).Returns(string.Empty);
        SetupCompilationFactory();
        AnalysisContext? captured = null;
        _typeProcessor.ProcessTypeQueueAsync(
                Arg.Any<Queue<(INamedTypeSymbol Symbol, int Depth)>>(),
                Arg.Do<AnalysisContext>(c => captured = c),
                Arg.Any<AnalysisOptions>())
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(filePath);

        captured.Should().NotBeNull();
        captured!.StartDirectory.Should().Be("/work/dir");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "AnalyzeFileUseCaseTests"`
Expected: FAIL — `StartDirectory` is `""` (the empty string from `GetDirectoryName("Widget.cs")`).

- [ ] **Step 3: Implement**

Replace line 38:

```csharp
        // GetDirectoryName on a bare relative filename returns "" — resolve the full path first
        // so the workspace walk always starts from a real directory.
        var directoryName = fileSystem.GetDirectoryName(fileSystem.GetFullPath(filePath));
        var startDir = string.IsNullOrEmpty(directoryName) ? Environment.CurrentDirectory : directoryName;
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram`
Expected: PASS (all; existing tests stub `GetDirectoryName` for absolute paths — extend their stubs with `GetFullPath` returning the input if any now fail on the changed call chain).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "fix(classdiagram): analyzing a bare relative filename no longer aborts

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 10: F9 — directory title survives trailing separator

**Files:**
- Modify: `src/ProjGraph.Lib.ClassDiagram/Application/UseCases/AnalyzeDirectoryUseCase.cs:40-45,95`
- Test: `tests/ProjGraph.Tests.Unit.ClassDiagram/AnalyzeDirectoryUseCaseTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `AnalyzeDirectoryUseCaseTests.cs` (mirror the file's existing substitute setup):

```csharp
    [Fact]
    public async Task ExecuteAsync_TrailingDirectorySeparator_ShouldKeepDirectoryNameAsTitle()
    {
        // "projgraph class ./src/" — GetFullPath preserves the trailing separator, and
        // Path.GetFileName of "…/src/" is "", silently dropping the diagram title.
        const string input = "/proj/src/";
        _fileSystem.DirectoryExists(input).Returns(true);
        _fileSystem.GetFullPath(input).Returns("/proj/src/");
        _discoverCsFilesUseCase.Execute("/proj/src/").Returns([]);

        var result = await _sut.ExecuteAsync(input);

        result.Title.Should().Be("src");
    }
```

(Use the actual field names for the substitutes in that test class.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "AnalyzeDirectoryUseCaseTests"`
Expected: FAIL — `Title` is `""`.

- [ ] **Step 3: Implement**

In `ExecuteAsync`, compute the title once and use it at both return sites:

```csharp
        var fullPath = fileSystem.GetFullPath(directoryPath);

        // A trailing separator ("./src/") makes Path.GetFileName return "" — trim it so the
        // diagram title is always the directory name.
        var title = Path.GetFileName(Path.TrimEndingDirectorySeparator(fullPath));
        var csFiles = discoverCsFilesUseCase.Execute(fullPath);

        if (csFiles.Count == 0)
        {
            return new ClassModel(title, [], []);
        }
```

and line 95: `return new ClassModel(title, context.Types, context.Relationships);`

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram`
Expected: PASS (all).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "fix(classdiagram): keep diagram title when directory path has trailing separator

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 11: Full verification

**Files:** none (verification only)

- [ ] **Step 1: Run the entire suite**

Run: `dotnet test ProjGraph.slnx`
Expected: all projects PASS (baseline was 890 green). If an integration test pinned pre-fix diagram output (extra/missing edges, cardinality), update the expectation to the corrected behavior and note it in the commit message.

- [ ] **Step 2: Verify formatting/style**

Run: `dotnet format ProjGraph.slnx --verify-no-changes`
Expected: no changes needed. Fix and re-run if not.

- [ ] **Step 3: Commit any expectation adjustments**

```bash
git add -A && git commit -m "test: adjust integration expectations to corrected class-diagram output

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

(Skip if nothing changed.)
