using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Walks the Fluent API invocation chains of a configuring method (OnModelCreating or a snapshot's
/// BuildModel) directly on the C# syntax tree to discover entity-level configuration: fluent-only
/// entities declared via <c>modelBuilder.Entity&lt;T&gt;()</c> / <c>Entity("Ns.T")</c> that have no
/// <c>DbSet&lt;T&gt;</c>, and their <c>.ToTable("X")</c> mapping, having replaced the retired text/regex
/// materialization and <c>ToTable</c> parsing. The receiver expression of each chain determines the
/// owning entity (see <see cref="FluentSyntax.ResolveOwningEntity"/>), so configuration never leaks
/// between unrelated statements or into nested owned-type / join-entity builder lambdas.
/// </summary>
internal static class FluentEntityWalker
{
    /// <summary>
    /// Materializes fluent-only entities (Task 1) and applies <c>ToTable</c> mappings (Task 2) found in
    /// <paramref name="scope"/>.
    /// </summary>
    /// <param name="scope">The <c>OnModelCreating</c> method declaration (or an owned builder's argument list) to walk.</param>
    /// <param name="entities">Entities already discovered from DbSets; augmented in place with fluent-only entities.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is populated.</param>
    /// <param name="compilation">The Roslyn compilation for symbol resolution of fluent-only entity types.</param>
    /// <param name="ambientEntity">
    /// The owning entity to fall back to when a chain has no <c>Entity&lt;T&gt;()</c> call to resolve from
    /// (e.g. an <c>IEntityTypeConfiguration&lt;T&gt;.Configure</c> body rooted at a bare builder parameter).
    /// </param>
    public static void Apply(
        SyntaxNode scope,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        string? ambientEntity = null)
    {
        foreach (var entityInvocation in FluentSyntax.FindConfigRoots(scope, EfAnalysisConstants.EfMethods.Entity))
        {
            FluentSyntax.MaterializeEntity(
                FluentSyntax.EntityNameFromInvocation(entityInvocation), entities, model, compilation);
        }

        foreach (var toTableInvocation in FluentSyntax.FindConfigRoots(scope, EfAnalysisConstants.EfMethods.ToTable))
        {
            ApplyTableName(toTableInvocation, entities, model, ambientEntity);
        }
    }

    /// <summary>
    /// Collects the entity type names referenced by every <c>Entity&lt;T&gt;()</c> / <c>Entity("Ns.T")</c>
    /// invocation in <paramref name="method"/>, namespace-stripped. Unlike <see cref="Apply"/>, nested
    /// builder scopes are NOT excluded: this feeds entity-*file* discovery, which wants maximal recall,
    /// while configuration scoping stays the walkers' concern.
    /// </summary>
    /// <param name="method">The configuring method (e.g. a snapshot's <c>BuildModel</c>) to scan.</param>
    public static HashSet<string> CollectEntityNames(MethodDeclarationSyntax method)
    {
        var names = new HashSet<string>();
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax ma
                && FluentSyntax.SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.Entity
                && FluentSyntax.EntityNameFromInvocation(invocation) is { Length: > 0 } name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// Applies a <c>.ToTable("X")</c> call to its owning entity, replacing the entity instance with a copy
    /// carrying the table name in both the entities dictionary and the model. Mirrors the regex parser's
    /// table-mapping step; the table name is the call's first string-literal argument, which also covers the
    /// <c>.ToTable("X", "schema")</c> overload (Low #13).
    /// </summary>
    /// <param name="toTableInvocation">The <c>ToTable</c> invocation.</param>
    /// <param name="entities">The known entities.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is updated.</param>
    /// <param name="ambientEntity">The owning entity to fall back to when the chain has no <c>Entity&lt;T&gt;()</c> call.</param>
    private static void ApplyTableName(
        InvocationExpressionSyntax toTableInvocation,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        string? ambientEntity)
    {
        var tableName = TableNameArgument(toTableInvocation);
        if (tableName is null)
        {
            return;
        }

        var entityName = FluentSyntax.ResolveOwningEntity(toTableInvocation, ambientEntity);
        if (entityName is null || !entities.TryGetValue(entityName, out var entity))
        {
            return;
        }

        var updated = EfEntityFactory.CopyWith(entity, tableName);

        entities[entityName] = updated;

        // Find the slot by EffectiveKey, not by reference: reference equality would silently stop
        // updating the model list the moment the dictionary and model.Entities hold different object
        // instances for the same logical entity (these are init-only records that walkers replace
        // wholesale, so nothing guarantees they stay the same instance forever). EffectiveKey — not
        // Name, which is the CLR type name and is not unique across owned entities (e.g. two
        // navigations of the same owned type) — is the model's actual identity.
        var index = -1;
        for (var i = 0; i < model.Entities.Count; i++)
        {
            if (model.Entities[i].EffectiveKey == updated.EffectiveKey)
            {
                index = i;
                break;
            }
        }

        if (index >= 0)
        {
            model.Entities[index] = updated;
        }
    }

    /// <summary>Returns the first string-literal argument of a <c>ToTable</c> call (the table name), else <see langword="null"/>.</summary>
    /// <param name="invocation">The ToTable invocation.</param>
    private static string? TableNameArgument(InvocationExpressionSyntax invocation)
    {
        var arg = invocation.ArgumentList.Arguments.FirstOrDefault();
        return arg?.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;
    }
}
