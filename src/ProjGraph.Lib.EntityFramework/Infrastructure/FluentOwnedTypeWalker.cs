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

        var navigation = FluentSyntax.OwnedNavigationName(owns);
        if (string.IsNullOrEmpty(navigation))
        {
            return;
        }

        var key = $"{ownerKey}.{navigation}";
        GetOrCreateOwned(key, owns, owner, navigation, isCollection, entities, model, compilation);

        // The owned builder's own lambda configuration (e.g. OwnsOne(o => o.Address, a => a.Property(...))).
        // Calls chained onto the OwnsOne invocation instead (the form with no builder lambda) are resolved
        // to the owned key by the outer walkers' passes over the enclosing method, since they lie outside
        // this argument list.
        FluentPropertyWalker.Apply(owns.ArgumentList, entities, compilation, key);
        FluentEntityWalker.Apply(owns.ArgumentList, entities, model, compilation, key);
    }

    /// <summary>
    /// Creates the owned entity for <paramref name="key"/> on first sight, adding it to
    /// <paramref name="entities"/> and <paramref name="model"/>. Repeated calls targeting the same
    /// navigation (as in the chained form spread across statements) are no-ops, so they merge into one
    /// entity rather than duplicating it.
    /// </summary>
    /// <param name="key">The owned entity's dictionary key (<c>{Owner}.{Nav}</c>).</param>
    /// <param name="owns">The <c>OwnsOne</c>/<c>OwnsMany</c> invocation.</param>
    /// <param name="owner">The owning entity.</param>
    /// <param name="navigation">The owner's navigation property name for the owned type.</param>
    /// <param name="isCollection">Whether the owned type is a collection (<c>OwnsMany</c>).</param>
    /// <param name="entities">The known entities, augmented in place.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is augmented.</param>
    /// <param name="compilation">The compilation for owned-type symbol resolution.</param>
    private static void GetOrCreateOwned(
        string key,
        InvocationExpressionSyntax owns,
        EfEntity owner,
        string navigation,
        bool isCollection,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        if (entities.ContainsKey(key))
        {
            return;
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
    }

    /// <summary>
    /// Resolves the owned CLR type: from the explicit type-name literal on the snapshot path, else from the
    /// owner symbol's navigation property (unwrapping the collection element type for <c>OwnsMany</c>).
    /// </summary>
    /// <param name="owns">The <c>OwnsOne</c>/<c>OwnsMany</c> invocation.</param>
    /// <param name="owner">The owning entity.</param>
    /// <param name="navigation">The owner's navigation property name for the owned type.</param>
    /// <param name="isCollection">Whether the owned type is a collection (<c>OwnsMany</c>).</param>
    /// <param name="compilation">The compilation for owned-type symbol resolution.</param>
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
    /// Resolves the effective table of every captured owned type that declared no <c>ToTable</c>. Runs after
    /// all configuration passes so a chained <c>ToTable</c> is already applied and is not overwritten:
    /// <c>OwnsOne</c> shares the owner's table (EF table-splitting); <c>OwnsMany</c> gets EF's default
    /// <c>{OwnerTable}_{Nav}</c>, which never equals the owner's, so it always renders as its own box.
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
}
