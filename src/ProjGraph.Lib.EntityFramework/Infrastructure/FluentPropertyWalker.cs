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
/// text/regex property parser. The receiver expression of each chain determines the owning entity (see
/// <see cref="FluentSyntax.ResolveOwningEntity"/>), so configuration never leaks between unrelated
/// statements or into nested owned-type / join-entity builder lambdas.
/// </summary>
internal static class FluentPropertyWalker
{
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
        foreach (var propertyRoot in FluentSyntax.FindConfigRoots(method, EfAnalysisConstants.EfMethods.Property))
        {
            ApplyPropertyChain(propertyRoot, entities, compilation, ambientEntity);
        }

        foreach (var keyRoot in FluentSyntax.FindConfigRoots(method, EfAnalysisConstants.EfMethods.HasKey))
        {
            ApplyKey(keyRoot, entities, ambientEntity);
        }
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
        var entityName = FluentSyntax.ResolveOwningEntity(propertyRoot, ambientEntity);
        if (entityName is null || !entities.TryGetValue(entityName, out var entity))
        {
            return;
        }

        var propertyName = SingleArgumentName(propertyRoot);
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        var type = FluentSyntax.GenericTypeArgumentName(propertyRoot) ?? "";
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
        var entityName = FluentSyntax.ResolveOwningEntity(keyRoot, ambientEntity);
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

    /// <summary>Enumerates the invocation calls chained after <paramref name="root"/>, in source order.</summary>
    /// <param name="root">The chain-seeding invocation (e.g. a <c>Property</c> call).</param>
    private static IEnumerable<(string Name, InvocationExpressionSyntax Invocation)> TrailingCalls(
        InvocationExpressionSyntax root)
    {
        for (var cursor = root.Parent;
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
    /// Configures a property from an explicit SQL column type: recovers the CLR type when the current
    /// type is only the <c>string</c> fallback (e.g. a cross-project entity whose type was guessed),
    /// captures decimal precision/scale, and infers max length from a sized string column type.
    /// </summary>
    /// <param name="property">The property to configure.</param>
    /// <param name="configArg">The column type argument (e.g. <c>decimal(18,2)</c>, <c>nvarchar(200)</c>).</param>
    private static EfProperty ApplyColumnTypeConfiguration(EfProperty property, string configArg)
    {
        var updated = property;

        // The SQL column type is authoritative. When the CLR type is only the guessed string fallback,
        // recover a more accurate value type (e.g. decimal, Guid, bool) from the column type.
        var inferredType = SqlColumnTypeMapper.ToClrType(configArg);
        if (inferredType is not null &&
            updated.Type.Equals(EfAnalysisConstants.DataTypes.StringTypeName, StringComparison.OrdinalIgnoreCase))
        {
            updated = EfPropertyFactory.CopyWith(updated, new EfPropertyOverrides
            {
                Type = inferredType,
                IsValueType = EfPropertyFactory.IsValueTypeString(inferredType)
            });
        }

        // decimal(precision, scale): capture both constraints.
        var decimalMatch = EfAnalysisRegexPatterns.DecimalPrecisionRegex().Match(configArg);
        if (decimalMatch.Success &&
            int.TryParse(decimalMatch.Groups[1].Value, out var precision) &&
            int.TryParse(decimalMatch.Groups[2].Value, out var scale))
        {
            updated = EfPropertyFactory.CopyWith(updated, new EfPropertyOverrides
            {
                Precision = precision,
                Scale = scale
            });
        }
        else if (updated.MaxLength is null)
        {
            // Sized string column types such as nvarchar(200): capture max length.
            var lengthMatch = EfAnalysisRegexPatterns.NumberInParensRegex().Match(configArg);
            if (lengthMatch.Success && int.TryParse(lengthMatch.Groups[1].Value, out var len))
            {
                updated = EfPropertyFactory.CopyWith(updated, new EfPropertyOverrides
                {
                    MaxLength = len
                });
            }
        }

        return updated;
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
