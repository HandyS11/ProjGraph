using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Discovers <c>IEntityTypeConfiguration&lt;T&gt;</c> classes referenced from a DbContext's
/// <c>OnModelCreating</c> method — via <c>modelBuilder.ApplyConfiguration(new XConfig())</c> or
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(...)</c> — and folds each class's
/// <c>Configure(EntityTypeBuilder&lt;T&gt;)</c> body into the model by running the fluent walkers with
/// <c>T</c> as their ambient entity. Config classes are matched by syntax (their
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> base list), so no EF Core reference is required.
/// </summary>
internal static class EntityConfigurationWalker
{
    /// <summary>A config class resolved from the compilation: its type name, target entity, and Configure body.</summary>
    /// <param name="ClassName">The config class's simple name.</param>
    /// <param name="EntityName">The configured entity type <c>T</c>.</param>
    /// <param name="Configure">The <c>Configure(EntityTypeBuilder&lt;T&gt;)</c> method declaration.</param>
    private readonly record struct ConfigClass(string ClassName, string EntityName, MethodDeclarationSyntax Configure);

    /// <summary>
    /// Applies every <c>IEntityTypeConfiguration&lt;T&gt;</c> class referenced from <paramref name="method"/>.
    /// </summary>
    /// <param name="method">The <c>OnModelCreating</c> method declaration.</param>
    /// <param name="entities">Entities already discovered; augmented with materialized config-only entities.</param>
    /// <param name="model">The model whose entities/relationships are populated.</param>
    /// <param name="compilation">The compilation whose syntax trees are scanned for config classes.</param>
    public static void Apply(
        MethodDeclarationSyntax method,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        var explicitNames = CollectExplicitConfigNames(method);
        var applyAll = HasApplyFromAssembly(method);
        if (explicitNames.Count == 0 && !applyAll)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var configClass in FindConfigClasses(compilation))
        {
            if (!applyAll && !explicitNames.Contains(configClass.ClassName))
            {
                continue;
            }

            if (!seen.Add(configClass.ClassName))
            {
                continue;
            }

            MaterializeEntity(configClass.EntityName, entities, model, compilation);

            FluentEntityWalker.Apply(configClass.Configure, entities, model, compilation, configClass.EntityName);
            FluentPropertyWalker.Apply(configClass.Configure, entities, compilation, configClass.EntityName);
            FluentRelationshipWalker.Apply(configClass.Configure, entities, model, compilation, configClass.EntityName);
        }
    }

    /// <summary>Collects the config-class type names from every <c>ApplyConfiguration(new X())</c> call in the method.</summary>
    /// <param name="method">The method to scan.</param>
    private static HashSet<string> CollectExplicitConfigNames(MethodDeclarationSyntax method)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax ma
                || SimpleName(ma.Name) != EfAnalysisConstants.EfMethods.ApplyConfiguration)
            {
                continue;
            }

            if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is ObjectCreationExpressionSyntax creation)
            {
                names.Add(TypeName(creation.Type));
            }
        }

        return names;
    }

    /// <summary>Returns whether the method contains any <c>ApplyConfigurationsFromAssembly(...)</c> call.</summary>
    /// <param name="method">The method to scan.</param>
    private static bool HasApplyFromAssembly(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax ma
                               && SimpleName(ma.Name) == EfAnalysisConstants.EfMethods.ApplyConfigurationsFromAssembly);
    }

    /// <summary>Enumerates every class in the compilation that implements <c>IEntityTypeConfiguration&lt;T&gt;</c> and has a <c>Configure</c> method.</summary>
    /// <param name="compilation">The compilation whose syntax trees are scanned.</param>
    private static IEnumerable<ConfigClass> FindConfigClasses(Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (AsConfigClass(declaration) is { } configClass)
                {
                    yield return configClass;
                }
            }
        }
    }

    /// <summary>Interprets a class declaration as a config class, or returns <see langword="null"/> if it is not one.</summary>
    /// <param name="declaration">The class declaration.</param>
    private static ConfigClass? AsConfigClass(ClassDeclarationSyntax declaration)
    {
        var configInterface = declaration.BaseList?.Types
            .Select(baseType => baseType.Type)
            .OfType<GenericNameSyntax>()
            .FirstOrDefault(generic =>
                generic.Identifier.Text == EfAnalysisConstants.EfMethods.EntityTypeConfigurationInterface
                && generic.TypeArgumentList.Arguments.Count == 1);

        if (configInterface is null)
        {
            return null;
        }

        var configure = declaration.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(member => member.Identifier.Text == EfAnalysisConstants.EfMethods.Configure);

        if (configure is null)
        {
            return null;
        }

        var entityName = TypeName(configInterface.TypeArgumentList.Arguments[0]);
        return new ConfigClass(declaration.Identifier.Text, entityName, configure);
    }

    /// <summary>
    /// Materializes the entity named <paramref name="entityName"/> when it is not already known, resolving its
    /// symbol (or falling back to a bare entity) and adding it to both the entities dictionary and the model.
    /// Mirrors <see cref="FluentEntityWalker"/>'s materialization.
    /// </summary>
    /// <param name="entityName">The configured entity type name.</param>
    /// <param name="entities">The known entities, augmented in place.</param>
    /// <param name="model">The model whose <see cref="EfModel.Entities"/> collection is augmented.</param>
    /// <param name="compilation">The compilation for symbol resolution.</param>
    private static void MaterializeEntity(
        string entityName,
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
