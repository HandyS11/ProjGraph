using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using System.Collections.Frozen;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Shared syntax-walking primitives for the Fluent API walkers (<see cref="FluentEntityWalker"/>,
/// <see cref="FluentPropertyWalker"/>, <see cref="FluentRelationshipWalker"/> and
/// <see cref="EntityConfigurationWalker"/>). Centralises the receiver-chain traversal, owning-entity
/// resolution and name-extraction helpers that every walker needs, so the scoping rules (a chain's
/// receiver determines its owning entity, and configuration never leaks into a nested owned-type /
/// join-entity builder lambda) live in exactly one place.
/// </summary>
internal static class FluentSyntax
{
    /// <summary>
    /// Fluent methods that open a nested builder lambda for a *different* target (an owned type or a join
    /// entity). Configuration calls inside their argument lists configure that nested builder, not the
    /// outer entity, and are excluded by <see cref="FindConfigRoots"/> / <see cref="ResolveOwningEntity"/>.
    /// Immutable and private so no other code can alter the walkers' scoping behaviour at runtime.
    /// </summary>
    private static readonly FrozenSet<string> NestedBuilderScopes = new[]
    {
        EfAnalysisConstants.EfMethods.OwnsOne,
        EfAnalysisConstants.EfMethods.OwnsMany,
        EfAnalysisConstants.EfMethods.UsingEntity
    }.ToFrozenSet(StringComparer.Ordinal);

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
            .Any(inv => IsNestedBuilderFence(inv, node));
    }

    /// <summary>
    /// Determines whether <paramref name="invocation"/> fences <paramref name="node"/> off from the outer
    /// entity: an owned-type / join-entity builder call (<see cref="NestedBuilderScopes"/>) whose ARGUMENT
    /// LIST contains the node. The argument list — not the whole invocation — is tested because such a call
    /// is itself chained onto the entity being configured, so a node on its receiver spine is not fenced by
    /// it. The single home of the boundary rule shared by <see cref="IsInsideNestedBuilderScope"/> and
    /// <see cref="ResolveOwningEntity"/>'s ancestor search.
    /// </summary>
    /// <param name="invocation">The candidate fence invocation.</param>
    /// <param name="node">The node being tested.</param>
    private static bool IsNestedBuilderFence(InvocationExpressionSyntax invocation, SyntaxNode node)
    {
        return invocation.Expression is MemberAccessExpressionSyntax ma
               && NestedBuilderScopes.Contains(SimpleName(ma.Name))
               && invocation.ArgumentList.Span.Contains(node.Span);
    }

    /// <summary>
    /// Resolves the entity that owns a configuration call, either from an <c>Entity&lt;T&gt;()</c> earlier in
    /// the same chain (<c>modelBuilder.Entity&lt;T&gt;().Property(...)</c>) or from the enclosing
    /// <c>Entity&lt;T&gt;(e =&gt; ...)</c> configuration lambda. When the receiver chain crosses an
    /// <c>OwnsOne</c>/<c>OwnsMany</c> call, the result is the owned type's <c>{Owner}.{Nav}</c> key instead —
    /// so a chained <c>Property</c>/<c>ToTable</c> configures the owned type, never the outer entity the
    /// chain would otherwise reach. Crossing a <c>UsingEntity</c> (join-entity) call stops instead (returning
    /// <see langword="null"/> from the receiver-chain search, or falling back to <paramref name="ambientEntity"/>
    /// from the ancestor search), since join entities are out of scope — including when
    /// <paramref name="configInvocation"/> itself lives inside such a builder's own argument list (the case a
    /// caller who scopes directly to that argument list, passing its own key as <paramref name="ambientEntity"/>,
    /// relies on).
    /// </summary>
    /// <param name="configInvocation">The configuration invocation (e.g. <c>Property</c>/<c>ToTable</c>).</param>
    /// <param name="ambientEntity">The owning entity to fall back to when no enclosing <c>Entity&lt;T&gt;()</c> is found.</param>
    public static string? ResolveOwningEntity(InvocationExpressionSyntax configInvocation, string? ambientEntity)
    {
        for (var receiver = ChainReceiver(configInvocation);
             receiver is not null;
             receiver = ChainReceiver(receiver))
        {
            if (receiver.Expression is not MemberAccessExpressionSyntax ma)
            {
                continue;
            }

            var callName = SimpleName(ma.Name);

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

            if (callName == EfAnalysisConstants.EfMethods.Entity)
            {
                return EntityNameFromInvocation(receiver);
            }
        }

        InvocationExpressionSyntax? enclosingEntity = null;
        foreach (var ancestor in configInvocation.Ancestors().OfType<InvocationExpressionSyntax>())
        {
            // Climbing past an owned-type / join-entity builder fence (e.g. the OwnsOne call whose
            // argument list configInvocation lives inside) would reattribute this call to whatever
            // Entity<T>() lies beyond it. Stop instead, so ambientEntity — the owned/join target the
            // caller scoped this search to — wins.
            if (IsNestedBuilderFence(ancestor, configInvocation))
            {
                break;
            }

            if (ancestor.Expression is MemberAccessExpressionSyntax ma
                && SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.Entity)
            {
                enclosingEntity = ancestor;
                break;
            }
        }

        return enclosingEntity is null ? ambientEntity : EntityNameFromInvocation(enclosingEntity);
    }

    /// <summary>Returns the invocation on the receiver side of a member-access invocation, or <see langword="null"/>.</summary>
    /// <param name="invocation">The invocation whose receiver to inspect.</param>
    public static InvocationExpressionSyntax? ChainReceiver(InvocationExpressionSyntax invocation)
    {
        return (invocation.Expression as MemberAccessExpressionSyntax)?.Expression as InvocationExpressionSyntax;
    }

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

    /// <summary>Returns the entity name from an <c>Entity&lt;T&gt;()</c> or <c>Entity("NS.T")</c> invocation.</summary>
    /// <param name="invocation">The Entity invocation.</param>
    public static string? EntityNameFromInvocation(InvocationExpressionSyntax invocation)
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
    public static string? GenericTypeArgumentName(InvocationExpressionSyntax invocation)
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
    public static string SimpleName(SimpleNameSyntax name) => name.Identifier.Text;

    /// <summary>
    /// Returns the name of a type syntax: the bare identifier for a simple name, otherwise the last dotted
    /// segment of its text (namespace qualification stripped; any generic argument list is retained).
    /// </summary>
    /// <param name="type">The type syntax.</param>
    public static string TypeName(TypeSyntax type)
    {
        return type is IdentifierNameSyntax identifier
            ? identifier.Identifier.Text
            : LastSegment(type.ToString());
    }

    /// <summary>Returns the substring after the last <c>.</c>, or the whole string when there is none.</summary>
    /// <param name="value">The dotted name.</param>
    public static string LastSegment(string value)
        => value.Contains('.', StringComparison.Ordinal) ? value.Split('.')[^1] : value;

    /// <summary>
    /// Materializes the entity named <paramref name="entityName"/> when it is not already known, resolving its
    /// symbol (or falling back to a bare entity) and adding it to both the entities dictionary and the model.
    /// Mirrors the regex parser's materialization; used by <see cref="FluentEntityWalker"/> and
    /// <see cref="EntityConfigurationWalker"/>.
    /// </summary>
    /// <param name="entityName">The configured entity type name (namespace-stripped).</param>
    /// <param name="entities">The known entities, augmented in place.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is augmented.</param>
    /// <param name="compilation">The compilation for symbol resolution.</param>
    public static void MaterializeEntity(
        string? entityName,
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
}
