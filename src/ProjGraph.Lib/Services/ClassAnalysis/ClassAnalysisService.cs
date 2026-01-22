using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Interfaces;
using ProjGraph.Lib.Services.EfAnalysis;

namespace ProjGraph.Lib.Services.ClassAnalysis;

/// <summary>
/// Represents a service for analyzing C# classes and their relationships.
/// This service implements the <see cref="IClassAnalysisService"/> interface
/// and provides methods to analyze C# source files and extract class definitions,
/// relationships, and other metadata.
/// </summary>
public class ClassAnalysisService : IClassAnalysisService
{
    /// <summary>
    /// Analyzes a C# source file to extract class definitions and their relationships.
    /// This method reads the file, parses its syntax tree, and performs an analysis to identify
    /// class models and their relationships based on the provided options.
    /// </summary>
    /// <param name="filePath">The path to the C# source file to analyze.</param>
    /// <param name="includeInheritance">
    /// A boolean value indicating whether to include inheritance relationships in the analysis.
    /// </param>
    /// <param name="includeDependencies">
    /// A boolean value indicating whether to include dependency relationships in the analysis.
    /// </param>
    /// <param name="maxDepth">The maximum depth of type relationships to analyze.</param>
    /// <returns>
    /// A <see cref="ClassModel"/> object containing the analyzed class definitions and their relationships.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    public async Task<ClassModel> AnalyzeFileAsync(
        string filePath,
        bool includeInheritance = true,
        bool includeDependencies = false,
        int maxDepth = 1)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Source file not found", filePath);
        }

        var startDir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var code = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath);

        var compilation = CompilationFactory.CreateCompilation([syntaxTree]);

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

        await TypeProcessor.ProcessTypeQueueAsync(typesToAnalyze, context, options);

        return new ClassModel(Path.GetFileName(filePath), context.Types, context.Relationships);
    }

    /// <summary>
    /// Enqueues the initial types from the provided syntax tree into the analysis queue.
    /// This method extracts all type declarations from the syntax tree, resolves their symbols,
    /// and adds them to the queue for further analysis with an initial depth of 0.
    /// </summary>
    /// <param name="syntaxTree">The <see cref="SyntaxTree"/> representing the source code to analyze.</param>
    /// <param name="compilation">The <see cref="CSharpCompilation"/> used to obtain semantic information.</param>
    /// <param name="typesToAnalyze">
    /// A queue of tuples where each tuple contains a symbol representing a type and its depth in the analysis hierarchy.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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