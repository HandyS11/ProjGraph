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
    Task<Dictionary<string, string>> DiscoverEntityFilesAsync(
        List<string> searchDirectories,
        HashSet<string> entityTypeNames,
        string contextFilePath);

    /// <summary>
    /// Discovers source files containing base class definitions used by entity types.
    /// </summary>
    Task<Dictionary<string, string>> DiscoverBaseClassFilesAsync(Dictionary<string, string> entityFiles,
        string contextDirectory);

    /// <summary>
    /// Builds a list of directories to search for entity files based on a context file directory.
    /// </summary>
    List<string> BuildSearchDirectories(string contextDirectory);

    /// <summary>
    /// Extracts entity type names from a DbContext class declaration.
    /// </summary>
    HashSet<string> ExtractEntityTypeNames(ClassDeclarationSyntax contextClass);

    /// <summary>
    /// Extracts base class names from a syntax node and adds them to the provided set.
    /// </summary>
    void ExtractBaseClassNamesFromSyntax(SyntaxNode root, HashSet<string> baseClassNames);

    /// <summary>
    /// Searches for source files containing base class definitions in a directory.
    /// </summary>
    Dictionary<string, string> SearchForBaseClassFiles(HashSet<string> baseClassNames, DirectoryInfo searchDirectory);
}
