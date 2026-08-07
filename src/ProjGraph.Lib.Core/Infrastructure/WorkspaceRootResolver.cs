namespace ProjGraph.Lib.Core.Infrastructure;

/// <summary>
/// Provides shared logic for discovering workspace and solution root directories.
/// </summary>
public static class WorkspaceRootResolver
{
    /// <summary>
    /// Traverses up from the start directory looking for workspace markers
    /// (.sln, .slnx, .csproj, .git). Returns null if no workspace root is found.
    /// Stops traversal if the path is under the system temp directory.
    /// </summary>
    /// <param name="startDirectory">The directory path from which to begin searching upward.</param>
    /// <returns>The full path to the workspace root directory if found; otherwise, null.</returns>
    public static string? FindWorkspaceRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        var tempPath = GetNormalizedTempPath();

        while (current != null)
        {
            if (IsWorkspaceRoot(current))
            {
                return current.FullName;
            }

            if (IsTempPath(current, tempPath))
            {
                break;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Traverses up from the start directory a maximum of <paramref name="maxLevels"/> levels.
    /// Returns the highest reachable directory within those levels.
    /// Stops if the path is under the system temp directory.
    /// </summary>
    /// <param name="startDirectory">The directory path from which to begin traversing upward.</param>
    /// <param name="maxLevels">The maximum number of directory levels to traverse upward.</param>
    /// <returns>A <see cref="DirectoryInfo"/> representing the solution root directory.</returns>
    public static DirectoryInfo FindSolutionRoot(string startDirectory, int maxLevels)
    {
        var solutionRoot = new DirectoryInfo(startDirectory);
        var tempPath = GetNormalizedTempPath();

        if (IsTempPath(solutionRoot, tempPath))
        {
            return solutionRoot;
        }

        for (var i = 0; i < maxLevels && solutionRoot.Parent != null; i++)
        {
            solutionRoot = solutionRoot.Parent;
        }

        return solutionRoot;
    }

    /// <summary>
    /// Traverses up from the start directory looking for the enclosing *solution* root — a directory
    /// holding a .sln/.slnx file or a .git directory. Unlike <see cref="FindWorkspaceRoot"/> this ignores
    /// .csproj markers, which would stop the walk inside the starting project and never reach the sibling
    /// projects of a layered solution. Never walks above the system temp directory, so an isolated tree
    /// created under temp is bounded by its own marker rather than by the shared temp root.
    /// </summary>
    /// <param name="startDirectory">The directory path from which to begin searching upward.</param>
    /// <param name="maxLevels">The maximum number of directory levels to traverse upward.</param>
    /// <returns>The full path to the enclosing solution root if one is found; otherwise, null.</returns>
    public static string? FindEnclosingSolutionRoot(string startDirectory, int maxLevels)
    {
        var current = new DirectoryInfo(startDirectory);
        var tempPath = GetNormalizedTempPath();

        for (var level = 0; current != null && level <= maxLevels; level++)
        {
            if (IsSolutionRoot(current))
            {
                return current.FullName;
            }

            // Stop *at* the temp root rather than anywhere beneath it: an isolated tree created under temp
            // still gets to be bounded by its own marker, but the shared temp directory is never scanned.
            if (IsSameDirectory(current, tempPath))
            {
                break;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Determines whether the specified directory is exactly the given path (ignoring trailing separators).
    /// </summary>
    /// <param name="directory">The directory to check.</param>
    /// <param name="path">The normalized path to compare against.</param>
    /// <returns>True if the directory is that same directory; otherwise, false.</returns>
    private static bool IsSameDirectory(DirectoryInfo directory, string path)
    {
        return directory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Equals(path, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the specified directory holds a solution-level marker (.sln, .slnx or .git).
    /// </summary>
    /// <param name="directory">The directory to check.</param>
    /// <returns>True if the directory contains a solution-level marker; otherwise, false.</returns>
    private static bool IsSolutionRoot(DirectoryInfo directory)
    {
        return directory.GetFiles("*.sln").Length > 0 ||
               directory.GetFiles("*.slnx").Length > 0 ||
               directory.GetDirectories(DirectoryFilters.Git).Length > 0;
    }

    /// <summary>
    /// Determines whether the specified directory is a workspace root by checking for
    /// workspace marker files (.sln, .slnx, .csproj) or directories (.git).
    /// </summary>
    /// <param name="directory">The directory to check.</param>
    /// <returns>True if the directory contains workspace markers; otherwise, false.</returns>
    private static bool IsWorkspaceRoot(DirectoryInfo directory)
    {
        return directory.GetFiles("*.sln").Length > 0 ||
               directory.GetFiles("*.slnx").Length > 0 ||
               directory.GetFiles("*.csproj").Length > 0 ||
               directory.GetDirectories(DirectoryFilters.Git).Length > 0;
    }

    /// <summary>
    /// Gets the system temporary directory path with trailing directory separators removed.
    /// </summary>
    /// <returns>The normalized temporary directory path.</returns>
    private static string GetNormalizedTempPath()
    {
        return Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Determines whether the specified directory is under the system temporary directory path.
    /// </summary>
    /// <param name="directory">The directory to check.</param>
    /// <param name="tempPath">The normalized temporary directory path.</param>
    /// <returns>True if the directory is under the temp path; otherwise, false.</returns>
    private static bool IsTempPath(DirectoryInfo directory, string tempPath)
    {
        var normalized = directory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Equal to, or a real subdirectory of, the temp path. A bare prefix check would wrongly
        // treat a sibling like "/tmpfoo" as being under "/tmp".
        return normalized.Equals(tempPath, StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(tempPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
