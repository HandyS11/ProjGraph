using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Services.EfAnalysis;

/// <summary>
/// Parser for Entity Framework ModelSnapshot files.
/// </summary>
public static class ModelSnapshotParser
{
    /// <summary>
    /// Parses a ModelSnapshot class to build an EfModel.
    /// </summary>
    public static EfModel Parse(ClassDeclarationSyntax snapshotClass, INamedTypeSymbol snapshotType,
        Compilation compilation)
    {
        var model = new EfModel { ContextName = ExtractContextName(snapshotType) };
        var entities = new Dictionary<string, EfEntity>();

        var buildModelMethod = snapshotClass.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "BuildModel");

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

    private static string ExtractContextName(INamedTypeSymbol snapshotType)
    {
        var dbContextAttr = snapshotType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "DbContextAttribute");

        if (dbContextAttr?.ConstructorArguments.Length > 0 &&
            dbContextAttr.ConstructorArguments[0].Value is INamedTypeSymbol contextType)
        {
            return contextType.Name;
        }

        return snapshotType.Name.Replace("ModelSnapshot", "");
    }
}