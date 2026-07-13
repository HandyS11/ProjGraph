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
    private static readonly HashSet<string> NestedBuilderScopes = new(StringComparer.Ordinal)
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
            SimpleLambdaExpressionSyntax lambda => LambdaMemberName(lambda.Body),
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } lambda
                => LambdaMemberName(lambda.Body),
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression)
                => literal.Token.ValueText,
            _ => null
        };
    }

    /// <summary>Returns the member name of a lambda body of the form <c>x =&gt; x.Prop</c>, else <see langword="null"/>.</summary>
    /// <param name="body">The lambda body.</param>
    private static string? LambdaMemberName(CSharpSyntaxNode body)
        => (body as MemberAccessExpressionSyntax)?.Name.Identifier.Text;

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
