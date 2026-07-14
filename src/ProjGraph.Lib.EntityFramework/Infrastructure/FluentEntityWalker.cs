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
/// owning entity, so configuration never leaks between unrelated statements or into nested owned-type /
/// join-entity builder lambdas.
/// </summary>
internal static class FluentEntityWalker
{
    /// <summary>
    /// Fluent methods that open a nested builder lambda for a *different* target (an owned type or a join
    /// entity). <c>Entity</c>/<c>ToTable</c> calls inside their argument lists configure that nested builder,
    /// not the outer model, and are ignored — mirroring the scoping in <see cref="FluentPropertyWalker"/>
    /// and <see cref="FluentRelationshipWalker"/>.
    /// </summary>
    private static readonly HashSet<string> NestedBuilderScopes = new(StringComparer.Ordinal)
    {
        EfAnalysisConstants.EfMethods.OwnsOne,
        EfAnalysisConstants.EfMethods.OwnsMany,
        EfAnalysisConstants.EfMethods.UsingEntity
    };

    /// <summary>
    /// Materializes fluent-only entities (Task 1) and applies <c>ToTable</c> mappings (Task 2) found in
    /// <paramref name="method"/>.
    /// </summary>
    /// <param name="method">The <c>OnModelCreating</c> method declaration to walk.</param>
    /// <param name="entities">Entities already discovered from DbSets; augmented in place with fluent-only entities.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is populated.</param>
    /// <param name="compilation">The Roslyn compilation for symbol resolution of fluent-only entity types.</param>
    /// <param name="ambientEntity">
    /// The owning entity to fall back to when a chain has no <c>Entity&lt;T&gt;()</c> call to resolve from
    /// (e.g. an <c>IEntityTypeConfiguration&lt;T&gt;.Configure</c> body rooted at a bare builder parameter).
    /// </param>
    public static void Apply(
        MethodDeclarationSyntax method,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation,
        string? ambientEntity = null)
    {
        foreach (var entityInvocation in FindConfigRoots(method, EfAnalysisConstants.EfMethods.Entity))
        {
            MaterializeEntity(entityInvocation, entities, model, compilation);
        }

        foreach (var toTableInvocation in FindConfigRoots(method, EfAnalysisConstants.EfMethods.ToTable))
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
                && SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.Entity
                && EntityNameFromInvocation(invocation) is { Length: > 0 } name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// Finds every invocation whose immediate member name is <paramref name="methodName"/>, excluding those
    /// nested inside an owned-type / join-entity builder lambda (see <see cref="NestedBuilderScopes"/>).
    /// </summary>
    /// <param name="method">The method to scan.</param>
    /// <param name="methodName">The simple method name to match (e.g. <c>Entity</c> or <c>ToTable</c>).</param>
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
    /// Determines whether a node is lexically inside the argument list of an owned-type / join-entity builder
    /// invocation (<see cref="NestedBuilderScopes"/>). The argument list — not the whole invocation — is tested
    /// because such a call is itself chained onto the entity being configured.
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
    /// Materializes the entity named by an <c>Entity&lt;T&gt;()</c> / <c>Entity("Ns.T")</c> invocation when it
    /// is not already known, resolving its symbol (or falling back to a bare entity) and adding it to both the
    /// entities dictionary and the model. Mirrors the regex parser's materialization.
    /// </summary>
    /// <param name="entityInvocation">The <c>Entity</c> invocation.</param>
    /// <param name="entities">The known entities, augmented in place.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is augmented.</param>
    /// <param name="compilation">The compilation for symbol resolution.</param>
    private static void MaterializeEntity(
        InvocationExpressionSyntax entityInvocation,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        var entityName = EntityNameFromInvocation(entityInvocation);
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

        var entityName = ResolveOwningEntity(toTableInvocation, ambientEntity);
        if (entityName is null || !entities.TryGetValue(entityName, out var entity))
        {
            return;
        }

        var updated = new EfEntity
        {
            Name = entity.Name,
            Properties = entity.Properties,
            IsJoinEntity = entity.IsJoinEntity,
            TableName = tableName
        };

        entities[entityName] = updated;
        var index = model.Entities.IndexOf(model.Entities.FirstOrDefault(e => e.Name == entity.Name)!);
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

    /// <summary>
    /// Resolves the entity that owns a configuration call, either from an <c>Entity&lt;T&gt;()</c> earlier in
    /// the same chain (<c>modelBuilder.Entity&lt;T&gt;().ToTable(...)</c>) or from the enclosing
    /// <c>Entity&lt;T&gt;(e =&gt; ...)</c> configuration lambda.
    /// </summary>
    /// <param name="configInvocation">The <c>ToTable</c> invocation.</param>
    /// <param name="ambientEntity">The owning entity to fall back to when no enclosing <c>Entity&lt;T&gt;()</c> is found.</param>
    private static string? ResolveOwningEntity(InvocationExpressionSyntax configInvocation, string? ambientEntity)
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

        return enclosingEntity is null ? ambientEntity : EntityNameFromInvocation(enclosingEntity);
    }

    /// <summary>Returns the invocation on the receiver side of a member-access invocation, or <see langword="null"/>.</summary>
    /// <param name="invocation">The invocation whose receiver to inspect.</param>
    private static InvocationExpressionSyntax? ChainReceiver(InvocationExpressionSyntax invocation)
    {
        return (invocation.Expression as MemberAccessExpressionSyntax)?.Expression as InvocationExpressionSyntax;
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

    /// <summary>Returns the first generic type argument's simple name for an invocation like <c>Entity&lt;T&gt;()</c>, else <see langword="null"/>.</summary>
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

    /// <summary>
    /// Returns the name of a type syntax: the bare identifier for a simple name, otherwise the last dotted
    /// segment of its text (namespace qualification stripped; any generic argument list is retained).
    /// </summary>
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
