using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Lib.ClassDiagram.Infrastructure;

/// <summary>
/// Provides methods for resolving type symbols and loading their definitions from source files.
/// </summary>
/// <param name="workspaceTypeDiscovery">The workspace type discovery service.</param>
/// <param name="fileSystem">The file system abstraction for file operations.</param>
internal sealed class SymbolResolver(IWorkspaceTypeDiscovery workspaceTypeDiscovery, IFileSystem fileSystem)
    : ISymbolResolver
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
    public async Task<INamedTypeSymbol?> ResolveRelatedSymbolAsync(
        INamedTypeSymbol relatedSymbol,
        AnalysisContext context)
    {
        // Ensure we are working with the original definition (e.g., strip nullability markers)
        var symbolToResolve = relatedSymbol.OriginalDefinition;

        // A symbol declared in the current compilation is already fully resolved. Searching the
        // workspace again would waste a full directory scan and could bind a same-named type
        // from an unrelated file instead of this exact declaration.
        if (symbolToResolve.Locations.Any(l => l.IsInSource))
        {
            return symbolToResolve;
        }

        // A non-error symbol that is not in source was resolved from a referenced assembly.
        // There is nothing to discover in the workspace — and a scan could rebind the type to
        // an unrelated source type sharing the same simple name. Record it as external.
        // Workspace discovery below is reserved for unresolved (error) symbols.
        if (symbolToResolve.TypeKind != Microsoft.CodeAnalysis.TypeKind.Error)
        {
            AddExternalType(symbolToResolve, context);
            return null;
        }

        if (!context.TypeFileLookupCache.TryGetValue(symbolToResolve.Name, out var foundFile))
        {
            foundFile = await workspaceTypeDiscovery.FindTypeDefinitionFileAsync(
                symbolToResolve.Name, context.StartDirectory);
            context.TypeFileLookupCache[symbolToResolve.Name] = foundFile;
        }

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
    private async Task<INamedTypeSymbol?> LoadAndResolveSymbolAsync(
        INamedTypeSymbol relatedSymbol,
        string foundFile,
        AnalysisContext context)
    {
        var existingTree = context.Compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == foundFile);
        SyntaxTree treeToUse;

        if (existingTree == null)
        {
            var relatedCode = await fileSystem.ReadAllTextAsync(foundFile);
            treeToUse = CSharpSyntaxTree.ParseText(relatedCode, path: foundFile);
            context.AddSyntaxTrees(treeToUse);
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

        // Add the external node only once; the same external type may be referenced by many
        // source types, and each reference would otherwise produce a duplicate node.
        if (!context.AnalyzedTypeFullNames.Add(fullName))
        {
            return;
        }

        context.Types.Add(new TypeDefinition(
            relatedSymbol.Name,
            relatedSymbol.ContainingNamespace.ToDisplayString(),
            fullName,
            TypeAnalyzer.MapKind(relatedSymbol),
            [],
            relatedSymbol.IsAbstract));
    }
}
