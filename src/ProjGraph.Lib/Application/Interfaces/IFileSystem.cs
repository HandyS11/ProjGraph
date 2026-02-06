namespace ProjGraph.Lib.Application.Interfaces;

/// <summary>
/// Provides an abstraction for file system operations to improve testability.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>True if the file exists, otherwise false.</returns>
    bool FileExists(string path);

    /// <summary>
    /// Reads all text from a file at the specified path.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>The content of the file as a string.</returns>
    string ReadAllText(string path);

    /// <summary>
    /// Gets the absolute path for the specified path string.
    /// </summary>
    /// <param name="path">The relative or absolute path to evaluate.</param>
    /// <returns>The absolute path.</returns>
    string GetFullPath(string path);

    /// <summary>
    /// Gets the directory information for the specified path string.
    /// </summary>
    /// <param name="path">The path of a file or directory.</param>
    /// <returns>The directory information, or null if the path does not contain directory information.</returns>
    string? GetDirectoryName(string path);

    /// <summary>
    /// Combines multiple path strings into a single path.
    /// </summary>
    /// <param name="paths">An array of parts of the path to combine.</param>
    /// <returns>The combined path.</returns>
    string Combine(params string[] paths);
}