using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Reflection;

namespace ProjGraph.Mcp;

internal sealed class WorkspaceRootService : IDisposable
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

        var matches = _rootPaths.Select(rootPath => FindFileRecursively(rootPath, path)).OfType<string>().ToList();

        return matches.Count switch
        {
            0 => throw new FileNotFoundException(
                $"File '{path}' not found under any workspace root"),
            1 => matches[0],
            _ => throw new AmbiguousMatchException(
                $"'{path}' matches multiple roots: {string.Join(", ", matches)}. Provide an absolute path.")
        };
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

    private static string? FindFileRecursively(string rootPath, string fileName)
    {
        try
        {
            var files = Directory.GetFiles(rootPath, fileName, SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _initLock.Dispose();
    }
}
