using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using ProjGraph.Lib.EntityFramework.Infrastructure.Extensions;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Walks the Fluent API invocation chains of a configuring method (OnModelCreating or a snapshot's
/// BuildModel) directly on the C# syntax tree to discover entity relationships, having replaced the
/// retired text/regex relationship parser. The receiver expression of each chain determines the owning
/// entity, so configuration never leaks between unrelated statements.
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
        var existingKeys = model.Relationships.Select(r => r.GenerateKey()).ToHashSet();

        foreach (var hasInvocation in FindRelationshipRoots(method))
        {
            var chain = new FluentChain(hasInvocation);

            var sourceEntity = ResolveSourceEntity(chain, ambientEntity);
            if (sourceEntity is null)
            {
                continue;
            }

            var relationship = BuildRelationship(chain, sourceEntity, entities, compilation);
            if (relationship is null)
            {
                continue;
            }

            var hasMethod = chain.Calls
                .First(c => c.Name is EfAnalysisConstants.EfMethods.HasOne or EfAnalysisConstants.EfMethods.HasMany)
                .Name;
            ApplyForeignKey(chain, hasMethod, sourceEntity, relationship.TargetEntity, entities);

            if (existingKeys.Add(relationship.GenerateKey()))
            {
                model.Relationships.Add(relationship);
            }
        }
    }

    /// <summary>Finds every <c>HasOne</c>/<c>HasMany</c> invocation, excluding those inside a <c>UsingEntity(...)</c> call.</summary>
    /// <param name="method">The method to scan.</param>
    private static IEnumerable<InvocationExpressionSyntax> FindRelationshipRoots(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma
                          && FluentSyntax.SimpleName(ma.Name) is EfAnalysisConstants.EfMethods.HasOne
                              or EfAnalysisConstants.EfMethods.HasMany
                          && !IsInsideUsingEntity(inv));
    }

    /// <summary>
    /// Determines whether a node is lexically inside the argument list of a <c>UsingEntity(...)</c> invocation.
    /// Checked against <see cref="InvocationExpressionSyntax.ArgumentList"/> specifically (not the whole
    /// invocation), since a <c>UsingEntity</c> call is itself chained onto the very <c>HasMany</c>/<c>HasOne</c>
    /// invocation that seeds the outer relationship (e.g. <c>.HasMany(...).WithMany(...).UsingEntity(...)</c>),
    /// which would otherwise make that outer call falsely match as an ancestor of itself.
    /// </summary>
    /// <param name="node">The node to test.</param>
    private static bool IsInsideUsingEntity(SyntaxNode node)
    {
        return node.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma
                        && FluentSyntax.SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.UsingEntity
                        && inv.ArgumentList.Span.Contains(node.Span));
    }

    /// <summary>Resolves the entity that owns a fluent chain from its receiver expression.</summary>
    /// <param name="chain">The fluent chain.</param>
    /// <param name="ambientEntity">The owning entity to fall back to when no enclosing <c>Entity&lt;T&gt;()</c> is found.</param>
    private static string? ResolveSourceEntity(FluentChain chain, string? ambientEntity)
    {
        // modelBuilder.Entity<T>().HasMany(...): the Entity call is part of this chain's spine.
        foreach (var (name, invocation) in chain.Calls)
        {
            if (name == EfAnalysisConstants.EfMethods.Entity)
            {
                return FluentSyntax.EntityNameFromInvocation(invocation);
            }
        }

        // Entity<T>(e => e.HasMany(...)): the Has call is inside the Entity configuration lambda.
        //
        // CAUTION: this ancestor walk is unbounded — it climbs past owned-type builder fences without
        // stopping, unlike FluentSyntax.ResolveOwningEntity. FindRelationshipRoots fences only
        // UsingEntity, so a HasOne/HasMany declared INSIDE an OwnsOne/OwnsMany builder lambda IS
        // yielded, reaches this walk, and gets attributed to the Entity<T>() enclosing the builder —
        // the owner, not the owned type. That coarse attribution is a known limitation (this walker
        // has no way to produce an owned {Owner}.{Nav} source key). If owned-builder relationships
        // are ever modelled properly, this walk must stop at nested-builder fences the way
        // FluentSyntax.ResolveOwningEntity does, or the relationship will silently leak to the owner.
        var enclosingEntity = chain.HasNode.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression is MemberAccessExpressionSyntax ma
                                   && FluentSyntax.SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.Entity);

        return enclosingEntity is null ? ambientEntity : FluentSyntax.EntityNameFromInvocation(enclosingEntity);
    }

    /// <summary>Builds the relationship for a chain, or <see langword="null"/> when it has no paired <c>With</c> call.</summary>
    /// <param name="chain">The fluent chain.</param>
    /// <param name="sourceEntity">The owning entity name.</param>
    /// <param name="entities">The known entities.</param>
    /// <param name="compilation">The compilation for semantic resolution.</param>
    private static EfRelationship? BuildRelationship(
        FluentChain chain,
        string sourceEntity,
        Dictionary<string, EfEntity> entities,
        Compilation compilation)
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

        var explicitRequired = ExtractExplicitRequired(chain);
        return CreateShadowRelationship(sourceEntity, target, hasMethod, withMethod, explicitRequired);
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
        var generic = FluentSyntax.GenericTypeArgumentName(hasInvocation);
        if (generic is not null)
        {
            return generic;
        }

        var arg = hasInvocation.ArgumentList.Arguments.FirstOrDefault();
        return arg?.Expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression)
                => FluentSyntax.LastSegment(literal.Token.ValueText),
            SimpleLambdaExpressionSyntax lambda => NavigationTargetName(lambda, sourceEntity, entities, compilation),
            _ => null
        };
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
               && targetType is not null
               && entities.ContainsKey(targetType.Name)
            ? targetType.Name
            : navigationPropertyName;
    }

    /// <summary>Returns the navigation property name from a lambda like <c>x =&gt; x.Nav</c>, else <see langword="null"/>.</summary>
    /// <param name="lambda">The lambda expression.</param>
    private static string? NavigationName(SimpleLambdaExpressionSyntax lambda)
    {
        return (lambda.Body as MemberAccessExpressionSyntax)?.Name.Identifier.Text;
    }

    /// <summary>Reads an explicit <c>.IsRequired(...)</c> from the chain: <c>true</c> for no-arg or <c>true</c>, <c>false</c> otherwise; <see langword="null"/> when absent.</summary>
    /// <param name="chain">The fluent chain.</param>
    private static bool? ExtractExplicitRequired(FluentChain chain)
    {
        var (name, invocation) = chain.Calls.FirstOrDefault(c => c.Name == EfAnalysisConstants.EfMethods.IsRequired);
        if (name is null)
        {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        return arguments.Count == 0 || arguments[0].Expression.IsKind(SyntaxKind.TrueLiteralExpression);
    }

    /// <summary>Marks the foreign-key properties declared by a <c>HasForeignKey(...)</c> call on the dependent entity.</summary>
    /// <param name="chain">The fluent chain.</param>
    /// <param name="hasMethod">The Has method name (selects the default dependent entity).</param>
    /// <param name="sourceEntity">The owning entity name.</param>
    /// <param name="targetEntity">The relationship target entity name.</param>
    /// <param name="entities">The known entities.</param>
    private static void ApplyForeignKey(
        FluentChain chain,
        string hasMethod,
        string sourceEntity,
        string targetEntity,
        Dictionary<string, EfEntity> entities)
    {
        var (name, invocation) = chain.Calls.FirstOrDefault(c => c.Name == EfAnalysisConstants.EfMethods.HasForeignKey);
        if (name is null)
        {
            return;
        }

        var propertyNames = ForeignKeyPropertyNames(invocation, entities.Keys);
        if (propertyNames.Count == 0)
        {
            return;
        }

        var dependentEntity = FluentSyntax.GenericTypeArgumentName(invocation)
                              ?? (hasMethod == EfAnalysisConstants.EfMethods.HasOne ? sourceEntity : targetEntity);

        if (entities.TryGetValue(dependentEntity, out var entity))
        {
            MarkForeignKeys(entity, propertyNames);
        }
    }

    /// <summary>Extracts the property names from a <c>HasForeignKey</c> call (lambda member access, anonymous object, or string literals).</summary>
    /// <param name="invocation">The HasForeignKey invocation.</param>
    /// <param name="knownEntities">The known entity names, used to recognize the dependent-type-name argument of the two-string overload.</param>
    private static List<string> ForeignKeyPropertyNames(InvocationExpressionSyntax invocation, IReadOnlyCollection<string> knownEntities)
    {
        var lambdaNames = new List<string>();
        var literalNames = new List<string>();
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            switch (argument.Expression)
            {
                case SimpleLambdaExpressionSyntax lambda:
                    lambdaNames.AddRange(lambda.Body.DescendantNodesAndSelf()
                        .OfType<MemberAccessExpressionSyntax>()
                        .Select(m => m.Name.Identifier.Text));
                    break;
                case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                    literalNames.Add(literal.Token.ValueText);
                    break;
            }
        }

        // The ModelSnapshot one-to-one form emits HasForeignKey("Ns.Dependent", "FkId"): the first
        // string argument is the dependent entity *type name*, not an FK property. Drop it so it is
        // not fabricated into a phantom column. The single-string and composite (all-column) forms
        // never lead with an entity name, so they are preserved.
        if (literalNames.Count >= 2 && IsEntityTypeName(literalNames[0], knownEntities))
        {
            literalNames.RemoveAt(0);
        }

        lambdaNames.AddRange(literalNames);
        return lambdaNames;
    }

    /// <summary>Determines whether a <c>HasForeignKey</c> string argument names an entity type (dotted or a known entity) rather than a property.</summary>
    /// <param name="value">The string-literal argument.</param>
    /// <param name="knownEntities">The known entity names.</param>
    private static bool IsEntityTypeName(string value, IReadOnlyCollection<string> knownEntities)
        => value.Contains('.', StringComparison.Ordinal) || knownEntities.Contains(FluentSyntax.LastSegment(value));

    /// <summary>Sets <see cref="EfProperty.IsForeignKey"/> on the named properties, creating them if missing (parity with the regex parser).</summary>
    /// <param name="entity">The dependent entity.</param>
    /// <param name="propertyNames">The FK property names.</param>
    private static void MarkForeignKeys(EfEntity entity, List<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = EfPropertyFactory.GetOrCreateProperty(entity, propertyName, "");
            var updated = EfPropertyFactory.CopyWith(property, new EfPropertyOverrides { IsForeignKey = true });
            var index = entity.Properties.IndexOf(property);
            if (index >= 0)
            {
                entity.Properties[index] = updated;
            }
        }
    }

    /// <summary>
    /// Creates an EfRelationship from has/with method combination.
    /// </summary>
    /// <param name="sourceEntity">The source entity name.</param>
    /// <param name="targetEntity">The target entity name.</param>
    /// <param name="hasMethod">The Has method name (HasOne/HasMany).</param>
    /// <param name="withMethod">The With method name (WithOne/WithMany).</param>
    /// <param name="explicitRequired">
    /// The explicit <c>.IsRequired(...)</c> value when configured, or <see langword="null"/> to apply the
    /// EF convention default for the relationship kind (required for one-to-many, optional otherwise).
    /// </param>
    private static EfRelationship CreateShadowRelationship(string sourceEntity, string targetEntity,
        string hasMethod, string withMethod, bool? explicitRequired = null)
    {
        return (hasMethod, withMethod) switch
        {
            (EfAnalysisConstants.EfMethods.HasOne, EfAnalysisConstants.EfMethods.WithMany) => new EfRelationship
            {
                SourceEntity = targetEntity,
                TargetEntity = sourceEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = explicitRequired ?? true
            },
            (EfAnalysisConstants.EfMethods.HasMany, EfAnalysisConstants.EfMethods.WithOne) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = explicitRequired ?? true
            },
            (EfAnalysisConstants.EfMethods.HasOne, EfAnalysisConstants.EfMethods.WithOne) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.OneToOne,
                IsRequired = explicitRequired ?? false
            },
            (EfAnalysisConstants.EfMethods.HasMany, EfAnalysisConstants.EfMethods.WithMany) => new EfRelationship
            {
                SourceEntity = sourceEntity,
                TargetEntity = targetEntity,
                Type = EfRelationshipType.ManyToMany,
                IsRequired = explicitRequired ?? false
            },
            _ => new EfRelationship
            {
                SourceEntity = targetEntity,
                TargetEntity = sourceEntity,
                Type = EfRelationshipType.OneToMany,
                IsRequired = explicitRequired ?? true
            }
        };
    }

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
