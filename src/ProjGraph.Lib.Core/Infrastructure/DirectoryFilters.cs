using System.Collections.Frozen;

namespace ProjGraph.Lib.Core.Infrastructure;

/// <summary>
/// Provides shared directory exclusion lists used across the codebase.
/// </summary>
public static class DirectoryFilters
{
    /// <summary>
    /// Directories that should be excluded from recursive searches.
    /// </summary>
    private static FrozenSet<string> DefaultExcludedDirectories { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".git", "node_modules" }.ToFrozenSet(
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if the given directory name should be skipped during recursive searches.
    /// </summary>
    /// <param name="directoryPath">The full or relative path to the directory to check.</param>
    /// <returns>True if the directory should be excluded from recursive searches; otherwise, false.</returns>
    public static bool ShouldSkipDirectory(string directoryPath)
    {
        var dirName = Path.GetFileName(directoryPath);
        return DefaultExcludedDirectories.Contains(dirName);
    }
}
