namespace ProjGraph.Lib.Application.Interfaces;

/// <summary>
/// Provides an abstraction for file system operations to improve testability.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string ReadAllText(string path);
    string[] GetFiles(string path, string searchPattern, bool recursive = false);
    string GetFullPath(string path);
    string? GetDirectoryName(string path);
    string Combine(params string[] paths);
}
