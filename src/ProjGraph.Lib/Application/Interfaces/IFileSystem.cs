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
    /// Checks if a directory exists at the specified path.
    /// </summary>
    /// <param name="path">The path to the directory.</param>
    /// <returns>True if the directory exists, otherwise false.</returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Reads all text from a file at the specified path.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>The content of the file as a string.</returns>
    string ReadAllText(string path);

    /// <summary>
    /// Retrieves the names of files in the specified directory that match the search pattern.
    /// </summary>
    /// <param name="path">The path to the directory.</param>
    /// <param name="searchPattern">The search string to match against the names of files in the directory.</param>
    /// <param name="recursive">Whether to search all subdirectories recursively. Default is false.</param>
    /// <returns>An array of file names that match the search pattern.</returns>
    string[] GetFiles(string path, string searchPattern, bool recursive = false);

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