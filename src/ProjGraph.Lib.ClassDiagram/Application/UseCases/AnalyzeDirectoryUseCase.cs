using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Lib.ClassDiagram.Application.UseCases;

/// <summary>
/// Use case for analyzing a directory recursively to extract class definitions and their relationships.
/// </summary>
/// <param name="discoverCsFilesUseCase">The use case for discovering .cs files.</param>
/// <param name="compilationFactory">The factory for creating compilations.</param>
/// <param name="typeProcessor">The type processor for analyzing type symbols.</param>
/// <param name="fileSystem">The file system abstraction.</param>
public class AnalyzeDirectoryUseCase(
    IDiscoverCsFilesUseCase discoverCsFilesUseCase,
    ICompilationFactory compilationFactory,
    ITypeProcessor typeProcessor,
    IFileSystem fileSystem)
{
    /// <summary>
    /// Executes the analysis of a directory recursively to extract class definitions and their relationships.
    /// </summary>
    /// <param name="directoryPath">The path to the directory to analyze.</param>
    /// <param name="options">The analysis options.</param>
    /// <returns>A Task that represents the asynchronous operation. The task result contains the analyzed class model.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory is not found.</exception>
    public async Task<ClassModel> ExecuteAsync(
        string directoryPath,
        AnalysisOptions? options = null)
    {
        options ??= new AnalysisOptions();

        if (!fileSystem.DirectoryExists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var fullPath = fileSystem.GetFullPath(directoryPath);
        var csFiles = discoverCsFilesUseCase.Execute(fullPath);

        if (csFiles.Count == 0)
        {
            return new ClassModel(Path.GetFileName(fullPath), [], []);
        }

        var syntaxTrees = new List<SyntaxTree>();
        foreach (var file in csFiles)
        {
            var code = await fileSystem.ReadAllTextAsync(file);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(code, path: file));
        }

        var compilation = (CSharpCompilation)compilationFactory.CreateCompilation(syntaxTrees);

        var context = new AnalysisContext
        {
            AnalyzedTypeFullNames = [],
            Types = [],
            Relationships = [],
            Compilation = compilation,
            StartDirectory = fullPath
        };

        var typesToAnalyze = new Queue<(INamedTypeSymbol Symbol, int Depth)>();

        foreach (var tree in syntaxTrees)
        {
            var root = await tree.GetRootAsync();
            var semanticModel = compilation.GetSemanticModel(tree);

            foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(typeDecl) is { } symbol)
                {
                    typesToAnalyze.Enqueue((symbol, 0));
                }
            }
        }

        await typeProcessor.ProcessTypeQueueAsync(typesToAnalyze, context, options);

        return new ClassModel(Path.GetFileName(fullPath), context.Types, context.Relationships);
    }
}
