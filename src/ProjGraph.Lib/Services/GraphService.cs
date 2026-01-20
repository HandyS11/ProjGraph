using ProjGraph.Core.Models;
using ProjGraph.Lib.Interfaces;
using ProjGraph.Lib.Parsers;
using System.Diagnostics;

namespace ProjGraph.Lib.Services;

/// <summary>
/// Service responsible for building a solution graph from a given file path.
/// </summary>
public class GraphService : IGraphService
{
    /// <summary>
    /// Builds a solution graph by analyzing the specified file path.
    /// The file can be a solution file (.sln or .slnx) or a project file (.csproj).
    /// </summary>
    /// <param name="path">The path to the solution or project file.</param>
    /// <returns>A <see cref="SolutionGraph"/> object representing the projects and their dependencies.</returns>
    /// <exception cref="ArgumentException">Thrown when the file type is not supported.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    public SolutionGraph BuildGraph(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The specified file does not exist: {path}", path);
        }

        IEnumerable<string> projectFilePaths;

        if (path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            projectFilePaths = SlnxParser.GetProjectPaths(path);
        }
        else if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            projectFilePaths = SlnParser.GetProjectPaths(path);
        }
        else if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            projectFilePaths = DiscoverProjectsRecursively(path);
        }
        else
        {
            throw new ArgumentException("Unsupported file type: must be .sln, .slnx, or .csproj", nameof(path));
        }

        var projects = new List<Project>();
        var dependencies = new List<Dependency>();
        var pathToProject = new Dictionary<string, Project>(new PathEqualityComparer());
        var rawDependencies = new List<(string sourcePath, string targetPath)>();

        foreach (var projectPath in projectFilePaths)
        {
            var normalizedPath = NormalizePath(Path.GetFullPath(projectPath));

            if (!File.Exists(normalizedPath))
            {
                continue;
            }

            try
            {
                var (project, refs) = ProjectParser.Parse(normalizedPath);
                projects.Add(project);
                pathToProject[normalizedPath] = project; // Store by normalized path for lookup

                rawDependencies.AddRange(refs.Select(refPath => ResolveProjectReferencePath(normalizedPath, refPath))
                    .Select(absoluteRefPath => (normalizedPath, absoluteRefPath)));
            }
            catch
            {
                // For now, silently skip projects that fail to analyze
            }
        }

        foreach (var (sourcePath, targetPath) in rawDependencies)
        {
            if (pathToProject.TryGetValue(sourcePath, out var source) &&
                pathToProject.TryGetValue(targetPath, out var target))
            {
                dependencies.Add(new Dependency(source.Id, target.Id, DependencyType.ProjectReference));
            }
        }

        return new SolutionGraph(
            Path.GetFileName(path),
            path,
            projects,
            dependencies
        );
    }

    /// <summary>
    /// Discovers all project file paths recursively starting from the specified root project path.
    /// </summary>
    /// <param name="rootProjectPath">The full path to the root project file to start the discovery from.</param>
    /// <returns>A <see cref="HashSet{T}"/> containing the full paths of all discovered project files.</returns>
    /// <remarks>
    /// This method parses the root project file to find its references and recursively discovers all referenced projects.
    /// It ensures that each project is only processed once by maintaining a set of discovered project paths.
    /// If a project fails to parse, it is skipped, and the error is logged for debugging purposes.
    /// </remarks>
    private static HashSet<string> DiscoverProjectsRecursively(string rootProjectPath)
    {
        var discovered = new HashSet<string>(new PathEqualityComparer());
        var toProcess = new Queue<string>();

        var rootFullPath = NormalizePath(Path.GetFullPath(rootProjectPath));
        toProcess.Enqueue(rootFullPath);
        discovered.Add(rootFullPath);

        while (toProcess.Count > 0)
        {
            var currentPath = toProcess.Dequeue();

            if (!File.Exists(currentPath))
            {
                continue;
            }

            try
            {
                var (_, refs) = ProjectParser.Parse(currentPath);

                foreach (var refPath in refs)
                {
                    var absoluteRefPath = ResolveProjectReferencePath(currentPath, refPath);

                    if (discovered.Add(absoluteRefPath))
                    {
                        toProcess.Enqueue(absoluteRefPath);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but continue - don't let one bad project stop the whole analysis
                Debug.WriteLine($"Failed to parse project {currentPath}: {ex.Message}");
            }
        }

        return discovered;
    }

    /// <summary>
    /// Normalizes a path to ensure consistent comparison across platforms.
    /// Replaces backslashes with forward slashes for consistency.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path.</returns>
    private static string NormalizePath(string path)
    {
        // Replace backslashes with forward slashes for consistency across platforms
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// Resolves a project reference path relative to a project file path.
    /// </summary>
    /// <param name="projectPath">The full path to the project file.</param>
    /// <param name="referencePath">The relative path to the referenced project.</param>
    /// <returns>The normalized absolute path to the referenced project.</returns>
    private static string ResolveProjectReferencePath(string projectPath, string referencePath)
    {
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var combinedPath = Path.Combine(projectDir, referencePath);
        var fullPath = Path.GetFullPath(combinedPath);
        return NormalizePath(fullPath);
    }

    /// <summary>
    /// Equality comparer for file paths that handles cross-platform path comparison.
    /// Normalizes paths to use forward slashes and applies case-insensitive comparison on Windows.
    /// </summary>
    private sealed class PathEqualityComparer : IEqualityComparer<string>
    {
        /// <summary>
        /// Determines whether two file paths are equal, taking into account platform-specific
        /// case sensitivity and ensuring paths are normalized for comparison.
        /// </summary>
        /// <param name="x">The first file path to compare.</param>
        /// <param name="y">The second file path to compare.</param>
        /// <returns>
        /// True if the specified file paths are considered equal; otherwise, false.
        /// </returns>
        public bool Equals(string? x, string? y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            var normalizedX = NormalizePath(x);
            var normalizedY = NormalizePath(y);

            return string.Equals(normalizedX, normalizedY,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        /// <summary>
        /// Computes the hash code for a given file path, ensuring that the path is normalized
        /// and taking into account platform-specific case sensitivity.
        /// </summary>
        /// <param name="obj">The file path for which to compute the hash code.</param>
        /// <returns>
        /// An integer hash code for the specified file path. On Windows, the hash code is
        /// computed in a case-insensitive manner, while on other platforms it is case-sensitive.
        /// </returns>
        public int GetHashCode(string obj)
        {
            var normalized = NormalizePath(obj);
            return OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase.GetHashCode(normalized)
                : StringComparer.Ordinal.GetHashCode(normalized);
        }
    }
}