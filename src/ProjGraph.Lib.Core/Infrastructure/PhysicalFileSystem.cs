using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Lib.Core.Infrastructure;

/// <summary>
/// Physical file system implementation of IFileSystem.
/// </summary>
public class PhysicalFileSystem : IFileSystem
{
    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    /// <summary>
    /// Reads all text from the specified file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>The content of the file as a string.</returns>
    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Gets the absolute path for the specified path string.
    /// </summary>
    /// <param name="path">The relative or absolute path to evaluate.</param>
    /// <returns>The absolute path.</returns>
    public string GetFullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Gets the directory information for the specified path string.
    /// </summary>
    /// <param name="path">The path of a file or directory.</param>
    /// <returns>The directory information, or null if the path does not contain directory information.</returns>
    public string? GetDirectoryName(string path)
    {
        return Path.GetDirectoryName(path);
    }

    /// <summary>
    /// Returns the extension (including the period ".") of the specified path string.
    /// </summary>
    /// <param name="path">The path string from which to get the extension.</param>
    /// <returns>The extension of the specified path (including the period "."), or an empty string if no extension is present.</returns>
    public string GetExtension(string path)
    {
        return Path.GetExtension(path);
    }

    /// <summary>
    /// Combines multiple path strings into a single path.
    /// </summary>
    /// <param name="paths">An array of parts of the path to combine.</param>
    /// <returns>The combined path.</returns>
    public string Combine(params string[] paths)
    {
        return Path.Combine(paths);
    }

    /// <summary>
    /// Asynchronously reads all text from the specified file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains the content of the file as a string.</returns>
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        return File.ReadAllTextAsync(path, cancellationToken);
    }

    /// <summary>
    /// Asynchronously writes all text to a file at the specified path.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="contents">The string content to write.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
    {
        // File.WriteAllTextAsync defaults to UTF-8 without BOM in .NET
        return File.WriteAllTextAsync(path, contents, cancellationToken);
    }

    /// <summary>
    /// Creates all directories in the specified path unless they already exist.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    /// <summary>
    /// Checks if a directory exists at the specified path.
    /// </summary>
    /// <param name="path">The path to the directory.</param>
    /// <returns>True if the directory exists, otherwise false.</returns>
    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    /// <summary>
    /// Gets the names of subdirectories (including their paths) in the specified directory.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>An array of full names of subdirectories.</returns>
    public string[] GetDirectories(string path)
    {
        return Directory.GetDirectories(path);
    }

    /// <summary>
    /// Returns the names of files (including their paths) that match the specified search pattern in the specified directory.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <param name="searchPattern">The search string to match against the names of files.</param>
    /// <returns>An array of the full names of files that match the search pattern.</returns>
    public string[] GetFiles(string path, string searchPattern)
    {
        return Directory.GetFiles(path, searchPattern);
    }

    /// <summary>
    /// Returns an enumerable collection of full file names that match a search pattern in a specified path,
    /// using the specified enumeration options.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <param name="searchPattern">The search string to match against the names of files.</param>
    /// <param name="options">The enumeration options to use.</param>
    /// <returns>An enumerable collection of the full names of files that match the search pattern.</returns>
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions options)
    {
        return Directory.EnumerateFiles(path, searchPattern, options);
    }

    /// <summary>
    /// Returns an enumerable collection of directory full names that match a search pattern in a specified path,
    /// using the specified enumeration options.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <param name="searchPattern">The search string to match against the names of directories.</param>
    /// <param name="options">The enumeration options to use.</param>
    /// <returns>An enumerable collection of the full names of directories that match the search pattern.</returns>
    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions options)
    {
        return Directory.EnumerateDirectories(path, searchPattern, options);
    }

    /// <summary>
    /// Gets the current working directory of the application.
    /// </summary>
    /// <returns>The current working directory path.</returns>
    public string GetCurrentDirectory()
    {
        return Directory.GetCurrentDirectory();
    }
}
