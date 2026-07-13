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
    /// <param name="entities">The known entities (reserved for a future task's semantic resolution).</param>
#pragma warning disable RCS1163, S1172 // entities is reserved for a later task's semantic resolution
    private static string? ResolveSourceEntity(FluentChain chain, Dictionary<string, EfEntity> entities)
#pragma warning restore RCS1163, S1172
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
    /// <param name="semanticModel">The semantic model for the method's tree (reserved for a future task's semantic resolution).</param>
#pragma warning disable RCS1163, S1172 // semanticModel is reserved for a later task's semantic resolution
    private static EfRelationship? BuildRelationship(
        FluentChain chain,
        string sourceEntity,
        Dictionary<string, EfEntity> entities,
        Compilation compilation,
        SemanticModel semanticModel)
#pragma warning restore RCS1163, S1172
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
    /// <param name="sourceEntity">The owning entity, used to resolve a navigation-lambda argument's target type.</param>
    /// <param name="entities">The known entities.</param>
    /// <param name="compilation">The compilation, used to re-resolve <paramref name="sourceEntity"/>'s type symbol.</param>
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
                return NavigationTargetName(lambda, sourceEntity, entities, compilation);
            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves the entity type of a navigation lambda like <c>x =&gt; x.Nav</c> by looking up the property
    /// on <paramref name="sourceEntity"/>'s Roslyn symbol (bypassing the lambda's own symbol info, which is
    /// unresolved because the fluent chain is typically invoked through a <c>dynamic</c> modelBuilder in
    /// these fixtures). Falls back to the raw navigation property name when the symbol cannot be resolved.
    /// </summary>
    /// <param name="lambda">The lambda expression identifying the navigation property.</param>
    /// <param name="sourceEntity">The owning entity name whose declared members are searched.</param>
    /// <param name="entities">The known entities, used to look up <paramref name="sourceEntity"/>'s <see cref="EfEntity"/>.</param>
    /// <param name="compilation">The compilation used to re-resolve <paramref name="sourceEntity"/>'s type symbol.</param>
    private static string? NavigationTargetName(
        SimpleLambdaExpressionSyntax lambda,
        string sourceEntity,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
    {
        var navigationPropertyName = NavigationName(lambda);
        if (navigationPropertyName is null || !entities.TryGetValue(sourceEntity, out var entity))
        {
            return navigationPropertyName;
        }

        var sourceSymbol = EntityAnalyzer.FindEntitySymbol(entity, compilation);
        var navigationProperty = sourceSymbol?.GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(p => p.Name == navigationPropertyName);

        if (navigationProperty is null)
        {
            return navigationPropertyName;
        }

        return NavigationPropertyAnalyzer.IsNavigationProperty(navigationProperty, out var targetType, out _)
            ? targetType?.Name ?? navigationPropertyName
            : navigationPropertyName;
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
