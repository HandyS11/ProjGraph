using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ProjGraph.Lib.EntityFramework.Application;

/// <summary>
/// Provides methods for discovering entity-related source files within a workspace.
/// </summary>
public interface IEntityFileDiscovery
{
    /// <summary>
    /// Discovers source files containing entity type definitions referenced by a DbContext.
    /// </summary>
    /// <param name="searchDirectories">The directories to search for entity files.</param>
    /// <param name="entityTypeNames">The set of entity type names to search for.</param>
    /// <param name="contextFilePath">The file path of the context file to exclude.</param>
    Task<Dictionary<string, string>> DiscoverEntityFilesAsync(
        IReadOnlyList<string> searchDirectories,
        HashSet<string> entityTypeNames,
        string contextFilePath);

    /// <summary>
    /// Discovers source files containing base class definitions used by entity types.
    /// </summary>
    /// <param name="entityFiles">A dictionary of entity type names and their file paths.</param>
    /// <param name="contextDirectory">The directory containing the context file.</param>
    Task<Dictionary<string, string>> DiscoverBaseClassFilesAsync(Dictionary<string, string> entityFiles,
        string contextDirectory);

    /// <summary>
    /// Builds a list of directories to search for entity files based on a context file directory.
    /// </summary>
    /// <param name="contextDirectory">The directory containing the context file.</param>
    IReadOnlyList<string> BuildSearchDirectories(string contextDirectory);

    /// <summary>
    /// Extracts entity type names from a DbContext class declaration.
    /// </summary>
    /// <param name="contextClass">The class declaration syntax of the DbContext.</param>
    HashSet<string> ExtractEntityTypeNames(ClassDeclarationSyntax contextClass);

    /// <summary>
    /// Extracts base class names from a syntax node and adds them to the provided set.
    /// </summary>
    /// <param name="root">The root syntax node to analyze.</param>
    /// <param name="baseClassNames">The set to store extracted base class names.</param>
    void ExtractBaseClassNamesFromSyntax(SyntaxNode root, HashSet<string> baseClassNames);

    /// <summary>
    /// Searches for source files containing base class definitions in a directory.
    /// </summary>
    /// <param name="baseClassNames">The set of base class names to search for.</param>
    /// <param name="searchDirectory">The directory to search within.</param>
    Dictionary<string, string> SearchForBaseClassFiles(HashSet<string> baseClassNames, DirectoryInfo searchDirectory);
}
