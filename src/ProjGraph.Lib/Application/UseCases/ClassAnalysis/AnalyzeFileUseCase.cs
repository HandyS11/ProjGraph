using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Application.Interfaces;
using ProjGraph.Lib.Application.Services;

namespace ProjGraph.Lib.Application.UseCases.ClassAnalysis;

/// <summary>
/// Use case for analyzing a C# source file to extract class definitions and their relationships.
/// </summary>
public class AnalyzeFileUseCase(
    ICompilationFactory compilationFactory,
    ITypeProcessor typeProcessor,
    IFileSystem fileSystem)
{
    /// <summary>
    /// Executes the analysis of a C# source file to extract class definitions and their relationships.
    /// </summary>
    /// <param name="filePath">The path to the C# source file to analyze.</param>
    /// <param name="includeInheritance">Specifies whether to include inheritance relationships in the analysis.</param>
    /// <param name="includeDependencies">Specifies whether to include dependency relationships in the analysis.</param>
    /// <param name="maxDepth">The maximum depth for analyzing relationships.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the analyzed class model.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified source file is not found.</exception>
    public async Task<ClassModel> ExecuteAsync(
        string filePath,
        bool includeInheritance = true,
        bool includeDependencies = false,
        int maxDepth = 1)
    {
        if (!fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException("Source file not found", filePath);
        }

        var startDir = fileSystem.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
        var code = fileSystem.ReadAllText(filePath);
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

        var options = new AnalysisOptions
        {
            MaxDepth = maxDepth, IncludeInheritance = includeInheritance, IncludeDependencies = includeDependencies
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

        var typeDeclarations = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();

        foreach (var typeDecl in typeDeclarations)
        {
            if (semanticModel.GetDeclaredSymbol(typeDecl) is not { } symbol)
            {
                continue;
            }

            typesToAnalyze.Enqueue((symbol, 0));
        }
    }
}