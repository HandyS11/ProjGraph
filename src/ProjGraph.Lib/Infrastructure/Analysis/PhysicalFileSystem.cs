using ProjGraph.Lib.Application.Interfaces;

namespace ProjGraph.Lib.Infrastructure.Analysis;

/// <summary>
/// Physical file system implementation of IFileSystem.
/// </summary>
public class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    public string[] GetFiles(string path, string searchPattern, bool recursive = false)
    {
        return Directory.GetFiles(path, searchPattern,
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    }

    public string GetFullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    public string? GetDirectoryName(string path)
    {
        return Path.GetDirectoryName(path);
    }

    public string Combine(params string[] paths)
    {
        return Path.Combine(paths);
    }
}
