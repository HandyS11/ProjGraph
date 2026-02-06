using ProjGraph.Lib.Core.Abstractions;
using System.Diagnostics;

namespace ProjGraph.Lib.Core.Infrastructure;

/// <summary>
/// Infrastructure implementation of project discovery and path resolution.
/// </summary>
public class ProjectDiscoveryService(IProjectParser projectParser, IFileSystem fileSystem) : IProjectDiscoveryService
{
    /// <summary>
    /// Discovers all project files recursively starting from the specified root project path.
    /// </summary>
    /// <param name="rootProjectPath">The root directory path to start the project discovery from.</param>
    /// <returns>A collection of strings representing the full paths of discovered project files.</returns>
    public IEnumerable<string> DiscoverProjectsRecursively(string rootProjectPath)
    {
        var discoveredNormalized = new HashSet<string>(new PathEqualityComparer());
        var discoveredFullPaths = new HashSet<string>();
        var toProcess = new Queue<string>();

        var rootFullPath = fileSystem.GetFullPath(rootProjectPath);
        var rootNormalizedPath = NormalizePath(rootFullPath);
        toProcess.Enqueue(rootFullPath);
        discoveredNormalized.Add(rootNormalizedPath);
        discoveredFullPaths.Add(rootFullPath);

        while (toProcess.Count > 0)
        {
            var currentFullPath = toProcess.Dequeue();

            if (!fileSystem.FileExists(currentFullPath))
            {
                continue;
            }

            try
            {
                var (_, refs) = projectParser.Parse(currentFullPath);

                foreach (var refPath in refs)
                {
                    var absoluteRefPath = ResolveProjectReferencePath(currentFullPath, refPath);
                    var normalizedRefPath = NormalizePath(absoluteRefPath);

                    if (!discoveredNormalized.Add(normalizedRefPath))
                    {
                        continue;
                    }

                    discoveredFullPaths.Add(absoluteRefPath);
                    toProcess.Enqueue(absoluteRefPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to parse project {currentFullPath}: {ex.Message}");
            }
        }

        return discoveredFullPaths;
    }

    /// <summary>
    /// Normalizes the given file path by replacing backslashes with forward slashes.
    /// </summary>
    /// <param name="path">The file path to normalize.</param>
    /// <returns>The normalized file path with forward slashes.</returns>
    public string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// Resolves the full path of a project reference based on the project path and the reference path.
    /// </summary>
    /// <param name="projectPath">The full path of the project file.</param>
    /// <param name="referencePath">The relative path of the project reference.</param>
    /// <returns>The fully resolved absolute path of the project reference.</returns>
    public string ResolveProjectReferencePath(string projectPath, string referencePath)
    {
        var projectDir = fileSystem.GetDirectoryName(projectPath) ?? string.Empty;
        var normalizedReferencePath = referencePath.Replace('\\', Path.DirectorySeparatorChar);
        var combinedPath = fileSystem.Combine(projectDir, normalizedReferencePath);
        return fileSystem.GetFullPath(combinedPath);
    }

    /// <summary>
    /// A custom equality comparer for file paths that normalizes path separators
    /// and performs a case-insensitive comparison.
    /// </summary>
    private sealed class PathEqualityComparer : IEqualityComparer<string>
    {
        /// <summary>
        /// Determines whether two file paths are equal by normalizing their path separators
        /// and performing a case-insensitive comparison.
        /// </summary>
        /// <param name="x">The first file path to compare.</param>
        /// <param name="y">The second file path to compare.</param>
        /// <returns>
        /// <c>true</c> if the specified file paths are equal; otherwise, <c>false</c>.
        /// </returns>
        public bool Equals(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.Replace('\\', '/'), y.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns a hash code for a file system path using case-insensitive, normalized comparison.
        /// </summary>
        /// <param name="obj">The path for which to get a hash code.</param>
        /// <returns>A hash code based on the normalized, lowercase representation of the path.</returns>
        public int GetHashCode(string obj)
        {
            return obj.Replace('\\', '/').ToLowerInvariant().GetHashCode();
        }
    }
}