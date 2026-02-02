using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Services.EfAnalysis.Constants;

namespace ProjGraph.Lib.Services.EfAnalysis;

/// <summary>
/// Parser for Entity Framework ModelSnapshot files.
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

        if (buildModelMethod?.Body is null)
        {
            return model;
        }

        // Use the updated FluentApiConfigurationParser to process sections
        FluentApiConfigurationParser.ApplyConstraintsFromMethod(buildModelMethod, entities, model, compilation);

        // Populate the model entities if they weren't already added by ApplyConstraintsFromMethod
        foreach (var entity in entities.Values.Where(entity => model.Entities.All(e => e.Name != entity.Name)))
        {
            model.Entities.Add(entity);
        }

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

        return snapshotType.Name.Replace("ModelSnapshot", "");
    }
}