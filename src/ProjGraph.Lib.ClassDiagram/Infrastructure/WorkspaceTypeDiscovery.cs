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
    /// Finds the file containing the definition of a specific type within the workspace root
    /// derived from the given start directory. The search uses both a simple string match and
    /// Roslyn for verification.
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

        // A single scan from the workspace root keeps resolution deterministic: a partial
        // "common directory" pre-pass would return its first hit and override the path-sorted
        // tie-break applied below. Lookups are memoized per type name by the caller.
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

        var files = EnumerateSafely(
            () => fileSystem.EnumerateFiles(directory, FilePathGuard.CSharpFilesPattern, enumerationOptions));

        // Search files in the current directory
        foreach (var file in files)
        {
            // Simple string check first for performance
            string content;
            try
            {
                content = await fileSystem.ReadAllTextAsync(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Skip a file that cannot be read rather than aborting the whole workspace scan.
                continue;
            }

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

        var subDirectories = EnumerateSafely(
            () => fileSystem.EnumerateDirectories(directory, "*", enumerationOptions));

        // Recursively search subdirectories, skipping excluded directories
        foreach (var subDir in subDirectories)
        {
            if (DirectoryFilters.ShouldSkipDirectory(subDir))
            {
                continue;
            }

            await CollectTypeMatchesAsync(subDir, typeName, matches);
        }
    }

    /// <summary>
    /// Materializes a file-system enumeration defensively. Enumeration itself can fail
    /// mid-iteration (directory deleted, symlink cycle); the entries already yielded are kept
    /// so the scan degrades instead of aborting the analysis.
    /// </summary>
    /// <param name="enumerate">The enumeration to materialize.</param>
    /// <returns>The entries yielded before any failure.</returns>
    private static List<string> EnumerateSafely(Func<IEnumerable<string>> enumerate)
    {
        var results = new List<string>();
        try
        {
            results.AddRange(enumerate());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Partial results collected so far are kept.
        }

        return results;
    }
}
