using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;

namespace ProjGraph.Lib.ClassDiagram.Infrastructure;

/// <summary>
/// Provides methods for discovering type definitions within a workspace.
/// This class includes functionality to locate files containing specific type definitions
/// by searching directories and analyzing C# source files using Roslyn.
/// </summary>
/// <param name="fileSystem">The file system abstraction for file operations.</param>
internal sealed class WorkspaceTypeDiscovery(IFileSystem fileSystem) : IWorkspaceTypeDiscovery
{
    /// <summary>
    /// Finds the file containing the definition of a specific type within a given directory or its subdirectories.
    /// The method first attempts to search in common subdirectories for better performance, and if not found,
    /// it searches the entire root directory. The search uses both a simple string match and Roslyn for verification.
    /// </summary>
    /// <param name="typeName">The name of the type to search for (e.g., class, interface, struct, enum, or record).</param>
    /// <param name="startDirectory">The starting directory to begin the search.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the full path of the file
    /// containing the type definition if found; otherwise, null.
    /// </returns>
    public async Task<string?> FindTypeDefinitionFileAsync(string typeName, string startDirectory)
    {
        var root = WorkspaceRootResolver.FindWorkspaceRoot(startDirectory) ?? startDirectory;

        // Common file patterns to search first (optimistic)
        foreach (var dirName in new[]
                 {
                     "Models", "Entities", "Services", "Interfaces", "Common", "Data", "Internal"
                 })
        {
            var path = fileSystem.Combine(root, dirName);
            if (!fileSystem.DirectoryExists(path))
            {
                continue;
            }

            var found = await SearchDirectoryForTypeAsync(path, typeName);
            if (found != null)
            {
                return found;
            }
        }

        // Search the whole root if not found in common dirs
        return await SearchDirectoryForTypeAsync(root, typeName);
    }

    /// <summary>
    /// Searches a directory and its subdirectories for a C# file containing a specific type definition.
    /// The method first performs a simple string check for the type name in the file content for performance,
    /// and then uses Roslyn to verify the presence of the type definition.
    /// </summary>
    /// <param name="directory">The path of the directory to search.</param>
    /// <param name="typeName">The name of the type to search for (e.g., class, interface, struct, enum, or record).</param>
    /// <returns>
    /// The full path of the file containing the type definition if found; otherwise, null.
    /// </returns>
    private async Task<string?> SearchDirectoryForTypeAsync(string directory, string typeName)
    {
        var matches = new List<string>();
        await CollectTypeMatchesAsync(directory, typeName, matches);

        if (matches.Count <= 1)
        {
            return matches.FirstOrDefault();
        }

        // Multiple files define the same type name — sort by path for deterministic results
        matches.Sort(StringComparer.OrdinalIgnoreCase);
        return matches[0];
    }

    /// <summary>
    /// Recursively searches a directory and its subdirectories for C# files containing a specific type definition,
    /// collecting all matches for deterministic resolution.
    /// </summary>
    /// <param name="directory">The path of the directory to search.</param>
    /// <param name="typeName">The name of the type to search for.</param>
    /// <param name="matches">The list to collect matching file paths into.</param>
    private async Task CollectTypeMatchesAsync(string directory, string typeName, List<string> matches)
    {
        var enumerationOptions = new EnumerationOptions
        {
            IgnoreInaccessible = true
        };

        // Search files in the current directory
        foreach (var file in fileSystem.EnumerateFiles(directory, FilePathGuard.CSharpFilesPattern,
                     enumerationOptions))
        {
            // Simple string check first for performance
            var content = await fileSystem.ReadAllTextAsync(file);
            if (!content.Contains($"class {typeName}", StringComparison.Ordinal) &&
                !content.Contains($"interface {typeName}", StringComparison.Ordinal) &&
                !content.Contains($"struct {typeName}", StringComparison.Ordinal) &&
                !content.Contains($"enum {typeName}", StringComparison.Ordinal) &&
                !content.Contains($"record {typeName}", StringComparison.Ordinal))
            {
                continue;
            }

            // Verify with Roslyn to be sure
            var syntaxTree = CSharpSyntaxTree.ParseText(content);
            var root = await syntaxTree.GetRootAsync();
            var hasType = root.DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Any(t => t.Identifier.Text == typeName);

            if (hasType)
            {
                matches.Add(file);
            }
        }

        // Recursively search subdirectories, skipping excluded directories
        foreach (var subDir in fileSystem.EnumerateDirectories(directory, "*",
                     enumerationOptions))
        {
            if (DirectoryFilters.ShouldSkipDirectory(subDir))
            {
                continue;
            }

            await CollectTypeMatchesAsync(subDir, typeName, matches);
        }
    }
}
