using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;

namespace ProjGraph.Mcp;

/// <summary>
/// Resolves relative paths supplied to the MCP tools against the client's workspace roots.
/// </summary>
/// <remarks>
/// The Roots feature is deprecated by specification version 2026-07-28 (SEP-2577), which is why the
/// SDK calls below are wrapped in <c>MCP9005</c> suppressions. It stays wire-supported for at least
/// twelve months — removing it needs a separate SEP — so it is still served here, on both the new
/// revision and down-level ones. A client that does not advertise the capability degrades to
/// <see cref="RootsStatusKind.Unsupported"/> and is told to pass an absolute path. Retiring it means
/// taking the workspace root as a tool parameter or as server configuration, which is a behavioural
/// change tracked separately from this SDK upgrade.
/// </remarks>
/// <remarks>
/// Every method takes the <b>request-scoped</b> <see cref="McpServer"/>. From 2026-07-28 there is no
/// <c>initialize</c> handshake: the client restates its capabilities per request in <c>_meta</c>, so
/// <see cref="McpServer.ClientCapabilities"/> is null on the root server and populated only on the
/// instance the SDK binds to a tool-method parameter.
/// </remarks>
/// <param name="fileSystem">The file system used to probe candidate paths under each root.</param>
internal sealed class WorkspaceRootService(IFileSystem fileSystem) : IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile RootsStatusKind _status = RootsStatusKind.Unknown;
    private List<string> _rootPaths = [];
    private bool _notificationHandlerRegistered;
    private IAsyncDisposable? _rootsChangedRegistration;

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

        // Resolved into a local: on the per-request revision the roots belong to this request only,
        // so an overlapping request must not be able to swap them out from under this one.
        //
        // Every failure from here on throws McpException: the SDK replaces the message of any other
        // exception type with a generic "An error occurred invoking '…'", so the guidance
        // (most importantly "provide an absolute path") would never reach the client.
        var roots = await ResolveRootsAsync(server, ct)
            ?? throw new McpException(
                "Client does not support workspace roots. Please provide an absolute path.");

        var matches = ResolveMatches(roots, path);

        return matches.Count switch
        {
            0 => throw new McpException(
                $"'{path}' not found under any workspace root. Provide an absolute path."),
            1 => matches[0],
            _ => throw new McpException(
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
    /// <exception cref="McpException">Thrown when <paramref name="path"/> contains a wildcard.</exception>
    internal List<string> ResolveMatches(IEnumerable<string> rootPaths, string path)
    {
        // The input is a path, not a glob: reject wildcards so it cannot match an arbitrary file
        // (e.g. "*.slnx") under a root. McpException so the guidance reaches the client.
        if (path.IndexOfAny(['*', '?']) >= 0)
        {
            throw new McpException(
                $"Path '{path}' must not contain wildcard characters. Provide a specific relative path.");
        }

        return
        [
            .. rootPaths
                .Select(rootPath => ResolveWithinRoot(rootPath, path))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
        ];
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

        // A rooted result means the candidate is on a different volume — outside the root.
        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        // Reject only a genuine parent-directory segment ("..", "../", "..\"), not a legitimate
        // in-root name that merely starts with ".." (e.g. "..data").
        return relative != ".."
               && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
               && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>
    /// Invalidates the cached roots so the next resolution re-fetches them. Called when the client
    /// sends a <c>notifications/roots/list_changed</c> notification.
    /// </summary>
    internal void InvalidateRoots()
    {
        _status = RootsStatusKind.Unknown;
    }

    /// <summary>
    /// Registers, once, a handler for the client's <c>roots/list_changed</c> notification so a
    /// mid-session change to the workspace roots invalidates the cached set.
    /// </summary>
    /// <param name="server">The MCP server used to register the notification handler.</param>
    private void EnsureRootsChangedHandler(McpServer server)
    {
        if (_notificationHandlerRegistered)
        {
            return;
        }

        // Only mark as registered after a successful call, so a failed registration can be retried
        // on the next initialization instead of permanently disabling roots invalidation.
#pragma warning disable MCP9005 // Roots is deprecated (SEP-2577); still served for down-level clients. See the file header.
        _rootsChangedRegistration = server.RegisterNotificationHandler(
            NotificationMethods.RootsListChangedNotification,
            (_, _) =>
            {
                InvalidateRoots();
                return default;
            });
#pragma warning restore MCP9005
        _notificationHandlerRegistered = true;
    }

    /// <summary>
    /// Fetches the client's workspace roots over <c>roots/list</c>.
    /// </summary>
    /// <param name="server">The request-scoped server handling the current request.</param>
    /// <param name="ct">A token to cancel the request.</param>
    /// <returns>
    /// The root directories, or <see langword="null"/> when the client refuses the request.
    /// </returns>
    private static async Task<List<string>?> TryFetchRootsAsync(McpServer server, CancellationToken ct)
    {
        try
        {
#pragma warning disable MCP9005 // Roots is deprecated (SEP-2577); still served for down-level clients. See the file header.
            var result = await server.RequestRootsAsync(new ListRootsRequestParams(), ct);
#pragma warning restore MCP9005
            var paths = new List<string>();
            foreach (var root in result.Roots)
            {
                paths.Add(new Uri(root.Uri).LocalPath);
            }

            return paths;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A client can advertise the capability and still refuse the request — most likely once
            // it drops the deprecated feature. Reporting it as unsupported gets the caller the
            // actionable "provide an absolute path" guidance instead of a generic SDK error.
            return null;
        }
    }

    private static bool HasRootsCapability(McpServer server)
    {
#pragma warning disable MCP9005 // Roots is deprecated (SEP-2577); still served for down-level clients. See the file header.
        return server.ClientCapabilities?.Roots is not null;
#pragma warning restore MCP9005
    }

    /// <summary>
    /// Indicates whether the connection established client state once, via the <c>initialize</c>
    /// handshake (protocol revision <c>2025-11-25</c> and earlier). Only such a connection has a
    /// session for roots to be cached against and a durable channel for the client's
    /// <c>roots/list_changed</c> notification to invalidate that cache; from <c>2026-07-28</c> the
    /// client restates its capabilities on every request instead, so the roots are re-fetched each
    /// time rather than served stale for the rest of the process's life.
    /// </summary>
    /// <param name="server">The request-scoped server handling the current request.</param>
    /// <returns><see langword="true"/> when client state is session-scoped.</returns>
    private static bool UsesSessionScopedCapabilities(McpServer server)
    {
        // Date-based revisions order correctly under an ordinal comparison.
        var version = server.NegotiatedProtocolVersion;
        return version is not null && string.CompareOrdinal(version, "2026-07-28") < 0;
    }

    /// <summary>
    /// Produces the workspace roots the current request must resolve against.
    /// </summary>
    /// <param name="server">The request-scoped server handling the current request.</param>
    /// <param name="ct">A token to cancel the request.</param>
    /// <returns>
    /// The root directories, or <see langword="null"/> when the client offers none.
    /// </returns>
    private async Task<IReadOnlyList<string>?> ResolveRootsAsync(McpServer server, CancellationToken ct)
    {
        if (!UsesSessionScopedCapabilities(server))
        {
            // The per-request revision keeps nothing: the roots are scoped to this request, so
            // publishing them to the shared cache would let an overlapping request resolve against
            // the wrong workspace, and there is no session for roots/list_changed to invalidate.
            return HasRootsCapability(server) ? await TryFetchRootsAsync(server, ct) : null;
        }

        if (_status == RootsStatusKind.Ready)
        {
            return _rootPaths;
        }

        if (!HasRootsCapability(server))
        {
            _status = RootsStatusKind.Unsupported;
            return null;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            // Double-checked locking: re-check after acquiring lock
#pragma warning disable CA1508 // Avoid dead conditional code — volatile field may change between outer check and lock acquisition
            if (_status == RootsStatusKind.Ready)
            {
                return _rootPaths;
            }
#pragma warning restore CA1508

            EnsureRootsChangedHandler(server);

            var paths = await TryFetchRootsAsync(server, ct);
            if (paths is null)
            {
                _status = RootsStatusKind.Unsupported;
                return null;
            }

            _rootPaths = paths;
            _status = RootsStatusKind.Ready;
            return paths;
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

    public async ValueTask DisposeAsync()
    {
        _initLock.Dispose();
        if (_rootsChangedRegistration is not null)
        {
            await _rootsChangedRegistration.DisposeAsync();
        }
    }
}
