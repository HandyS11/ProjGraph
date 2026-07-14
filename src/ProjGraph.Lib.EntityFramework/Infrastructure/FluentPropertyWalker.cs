using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;
using ProjGraph.Lib.EntityFramework.Infrastructure.Patterns;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Walks the Fluent API invocation chains of a configuring method (OnModelCreating or a snapshot's
/// BuildModel) directly on the C# syntax tree to discover per-property configuration
/// (<c>Property</c>/<c>HasKey</c>/<c>IsRequired</c>/<c>HasMaxLength</c>/<c>HasPrecision</c>/
/// <c>HasColumnType</c>/<c>HasDefaultValue</c>/<c>HasDefaultValueSql</c>), having replaced the retired
/// text/regex property parser. The receiver expression of each chain determines the owning entity, so
/// configuration never leaks between unrelated statements or into nested owned-type / join-entity
/// builder lambdas.
/// </summary>
internal static class FluentPropertyWalker
{
    /// <summary>
    /// Fluent methods that open a nested builder lambda for a *different* target (an owned type or a
    /// join entity). <c>Property</c>/<c>HasKey</c> calls inside their argument lists configure that
    /// nested builder, not the outer entity, and are out of scope for this slice (owned types and join
    /// entities are handled in Slice 3).
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
    /// <param name="ambientEntity">
    /// The owning entity to fall back to when a chain has no <c>Entity&lt;T&gt;()</c> call to resolve from
    /// (e.g. an <c>IEntityTypeConfiguration&lt;T&gt;.Configure</c> body rooted at a bare builder parameter).
    /// </param>
    public static void Apply(
        MethodDeclarationSyntax method,
        Dictionary<string, EfEntity> entities,
        Compilation compilation,
        string? ambientEntity = null)
    {
        foreach (var propertyRoot in FindConfigRoots(method, EfAnalysisConstants.EfMethods.Property))
        {
            ApplyPropertyChain(propertyRoot, entities, compilation, ambientEntity);
        }

        foreach (var keyRoot in FindConfigRoots(method, EfAnalysisConstants.EfMethods.HasKey))
        {
            ApplyKey(keyRoot, entities, ambientEntity);
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
    /// <param name="ambientEntity">The owning entity to fall back to when the chain has no <c>Entity&lt;T&gt;()</c> call.</param>
    private static void ApplyPropertyChain(
        InvocationExpressionSyntax propertyRoot,
        Dictionary<string, EfEntity> entities,
        Compilation compilation,
        string? ambientEntity)
    {
        var entityName = ResolveOwningEntity(propertyRoot, ambientEntity);
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
        var current = EfPropertyFactory.GetOrCreateProperty(entity, propertyName, type);

        foreach (var (name, invocation) in TrailingCalls(propertyRoot))
        {
            var argText = invocation.ArgumentList.Arguments.ToString();
            var updated = ApplyConfiguration(current, name, argText, compilation);
            if (ReferenceEquals(updated, current))
            {
                continue;
            }

            ReplaceProperty(entity, current, updated);
            current = updated;
        }
    }

    /// <summary>
    /// Resolves the owning entity for a <c>HasKey</c> call and marks each named property as a primary key.
    /// </summary>
    /// <param name="keyRoot">The <c>HasKey</c> invocation.</param>
    /// <param name="entities">The known entities.</param>
    /// <param name="ambientEntity">The owning entity to fall back to when the chain has no <c>Entity&lt;T&gt;()</c> call.</param>
    private static void ApplyKey(
        InvocationExpressionSyntax keyRoot,
        Dictionary<string, EfEntity> entities,
        string? ambientEntity)
    {
        var entityName = ResolveOwningEntity(keyRoot, ambientEntity);
        if (entityName is null || !entities.TryGetValue(entityName, out var entity))
        {
            return;
        }

        foreach (var propertyName in KeyPropertyNames(keyRoot))
        {
            var property = EfPropertyFactory.GetOrCreateProperty(entity, propertyName, "");
            var updated = EfPropertyFactory.CopyWith(property, new EfPropertyOverrides { IsPrimaryKey = true });
            ReplaceProperty(entity, property, updated);
        }
    }

    /// <summary>
    /// Extracts primary-key property names from a <c>HasKey</c> argument: a single lambda member access
    /// (<c>a =&gt; a.Id</c>, including the parenthesized-parameter form <c>(a) =&gt; a.Id</c>), an
    /// anonymous-object lambda (<c>a =&gt; new { a.X, a.Y }</c>), or string literals.
    /// </summary>
    /// <param name="invocation">The <c>HasKey</c> invocation.</param>
    private static IEnumerable<string> KeyPropertyNames(InvocationExpressionSyntax invocation)
    {
        foreach (var expression in invocation.ArgumentList.Arguments.Select(argument => argument.Expression))
        {
            if (expression is LambdaExpressionSyntax lambda)
            {
                foreach (var member in lambda.Body.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
                {
                    yield return member.Name.Identifier.Text;
                }

                continue;
            }

            // Non-lambda argument: collect every string literal in its subtree. This matches the regex
            // parser's argument-wide literal scan, so both HasKey("A", "B") and array forms such as
            // HasKey(new[] { "A", "B" }) / HasKey(new string[] { "A", "B" }) yield their key names.
            var stringLiterals = expression.DescendantNodesAndSelf()
                .OfType<LiteralExpressionSyntax>()
                .Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression));
            foreach (var literal in stringLiterals)
            {
                yield return literal.Token.ValueText;
            }
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
    /// <param name="ambientEntity">The owning entity to fall back to when no enclosing <c>Entity&lt;T&gt;()</c> is found.</param>
    private static string? ResolveOwningEntity(InvocationExpressionSyntax configInvocation, string? ambientEntity)
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

            // A chained owned-type builder (e.g. Entity<T>().OwnsOne(...).Property(...)) configures
            // the owned type, not the owner. Stop before crossing it so the config is not leaked
            // onto the outer entity the receiver chain eventually reaches.
            if (NestedBuilderScopes.Contains(callName))
            {
                return null;
            }

            if (callName == EfAnalysisConstants.EfMethods.Entity)
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

    /// <summary>
    /// Returns the name of a type syntax: the bare identifier for a simple name, otherwise the last
    /// dotted segment of its text (namespace qualification stripped; any generic argument list is retained).
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

    /// <summary>
    /// Applies a single property-configuration call to a property, dispatching on the fluent method name;
    /// returns the same instance for unrecognized methods (e.g. generated-snapshot noise such as
    /// <c>ValueGeneratedOnAdd</c> or <c>HasAnnotation</c>).
    /// </summary>
    /// <param name="property">The property to configure.</param>
    /// <param name="configMethod">The configuration method name (e.g. <c>HasMaxLength</c>).</param>
    /// <param name="configArg">The raw argument text captured between the call's parentheses.</param>
    /// <param name="compilation">The Roslyn compilation for constant/enum resolution.</param>
    private static EfProperty ApplyConfiguration(EfProperty property, string configMethod, string configArg,
        Compilation compilation)
    {
        return configMethod switch
        {
            EfAnalysisConstants.EfMethods.IsRequired => ApplyIsRequiredConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasMaxLength => ApplyMaxLengthConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasPrecision => ApplyPrecisionConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasColumnType => ApplyColumnTypeConfiguration(property, configArg),
            EfAnalysisConstants.EfMethods.HasDefaultValue => DefaultValueResolver.CreateWithDefaultValue(property,
                configArg, compilation),
            EfAnalysisConstants.EfMethods.HasDefaultValueSql => DefaultValueResolver.CreateWithDefaultValueSql(
                property, configArg),
            _ => property
        };
    }

    private static EfProperty ApplyIsRequiredConfiguration(EfProperty property, string configArg)
    {
        var isRequired = string.IsNullOrEmpty(configArg) ||
                         configArg.Equals("true", StringComparison.OrdinalIgnoreCase);
        return EfPropertyFactory.CopyWith(property, new EfPropertyOverrides
        {
            IsRequired = isRequired,
            IsExplicitlyRequired = isRequired || property.IsExplicitlyRequired
        });
    }

    private static EfProperty ApplyMaxLengthConfiguration(EfProperty property, string configArg)
    {
        if (int.TryParse(configArg, out var maxLen))
        {
            return EfPropertyFactory.CopyWith(property, new EfPropertyOverrides
            {
                MaxLength = maxLen
            });
        }

        return property;
    }

    /// <summary>
    /// Configures the column type for a property, inferring max length from column type definition if needed.
    /// </summary>
    /// <param name="property">The property to configure.</param>
    /// <param name="configArg">The column type argument.</param>
    private static EfProperty ApplyColumnTypeConfiguration(EfProperty property, string configArg)
    {
        if (property.MaxLength is not null)
        {
            return property;
        }

        var match = EfAnalysisRegexPatterns.NumberInParensRegex().Match(configArg);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var len))
        {
            return EfPropertyFactory.CopyWith(property, new EfPropertyOverrides
            {
                MaxLength = len
            });
        }

        return property;
    }

    /// <summary>
    /// Creates a new property with the specified precision and scale.
    /// </summary>
    /// <param name="property">The source property.</param>
    /// <param name="configArg">The precision/scale argument string.</param>
    private static EfProperty ApplyPrecisionConfiguration(EfProperty property, string configArg)
    {
        var precisionArgs = configArg.Split(',');
        if (precisionArgs.Length < 1 || !int.TryParse(precisionArgs[0].Trim(), out var precision))
        {
            return property;
        }

        int? scale = null;
        if (precisionArgs.Length >= 2 && int.TryParse(precisionArgs[1].Trim(), out var s))
        {
            scale = s;
        }

        return EfPropertyFactory.CopyWith(property, new EfPropertyOverrides
        {
            Precision = precision,
            Scale = scale ?? property.Scale
        });
    }
}
