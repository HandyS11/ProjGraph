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
    internal readonly record struct ConfigClass(string ClassName, string EntityName, MethodDeclarationSyntax Configure);

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

            FluentSyntax.MaterializeEntity(configClass.EntityName, entities, model, compilation);

            // Mirrors the DbContext/snapshot paths' pass ordering (FluentApiConfigurationParser,
            // ModelSnapshotParser), applied per config class with T as the ambient entity: the first
            // FluentEntityWalker pass applies any ToTable on T itself; FluentOwnedTypeWalker.Apply then
            // captures OwnsOne/OwnsMany calls in the Configure body (e.g. eShopOnWeb's
            // Order.OwnsOne(o => o.ShipToAddress, ...), configured from an
            // IEntityTypeConfiguration<Order>); FluentPropertyWalker derives T's own property config; the
            // second FluentEntityWalker pass then applies a ToTable chained onto the OwnsOne call itself
            // (e.g. `builder.OwnsOne(v => v.Warehouse).ToTable("X")`) — that owned entity does not exist
            // in the dictionary during the first pass, so the call is a no-op then and idempotent now.
            FluentEntityWalker.Apply(configClass.Configure, entities, model, compilation, configClass.EntityName);
            FluentOwnedTypeWalker.Apply(configClass.Configure, entities, model, compilation, configClass.EntityName);
            FluentPropertyWalker.Apply(configClass.Configure, entities, compilation, configClass.EntityName);
            FluentEntityWalker.Apply(configClass.Configure, entities, model, compilation, configClass.EntityName);
            FluentRelationshipWalker.Apply(configClass.Configure, entities, model, compilation, configClass.EntityName);
        }

        // Deliberately does NOT call FluentOwnedTypeWalker.ResolveTables here. This method folds in only
        // the config-class subset of a context's configuration — an owned type captured directly in
        // OnModelCreating (outside any config class) can still be waiting for ITS owner's ToTable, which
        // may live in a config class folded in by a LATER caller (e.g. a second ApplyConfiguration call
        // this method hasn't reached yet, or — the bug this comment replaces — one already folded in but
        // whose owned type was resolved too early by a premature global pass). ResolveTables is
        // idempotent-BY-SKIP (FluentOwnedTypeWalker.ResolveTables leaves TableName alone once set), so
        // running it here would permanently freeze any owned type it reaches at that point, uncorrectable
        // by a later, correct pass. Finalizing table resolution is therefore the orchestrator's job, run
        // once, globally, after every configuration pass — including this one — has run
        // (FluentApiConfigurationParser.ApplyFluentApiConstraints; ModelSnapshotParser.Parse has no
        // config-class pass, so it just runs ResolveTables after its own single sweep). Callers that
        // invoke this method directly — this walker's own unit tests included — must call
        // FluentOwnedTypeWalker.ResolveTables themselves afterwards if they need to observe an owned
        // type's effective table, exactly as the real orchestrator does.
    }

    /// <summary>Collects the config-class type names from every <c>ApplyConfiguration(new X())</c> call in the method.</summary>
    /// <param name="method">The method to scan.</param>
    private static HashSet<string> CollectExplicitConfigNames(MethodDeclarationSyntax method)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax ma
                || FluentSyntax.SimpleName(ma.Name) != EfAnalysisConstants.EfMethods.ApplyConfiguration)
            {
                continue;
            }

            if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is ObjectCreationExpressionSyntax creation)
            {
                names.Add(FluentSyntax.TypeName(creation.Type));
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
                               && FluentSyntax.SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.ApplyConfigurationsFromAssembly);
    }

    /// <summary>Enumerates every class in the compilation that implements <c>IEntityTypeConfiguration&lt;T&gt;</c> and has a <c>Configure</c> method.</summary>
    /// <param name="compilation">The compilation whose syntax trees are scanned.</param>
    private static IEnumerable<ConfigClass> FindConfigClasses(Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            foreach (var configClass in FindConfigClassesInRoot(tree.GetRoot()))
            {
                yield return configClass;
            }
        }
    }

    /// <summary>
    /// Enumerates every class in a single syntax root that implements <c>IEntityTypeConfiguration&lt;T&gt;</c>
    /// and has a <c>Configure</c> method. Purely syntactic — no semantic model required — so it can also run
    /// during entity-file discovery, before a compilation exists (<see cref="EfModelAnalyzer"/>'s pre-pass
    /// that seeds owned-navigation CLR types from config classes as well as <c>OnModelCreating</c>).
    /// </summary>
    /// <param name="root">The syntax root to scan.</param>
    internal static IEnumerable<ConfigClass> FindConfigClassesInRoot(SyntaxNode root)
    {
        // TypeDeclarationSyntax, not ClassDeclarationSyntax: a record (or struct) can implement
        // IEntityTypeConfiguration<T> too, and a class-only scan would silently skip its Configure body —
        // the same hazard family that made record-declared owners invisible to owned-nav discovery.
        foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (AsConfigClass(declaration) is { } configClass)
            {
                yield return configClass;
            }
        }
    }

    /// <summary>Interprets a type declaration as a config class, or returns <see langword="null"/> if it is not one.</summary>
    /// <param name="declaration">The type declaration.</param>
    private static ConfigClass? AsConfigClass(TypeDeclarationSyntax declaration)
    {
        // EF's ApplyConfigurationsFromAssembly only instantiates concrete types (a record compiles to
        // a class, so it qualifies); an interface extending IEntityTypeConfiguration<T> with a
        // default-implemented Configure is never applied at runtime and must not be folded in here.
        if (declaration is InterfaceDeclarationSyntax)
        {
            return null;
        }

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

        var entityName = FluentSyntax.TypeName(configInterface.TypeArgumentList.Arguments[0]);
        return new ConfigClass(declaration.Identifier.Text, entityName, configure);
    }
}
