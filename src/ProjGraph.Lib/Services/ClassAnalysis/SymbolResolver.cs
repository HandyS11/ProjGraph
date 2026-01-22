using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Services.ClassAnalysis;

/// <summary>
/// Provides methods for resolving type symbols and loading their definitions from source files.
/// </summary>
internal static class SymbolResolver
{
    /// <summary>
    /// Resolves a related symbol by determining if it's already in the compilation or needs to be loaded from a file.
    /// If the symbol is external (not found in source), it's added as an external type.
    /// </summary>
    /// <param name="relatedSymbol">The <see cref="INamedTypeSymbol"/> to resolve.</param>
    /// <param name="context">The <see cref="AnalysisContext"/> containing the current state of the analysis.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the resolved symbol,
    /// or null if it's an external type that was added to the context.
    /// </returns>
    public static async Task<INamedTypeSymbol?> ResolveRelatedSymbolAsync(
        INamedTypeSymbol relatedSymbol,
        AnalysisContext context)
    {
        if (relatedSymbol.Locations.Any(l => l.IsInSource))
        {
            return relatedSymbol;
        }

        var foundFile =
            await WorkspaceTypeDiscovery.FindTypeDefinitionFileAsync(relatedSymbol.Name, context.StartDirectory);

        if (foundFile is null)
        {
            AddExternalType(relatedSymbol, context);
            return null;
        }

        if (context.Compilation.SyntaxTrees.Any(t => t.FilePath == foundFile))
        {
            return relatedSymbol;
        }

        return await LoadAndResolveSymbolAsync(relatedSymbol, foundFile, context);
    }

    /// <summary>
    /// Loads and resolves a related symbol from a specified file and updates the analysis context.
    /// </summary>
    /// <param name="relatedSymbol">The <see cref="INamedTypeSymbol"/> representing the related symbol to resolve.</param>
    /// <param name="foundFile">The file path where the related symbol is defined.</param>
    /// <param name="context">The <see cref="AnalysisContext"/> containing the current state of the analysis.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the resolved <see cref="INamedTypeSymbol"/> 
    /// if found, or the original <paramref name="relatedSymbol"/> if the symbol could not be resolved.
    /// </returns>
    private static async Task<INamedTypeSymbol?> LoadAndResolveSymbolAsync(
        INamedTypeSymbol relatedSymbol,
        string foundFile,
        AnalysisContext context)
    {
        var relatedCode = await File.ReadAllTextAsync(foundFile);
        var relatedTree = CSharpSyntaxTree.ParseText(relatedCode, path: foundFile);
        context.Compilation = context.Compilation.AddSyntaxTrees(relatedTree);

        var newSemanticModel = context.Compilation.GetSemanticModel(relatedTree);
        var root = await relatedTree.GetRootAsync();
        var decl = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.Text == relatedSymbol.Name);

        return decl != null ? newSemanticModel.GetDeclaredSymbol(decl) ?? relatedSymbol : relatedSymbol;
    }

    /// <summary>
    /// Adds an external type to the analysis context. This method is used when a related symbol
    /// is determined to be external (not defined in the current project) and needs to be added
    /// to the list of analyzed types.
    /// </summary>
    /// <param name="relatedSymbol">The <see cref="INamedTypeSymbol"/> representing the external type to add.</param>
    /// <param name="context">The <see cref="AnalysisContext"/> containing the current state of the analysis.</param>
    private static void AddExternalType(
        INamedTypeSymbol relatedSymbol,
        AnalysisContext context)
    {
        // Don't add system types as external type nodes
        if (TypeFilter.IsSystemType(relatedSymbol))
        {
            return;
        }

        var fullName = TypeAnalyzer.GetFullyQualifiedName(relatedSymbol);
        context.Types.Add(new TypeDefinition(
            relatedSymbol.Name,
            relatedSymbol.ContainingNamespace.ToDisplayString(),
            fullName,
            TypeAnalyzer.MapKind(relatedSymbol),
            [],
            relatedSymbol.IsAbstract));
        context.AnalyzedTypeFullNames.Add(fullName);
    }
}