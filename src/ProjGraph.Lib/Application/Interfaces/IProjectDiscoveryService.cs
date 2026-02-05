namespace ProjGraph.Lib.Application.Interfaces;

/// <summary>
/// Service responsible for discovering projects and resolving paths within a solution.
/// </summary>
public interface IProjectDiscoveryService
{
    /// <summary>
    /// Discovers all project file paths recursively starting from the specified root project path.
    /// </summary>
    /// <param name="rootProjectPath">The full path to the root project file to start the discovery from.</param>
    /// <returns>A collection of full paths of all discovered project files.</returns>
    IEnumerable<string> DiscoverProjectsRecursively(string rootProjectPath);

    /// <summary>
    /// Normalizes a path to ensure consistent comparison across platforms.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path.</returns>
    string NormalizePath(string path);

    /// <summary>
    /// Resolves a project reference path relative to a project file path.
    /// </summary>
    /// <param name="projectPath">The full path to the project file.</param>
    /// <param name="referencePath">The relative path to the referenced project.</param>
    /// <returns>The absolute path to the referenced project.</returns>
    string ResolveProjectReferencePath(string projectPath, string referencePath);
}