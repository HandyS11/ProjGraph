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
    /// Gets the current working directory of the application.
    /// </summary>
    /// <returns>The current working directory path.</returns>
    public string GetCurrentDirectory()
    {
        return Directory.GetCurrentDirectory();
    }
}
