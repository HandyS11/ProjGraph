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
    /// Returns the extension (including the period ".") of the specified path string.
    /// </summary>
    /// <param name="path">The path string from which to get the extension.</param>
    /// <returns>The extension of the specified path (including the period "."), or an empty string if no extension is present.</returns>
    string GetExtension(string path);

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
    /// Asynchronously writes all text to a file at the specified path.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="contents">The string content to write.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates all directories in the specified path unless they already exist.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    void CreateDirectory(string path);

    /// <summary>
    /// Checks if a directory exists at the specified path.
    /// </summary>
    /// <param name="path">The path to the directory.</param>
    /// <returns>True if the directory exists, otherwise false.</returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Gets the names of subdirectories (including their paths) in the specified directory.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>An array of full names of subdirectories.</returns>
    string[] GetDirectories(string path);

    /// <summary>
    /// Returns the names of files (including their paths) that match the specified search pattern in the specified directory.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <param name="searchPattern">The search string to match against the names of files.</param>
    /// <returns>An array of the full names of files that match the search pattern.</returns>
    string[] GetFiles(string path, string searchPattern);

    /// <summary>
    /// Returns an enumerable collection of full file names that match a search pattern in a specified path,
    /// using the specified enumeration options.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <param name="searchPattern">The search string to match against the names of files.</param>
    /// <param name="options">The enumeration options to use.</param>
    /// <returns>An enumerable collection of the full names of files that match the search pattern.</returns>
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions options);

    /// <summary>
    /// Returns an enumerable collection of directory full names that match a search pattern in a specified path,
    /// using the specified enumeration options.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <param name="searchPattern">The search string to match against the names of directories.</param>
    /// <param name="options">The enumeration options to use.</param>
    /// <returns>An enumerable collection of the full names of directories that match the search pattern.</returns>
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions options);

    /// <summary>
    /// Gets the current working directory of the application.
    /// </summary>
    /// <returns>The current directory path.</returns>
    string GetCurrentDirectory();
}
