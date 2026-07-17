using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Lib.ClassDiagram.Application.UseCases;

/// <summary>
/// Use case for analyzing a C# source file to extract class definitions and their relationships.
/// </summary>
/// <param name="compilationFactory">The factory for creating compilations.</param>
/// <param name="typeProcessor">The type processor for analyzing type queues.</param>
/// <param name="fileSystem">The file system abstraction.</param>
public class AnalyzeFileUseCase(
    ICompilationFactory compilationFactory,
    ITypeProcessor typeProcessor,
    IFileSystem fileSystem)
{
    /// <summary>
    /// Executes the analysis of a C# source file to extract class definitions and their relationships.
    /// </summary>
    /// <param name="filePath">The path to the C# source file to analyze.</param>
    /// <param name="options">The analysis options.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the analyzed class model.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified source file is not found.</exception>
    public async Task<ClassModel> ExecuteAsync(
        string filePath,
        AnalysisOptions? options = null)
    {
        options ??= new AnalysisOptions();

        if (!fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException("Source file not found", filePath);
        }

        // GetDirectoryName on a bare relative filename returns "" — resolve the full path first
        // so the workspace walk always starts from a real directory.
        var directoryName = fileSystem.GetDirectoryName(fileSystem.GetFullPath(filePath));
        var startDir = string.IsNullOrEmpty(directoryName) ? Environment.CurrentDirectory : directoryName;
        var code = await fileSystem.ReadAllTextAsync(filePath);

        var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath);
        var compilation = (CSharpCompilation)compilationFactory.CreateCompilation([syntaxTree]);

        var context = new AnalysisContext
        {
            AnalyzedTypeFullNames = [],
            Types = [],
            Relationships = [],
            Compilation = compilation,
            StartDirectory = startDir
        };

        var typesToAnalyze = new Queue<(INamedTypeSymbol Symbol, int Depth)>();

        await EnqueueInitialTypesAsync(syntaxTree, compilation, typesToAnalyze);

        await typeProcessor.ProcessTypeQueueAsync(typesToAnalyze, context, options);

        return new ClassModel(Path.GetFileName(filePath), context.Types, context.Relationships);
    }

    /// <summary>
    /// Enqueues the initial types from the syntax tree for analysis.
    /// </summary>
    /// <param name="syntaxTree">The syntax tree of the C# source file.</param>
    /// <param name="compilation">The C# compilation object for semantic analysis.</param>
    /// <param name="typesToAnalyze">The queue to store types to be analyzed, along with their depth.</param>
    private static async Task EnqueueInitialTypesAsync(
        SyntaxTree syntaxTree,
        CSharpCompilation compilation,
        Queue<(INamedTypeSymbol Symbol, int Depth)> typesToAnalyze)
    {
        var root = await syntaxTree.GetRootAsync();
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(typeDecl) is not { } symbol)
            {
                continue;
            }

            typesToAnalyze.Enqueue((symbol, 0));
        }
    }
}
