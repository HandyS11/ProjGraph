using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Orchestrates the DbContext path's Fluent API analysis: locates <c>OnModelCreating</c> and folds its
/// configuration into the model via the Roslyn syntax walkers.
/// </summary>
public static class FluentApiConfigurationParser
{
    /// <summary>
    /// Applies Fluent API constraints to the specified Entity Framework model by parsing the "OnModelCreating" method
    /// of the provided context type and processing each entity configuration section.
    /// </summary>
    /// <param name="contextType">The named type symbol of the DbContext class.</param>
    /// <param name="entities">The dictionary of entities in the model.</param>
    /// <param name="model">The EF model to apply constraints to.</param>
    /// <param name="compilation">The Roslyn compilation for symbol resolution.</param>
    public static void ApplyFluentApiConstraints(
        INamedTypeSymbol contextType,
        Dictionary<string, EfEntity> entities,
        EfModel model,
        Compilation compilation)
    {
        var methodSyntax = FindOnModelCreatingMethod(contextType);
        if (methodSyntax is null || (methodSyntax.Body is null && methodSyntax.ExpressionBody is null))
        {
            return;
        }

        // Every concern flows through the Roslyn syntax walkers. FluentEntityWalker materializes
        // fluent-only entities and applies ToTable; FluentPropertyWalker derives property config +
        // primary keys; FluentRelationshipWalker derives relationships + foreign keys. The second
        // FluentEntityWalker pass applies ToTable calls whose owning entity is an owned type that did not
        // exist during the first pass (it is idempotent for entities already known); ResolveTables then
        // runs last so a chained ToTable is already applied and is not overwritten by the table-splitting
        // default.
        FluentEntityWalker.Apply(methodSyntax, entities, model, compilation);
        FluentOwnedTypeWalker.Apply(methodSyntax, entities, model, compilation);
        FluentPropertyWalker.Apply(methodSyntax, entities, compilation);
        FluentEntityWalker.Apply(methodSyntax, entities, model, compilation);
        FluentOwnedTypeWalker.ResolveTables(entities, model);
        FluentRelationshipWalker.Apply(methodSyntax, entities, model, compilation);

        // Fold IEntityTypeConfiguration<T> classes referenced via ApplyConfiguration /
        // ApplyConfigurationsFromAssembly by walking each Configure(EntityTypeBuilder<T>) body with T as
        // the ambient entity (Slice 4).
        EntityConfigurationWalker.Apply(methodSyntax, entities, model, compilation);

        // Runs last, after every pass that could still mark an owned property's shadow PK/FK (including
        // a context-path HasKey/HasForeignKey reached via EntityConfigurationWalker), so both the
        // DbContext and snapshot paths strip the same EF implementation details identically.
        FluentOwnedTypeWalker.StripShadowKeys(entities, model);
    }

    private static MethodDeclarationSyntax? FindOnModelCreatingMethod(INamedTypeSymbol contextType)
    {
        var onModelCreating = contextType.GetMembers(EfAnalysisConstants.EfMethods.OnModelCreating)
            .OfType<IMethodSymbol>()
            .FirstOrDefault();

        var syntaxRef = onModelCreating?.DeclaringSyntaxReferences.FirstOrDefault();
        return syntaxRef?.GetSyntax() as MethodDeclarationSyntax;
    }
}
