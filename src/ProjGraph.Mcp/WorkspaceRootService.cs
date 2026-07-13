using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using System.Reflection;

namespace ProjGraph.Mcp;

internal sealed class WorkspaceRootService(IFileSystem fileSystem) : IDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile RootsStatusKind _status = RootsStatusKind.Unknown;
    private List<string> _rootPaths = [];

    private enum RootsStatusKind
    {
        Unknown = 0,
        Unsupported = 1,
        Ready = 2
    }

    public async Task<string> TryResolveAsync(string path, McpServer server, CancellationToken ct)
    {
        if (Path.IsPathFullyQualified(path))
        {
            return path;
        }

        await EnsureInitializedAsync(server, ct);

        if (_status == RootsStatusKind.Unsupported)
        {
            throw new InvalidOperationException(
                "Client does not support workspace roots. Please provide an absolute path.");
        }

        var matches = ResolveMatches(_rootPaths, path);

        return matches.Count switch
        {
            0 => throw new FileNotFoundException(
                $"'{path}' not found under any workspace root"),
            1 => matches[0],
            _ => throw new AmbiguousMatchException(
                $"'{path}' matches multiple roots: {string.Join(", ", matches)}. Provide an absolute path.")
        };
    }

    /// <summary>
    /// Resolves a relative path against the given workspace roots, matching either a file or a
    /// directory. The direct combined path (root + relative) is tried first — resolving paths with
    /// subdirectories and directory paths — guarded against escaping the root with "..". A bare
    /// name falls back to a recursive search for a matching file or directory.
    /// </summary>
    /// <param name="rootPaths">The workspace root directories.</param>
    /// <param name="path">The relative path to resolve. Must not contain wildcard characters.</param>
    /// <returns>The distinct set of matching absolute paths across all roots.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> contains a wildcard.</exception>
    internal List<string> ResolveMatches(IEnumerable<string> rootPaths, string path)
    {
        // The input is a path, not a glob: reject wildcards so it cannot match an arbitrary file
        // (e.g. "*.slnx") under a root.
        if (path.IndexOfAny(['*', '?']) >= 0)
        {
            throw new ArgumentException(
                $"Path '{path}' must not contain wildcard characters. Provide a specific relative path.",
                nameof(path));
        }

        return rootPaths
            .Select(rootPath => ResolveWithinRoot(rootPath, path))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private string? ResolveWithinRoot(string rootPath, string relativePath)
    {
        var normalizedRoot = fileSystem.GetFullPath(rootPath);

        // Prefer the direct combined path: resolves relative paths with subdirectories and
        // directory paths, while the traversal guard prevents escaping the root with "..".
        var combined = fileSystem.GetFullPath(fileSystem.Combine(normalizedRoot, relativePath));
        if (IsWithinRoot(combined, normalizedRoot) &&
            (fileSystem.FileExists(combined) || fileSystem.DirectoryExists(combined)))
        {
            return combined;
        }

        // A path with directory separators must resolve via the direct combine above; only a bare
        // file or directory name is searched for recursively.
        if (relativePath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            return null;
        }

        return FindByNameRecursively(normalizedRoot, relativePath);
    }

    private static bool IsWithinRoot(string candidate, string root)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    internal async Task RefreshRootsAsync(McpServer server, CancellationToken ct)
    {
        var result = await server.RequestRootsAsync(new ListRootsRequestParams(), ct);
        var paths = new List<string>();
        foreach (var root in result.Roots)
        {
            paths.Add(new Uri(root.Uri).LocalPath);
        }

        _rootPaths = paths;
        _status = RootsStatusKind.Ready;
    }

    private async Task EnsureInitializedAsync(McpServer server, CancellationToken ct)
    {
        if (_status != RootsStatusKind.Unknown)
        {
            return;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            // Double-checked locking: re-check after acquiring lock
#pragma warning disable CA1508 // Avoid dead conditional code — volatile field may change between outer check and lock acquisition
            if (_status != RootsStatusKind.Unknown)
            {
                return;
            }
#pragma warning restore CA1508

            if (server.ClientCapabilities?.Roots is null)
            {
                _status = RootsStatusKind.Unsupported;
                return;
            }

            await RefreshRootsAsync(server, ct);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private string? FindByNameRecursively(string rootPath, string name)
    {
        var queue = new Queue<string>();
        queue.Enqueue(rootPath);

        while (queue.Count > 0)
        {
            var currentDir = queue.Dequeue();

            if (DirectoryFilters.ShouldSkipDirectory(currentDir))
            {
                continue;
            }

            try
            {
                // 'name' is a literal (wildcards were rejected up front), so this is an exact match.
                var fileMatch = fileSystem.GetFiles(currentDir, name).FirstOrDefault();
                if (fileMatch is not null)
                {
                    return fileMatch;
                }

                foreach (var subDir in fileSystem.GetDirectories(currentDir))
                {
                    if (string.Equals(Path.GetFileName(subDir), name, StringComparison.Ordinal))
                    {
                        return subDir;
                    }

                    queue.Enqueue(subDir);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
            {
                // Skip directories we cannot access
            }
        }

        return null;
    }

    public void Dispose()
    {
        _initLock.Dispose();
    }
}
