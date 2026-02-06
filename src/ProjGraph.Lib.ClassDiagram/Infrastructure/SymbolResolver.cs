using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;

namespace ProjGraph.Lib.ClassDiagram.Infrastructure;

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
        // Ensure we are working with the original definition (e.g., strip nullability markers)
        var symbolToResolve = relatedSymbol.OriginalDefinition;

        var foundFile =
            await WorkspaceTypeDiscovery.FindTypeDefinitionFileAsync(symbolToResolve.Name, context.StartDirectory);

        if (foundFile is not null)
        {
            return await LoadAndResolveSymbolAsync(symbolToResolve, foundFile, context);
        }

        AddExternalType(symbolToResolve, context);
        return null;
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
        var existingTree = context.Compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == foundFile);
        SyntaxTree treeToUse;

        if (existingTree == null)
        {
            var relatedCode = await File.ReadAllTextAsync(foundFile);
            treeToUse = CSharpSyntaxTree.ParseText(relatedCode, path: foundFile);
            context.Compilation = context.Compilation.AddSyntaxTrees(treeToUse);
        }
        else
        {
            treeToUse = existingTree;
        }

        var newSemanticModel = context.Compilation.GetSemanticModel(treeToUse);
        var root = await treeToUse.GetRootAsync();
        var decls = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();

        // Find the specific type declaration that matches our symbol's name
        var decl = decls.FirstOrDefault(t => t.Identifier.Text == relatedSymbol.Name);

        if (decl is not null && newSemanticModel.GetDeclaredSymbol(decl) is { } resolved)
        {
            return resolved.OriginalDefinition;
        }

        return relatedSymbol.OriginalDefinition;
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





