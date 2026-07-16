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
        // Combine OwnsOne and OwnsMany roots and process them owner-first. FindConfigRoots walks
        // DescendantNodes(), a pre-order traversal: for a chained nested form like
        // Entity<T>().OwnsOne(a).OwnsOne(b), the syntactically OUTERMOST node is the .OwnsOne(b) call —
        // .OwnsOne(a) is nested inside it as its receiver — so pre-order yields b before a. Capturing b
        // first would resolve its owner key to a's {Owner}.{Nav}, which doesn't exist in the dictionary
        // yet, and Capture would silently drop b and everything chained onto it.
        //
        // A receiver-chain-inner call always has a strictly smaller Span.End than the call chained onto
        // it (its own tokens all precede the closing paren of the outer call), so ordering by Span.End
        // ascending processes inner-before-outer, i.e. owner-before-owned, for both same-type and
        // mixed OwnsOne/OwnsMany nesting. For sibling calls (two independent statements, or a builder
        // lambda's single call) Span.End ascending matches source order, so the already-working forms
        // (builder-lambda, two-statement chained merge, nested-in-lambda) are unaffected.
        var ownsOneRoots = FluentSyntax.FindConfigRoots(scope, EfAnalysisConstants.EfMethods.OwnsOne)
            .Select(inv => (Invocation: inv, IsCollection: false));
        var ownsManyRoots = FluentSyntax.FindConfigRoots(scope, EfAnalysisConstants.EfMethods.OwnsMany)
            .Select(inv => (Invocation: inv, IsCollection: true));

        var orderedRoots = ownsOneRoots.Concat(ownsManyRoots).OrderBy(t => t.Invocation.Span.End);

        foreach (var (owns, isCollection) in orderedRoots)
        {
            Capture(owns, entities, model, compilation, ambientEntity, isCollection);
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

        // WithOwner().HasForeignKey("X") is the owned-type-specific relationship declaration a generated
        // snapshot always emits; FluentRelationshipWalker only recognises HasOne/HasMany chains, so it
        // never sees this one. StripShadowKeys depends on IsForeignKey to tell a table-split owned type's
        // FK column (dropped) from a real one (kept), so it must be marked here instead.
        ApplyOwnedForeignKey(owns.ArgumentList, entities, key);

        // Recurse so an OwnsOne/OwnsMany nested inside this owned builder (owned-within-owned) is
        // captured with this owned entity as ambient. FindConfigRoots is scope-relative (Task 1), so
        // the nested call is found relative to owns.ArgumentList while any fence deeper still excludes
        // its children — the recursive call handles those in turn.
        Apply(owns.ArgumentList, entities, model, compilation, key);
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

        // Name falls back to the snapshot's type-name literal (e.g. "ReceiptAddress" from
        // "Fixtures.ReceiptAddress"), not the navigation, when the type is named but its symbol could not
        // be resolved (a snapshot fixture with no backing CLR class, as `dotnet ef` output always is here).
        // Mirrors FluentSyntax.MaterializeEntity's root-entity fallback, which likewise names the entity
        // from the literal rather than leaving it keyed only by the caller's context.
        var owned = new EfEntity
        {
            Name = ownedType?.Name ?? SnapshotOwnedTypeName(owns) ?? navigation,
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
    /// Marks the foreign-key properties declared by a <c>WithOwner().HasForeignKey(...)</c> call found
    /// directly within an owned builder's own scope (nested owned builders are excluded, mirroring
    /// <see cref="FluentSyntax.FindConfigRoots"/>'s general fencing). Creates the property if the
    /// preceding <see cref="FluentPropertyWalker"/> pass has not already added it.
    /// </summary>
    /// <param name="ownedScope">The owned builder's own argument list.</param>
    /// <param name="entities">The known entities.</param>
    /// <param name="key">The owned entity's dictionary key (<c>{Owner}.{Nav}</c>).</param>
    private static void ApplyOwnedForeignKey(SyntaxNode ownedScope, Dictionary<string, EfEntity> entities, string key)
    {
        if (!entities.TryGetValue(key, out var owned))
        {
            return;
        }

        foreach (var invocation in FluentSyntax.FindConfigRoots(ownedScope, EfAnalysisConstants.EfMethods.HasForeignKey))
        {
            if (FluentSyntax.ChainReceiver(invocation) is not { Expression: MemberAccessExpressionSyntax withOwnerAccess }
                || FluentSyntax.SimpleName(withOwnerAccess.Name) != EfAnalysisConstants.EfMethods.WithOwner)
            {
                continue;
            }

            foreach (var propertyName in ForeignKeyPropertyNames(invocation))
            {
                var property = EfPropertyFactory.GetOrCreateProperty(owned, propertyName, "");
                var updated = EfPropertyFactory.CopyWith(property, new EfPropertyOverrides { IsForeignKey = true });
                var index = owned.Properties.IndexOf(property);
                if (index >= 0)
                {
                    owned.Properties[index] = updated;
                }
            }
        }
    }

    /// <summary>Extracts the property names from a <c>HasForeignKey</c> call (lambda member access or string literals).</summary>
    /// <param name="invocation">The <c>HasForeignKey</c> invocation.</param>
    private static IEnumerable<string> ForeignKeyPropertyNames(InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            switch (argument.Expression)
            {
                case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                    yield return literal.Token.ValueText;
                    break;
                case SimpleLambdaExpressionSyntax lambda:
                    foreach (var member in lambda.Body.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
                    {
                        yield return member.Name.Identifier.Text;
                    }

                    break;
            }
        }
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
        // Snapshot form: the FIRST ARGUMENT itself (not merely the first string literal anywhere in the
        // argument list) is the owned type's (namespace-qualified) name, e.g. OwnsOne("Ns.Type", "nav",
        // b1 => ...). Checking position, not just presence, matters because a lambda-navigation call can
        // carry a string literal in a later argument (e.g. an explicit table/entity-type-name overload)
        // that is not a type name at all; the DbContext path's first argument is always a lambda, never a
        // string literal, so this check alone distinguishes the two forms without misreading that later
        // literal as the owned type and silently degrading to an unseeded bare entity.
        var snapshotTypeName = SnapshotOwnedTypeName(owns);
        if (snapshotTypeName is not null)
        {
            return compilation.GetSymbolsWithName(snapshotTypeName, SymbolFilter.Type)
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

        // An IErrorTypeSymbol still satisfies `is INamedTypeSymbol` — Roslyn synthesizes one whenever the
        // navigation's CLR type could not be resolved (e.g. its file never made it into the compilation).
        // Treating it as resolved would seed the owned entity from a symbol with no members, capturing it
        // with zero properties instead of surfacing the failure — the exact silent-data-loss shape this
        // guards against; return null instead so the caller's graceful bare-entity fallback applies.
        if (navProperty?.Type is not INamedTypeSymbol navType || navType is IErrorTypeSymbol)
        {
            return null;
        }

        if (!isCollection)
        {
            return navType;
        }

        // Same IErrorTypeSymbol guard for the unwrapped element type (OwnsMany's List&lt;T&gt;-style
        // navigation): an unresolvable element must not masquerade as resolved either. When there is no
        // generic element at all (not a realistic shape, but not this method's concern), fall back to the
        // collection type itself, exactly as before this guard was added.
        return navType.TypeArguments.FirstOrDefault() switch
        {
            IErrorTypeSymbol => null,
            INamedTypeSymbol element => element,
            _ => navType
        };
    }

    /// <summary>
    /// Returns the owned type's namespace-qualified name literal when <paramref name="owns"/> uses the
    /// snapshot's string-literal form (its first argument is itself a string literal, e.g.
    /// <c>OwnsOne("Ns.Type", "nav", ...)</c>), namespace-stripped; else <see langword="null"/>. The
    /// DbContext lambda form's first argument is always a lambda, never a string literal, so this also
    /// serves as that form's discriminator.
    /// </summary>
    /// <param name="owns">The <c>OwnsOne</c>/<c>OwnsMany</c> invocation.</param>
    private static string? SnapshotOwnedTypeName(InvocationExpressionSyntax owns)
    {
        return owns.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
               && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? FluentSyntax.LastSegment(literal.Token.ValueText)
            : null;
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

            var ownerTable = EffectiveTable(owner);
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

    /// <summary>
    /// Normalises the EF implementation details a snapshot's owned block declares. Two rules, BOTH scoped to
    /// a table-split owned type only (<paramref name="entities"/> whose effective table equals its owner's —
    /// the case the renderer inlines onto the owner):
    /// <list type="bullet">
    /// <item>An inlined owned type's key is a shadow property EF invents to make the owned row addressable.
    /// It is not part of the modelled schema, and surfacing it would put a spurious PK on the owner once the
    /// owned type is inlined — so PK markers are cleared. An owned type on its OWN table (<c>OwnsMany</c>,
    /// <c>OwnsOne</c>+<c>ToTable</c>) is never inlined and draws its own box, where that same key IS its
    /// real, addressable primary key — so it is kept, exactly as the spec's inlined-only PK-suppression
    /// scope requires.</item>
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

                stripped.Properties.Add(sharesOwnerTable && property.IsPrimaryKey
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
}
