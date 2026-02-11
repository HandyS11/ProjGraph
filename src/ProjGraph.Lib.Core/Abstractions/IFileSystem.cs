namespace ProjGraph.Lib.Core.Abstractions;

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

    /// <summary>
    /// Asynchronously reads all text from a file at the specified path.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The content of the file as a string.</returns>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a directory exists at the specified path.
    /// </summary>
    /// <param name="path">The path to the directory.</param>
    /// <returns>True if the directory exists, otherwise false.</returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Enumerates files matching a search pattern in the specified directory.
    /// </summary>
    /// <param name="path">The directory to search.</param>
    /// <param name="searchPattern">The search pattern (e.g., "*.cs").</param>
    /// <param name="searchOption">Whether to search subdirectories.</param>
    /// <returns>An enumerable of file paths matching the pattern.</returns>
    IEnumerable<string> EnumerateFiles(string path, string searchPattern,
        SearchOption searchOption = SearchOption.TopDirectoryOnly);

    /// <summary>
    /// Enumerates subdirectories in the specified directory.
    /// </summary>
    /// <param name="path">The directory to search.</param>
    /// <returns>An enumerable of subdirectory paths.</returns>
    IEnumerable<string> EnumerateDirectories(string path);

    /// <summary>
    /// Gets the current working directory of the application.
    /// </summary>
    /// <returns>The current directory path.</returns>
    string GetCurrentDirectory();

    /// <summary>
    /// Gets the parent directory of the specified path.
    /// </summary>
    /// <param name="path">The path for which to find the parent.</param>
    /// <returns>The parent directory path, or null if no parent exists.</returns>
    string? GetParentDirectory(string path);
}
