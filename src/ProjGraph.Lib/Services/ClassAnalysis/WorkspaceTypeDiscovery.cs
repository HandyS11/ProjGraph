using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ProjGraph.Lib.Services.ClassAnalysis;

/// <summary>
/// Provides methods for discovering type definitions within a workspace.
/// This static class includes functionality to locate files containing specific type definitions
/// by searching directories and analyzing C# source files using Roslyn.
/// </summary>
public static class WorkspaceTypeDiscovery
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
    public static async Task<string?> FindTypeDefinitionFileAsync(string typeName, string startDirectory)
    {
        var root = FindWorkspaceRoot(startDirectory) ?? startDirectory;

        // Common file patterns to search first (optimistic)
        var commonDirs = new[] { "Models", "Entities", "Services", "Interfaces", "Common", "Data", "Internal" };
        foreach (var dirName in commonDirs)
        {
            var path = Path.Combine(root, dirName);
            if (!Directory.Exists(path))
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
    private static async Task<string?> SearchDirectoryForTypeAsync(string directory, string typeName)
    {
        return await SearchDirectoryRecursiveAsync(directory, typeName);
    }

    /// <summary>
    /// Recursively searches a directory and its subdirectories for a C# file containing a specific type definition.
    /// This method manually handles recursion to avoid descending into common non-source directories for better performance.
    /// </summary>
    /// <param name="directory">The path of the directory to search.</param>
    /// <param name="typeName">The name of the type to search for.</param>
    /// <returns>
    /// The full path of the file containing the type definition if found; otherwise, null.
    /// </returns>
    private static async Task<string?> SearchDirectoryRecursiveAsync(string directory, string typeName)
    {
        // Directories to skip during recursion for better performance
        var excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".git", "node_modules" };

        // Search files in the current directory
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", new EnumerationOptions { IgnoreInaccessible = true }))
        {
            // Simple string check first for performance
            var content = await File.ReadAllTextAsync(file);
            if (!content.Contains($"class {typeName}") &&
                !content.Contains($"interface {typeName}") &&
                !content.Contains($"struct {typeName}") &&
                !content.Contains($"enum {typeName}") &&
                !content.Contains($"record {typeName}"))
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
                return file;
            }
        }

        // Recursively search subdirectories, skipping excluded directories
        foreach (var subDir in Directory.EnumerateDirectories(directory, "*", new EnumerationOptions { IgnoreInaccessible = true }))
        {
            var dirName = Path.GetFileName(subDir);
            if (excludedDirs.Contains(dirName))
            {
                continue;
            }

            var result = await SearchDirectoryRecursiveAsync(subDir, typeName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the root directory of a workspace by traversing up the directory tree
    /// starting from the specified directory. The root is identified by the presence
    /// of certain files or directories, such as solution files (*.sln, *.slnx),
    /// project files (*.csproj), or a .git directory.
    /// </summary>
    /// <param name="startDir">The starting directory to begin the search.</param>
    /// <returns>
    /// The full path of the workspace root directory if found; otherwise, null.
    /// </returns>
    private static string? FindWorkspaceRoot(string startDir)
    {
        var current = new DirectoryInfo(startDir);
        var tempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        while (current != null)
        {
            if (current.GetFiles("*.sln").Length > 0 ||
                current.GetFiles("*.slnx").Length > 0 ||
                current.GetFiles("*.csproj").Length > 0 ||
                current.GetDirectories(".git").Length > 0)
            {
                return current.FullName;
            }

            // Don't traverse above the temp directory if we're in it
            if (current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(tempPath, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = current.Parent;
        }

        return null;
    }
}