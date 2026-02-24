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
        return directory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .StartsWith(tempPath, StringComparison.OrdinalIgnoreCase);
    }
}
