using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure.Constants;

namespace ProjGraph.Lib.EntityFramework.Infrastructure;

/// <summary>
/// Parser for Entity Framework ModelSnapshot files: locates the snapshot's <c>BuildModel</c> method and
/// folds its fluent configuration into an <see cref="EfModel"/> via the Roslyn syntax walkers
/// (<see cref="FluentEntityWalker"/>, <see cref="FluentPropertyWalker"/>, <see cref="FluentRelationshipWalker"/>).
/// </summary>
public static class ModelSnapshotParser
{
    /// <summary>
    /// Parses a ModelSnapshot class to extract the Entity Framework model.
    /// </summary>
    /// <param name="snapshotClass">The <see cref="ClassDeclarationSyntax"/> representing the ModelSnapshot class.</param>
    /// <param name="snapshotType">The <see cref="INamedTypeSymbol"/> representing the type of the ModelSnapshot class.</param>
    /// <param name="compilation">The <see cref="Compilation"/> object used for Roslyn analysis.</param>
    /// <returns>
    /// An <see cref="EfModel"/> object representing the parsed Entity Framework model, including its context name and entities.
    /// </returns>
    public static EfModel Parse(ClassDeclarationSyntax snapshotClass, INamedTypeSymbol snapshotType,
        Compilation compilation)
    {
        var model = new EfModel { ContextName = ExtractContextName(snapshotType) };
        var entities = new Dictionary<string, EfEntity>();

        var buildModelMethod = snapshotClass.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == EfAnalysisConstants.EfMethods.BuildModel);

        if (buildModelMethod is null || (buildModelMethod.Body is null && buildModelMethod.ExpressionBody is null))
        {
            return model;
        }

        // A generated snapshot's BuildModel has the same fluent shape as OnModelCreating (string-based
        // Entity/Property/HasKey/HasOne overloads), so the same Roslyn syntax walkers apply, in the same
        // order as the context path. The walkers add materialized entities to the model directly.
        FluentEntityWalker.Apply(buildModelMethod, entities, model, compilation);
        FluentPropertyWalker.Apply(buildModelMethod, entities, compilation);
        FluentRelationshipWalker.Apply(buildModelMethod, entities, model, compilation);

        return model;
    }

    /// <summary>
    /// Extracts the name of the DbContext associated with the given ModelSnapshot type.
    /// </summary>
    /// <param name="snapshotType">The <see cref="INamedTypeSymbol"/> representing the ModelSnapshot type.</param>
    /// <returns>
    /// A string representing the name of the DbContext associated with the ModelSnapshot.
    /// If the DbContextAttribute is present, its constructor argument is used to determine the name.
    /// Otherwise, the method derives the name by removing "ModelSnapshot" from the type name.
    /// </returns>
    private static string ExtractContextName(INamedTypeSymbol snapshotType)
    {
        var dbContextAttr = snapshotType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == EfAnalysisConstants.EfAttributes.DbContextAttribute);

        if (dbContextAttr?.ConstructorArguments.Length > 0 &&
            dbContextAttr.ConstructorArguments[0].Value is INamedTypeSymbol contextType)
        {
            return contextType.Name;
        }

        return snapshotType.Name.Replace("ModelSnapshot", "", StringComparison.Ordinal);
    }
}
