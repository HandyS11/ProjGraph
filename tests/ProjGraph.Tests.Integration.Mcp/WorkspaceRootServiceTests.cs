using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Mcp;
using ProjGraph.Tests.Integration.Mcp.Helpers;
using ProjGraph.Tests.Shared.Helpers;
using System.Reflection;

namespace ProjGraph.Tests.Integration.Mcp;

/// <summary>
/// Covers the parts of <see cref="WorkspaceRootService"/> that need a live
/// <see cref="ModelContextProtocol.Server.McpServer"/>: negotiating the client's roots capability,
/// fetching the roots over <c>roots/list</c>, and invalidating them when the client reports a
/// change. <c>McpRootsTests</c> covers the pure path-matching logic.
/// </summary>
public sealed class WorkspaceRootServiceTests : IDisposable
{
    private readonly TestDirectory _temp = new();

    [Fact]
    public async Task TryResolveAsync_ClientWithoutRootsCapability_ShouldAskForAnAbsolutePath()
    {
        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithoutRoots());
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        var act = async () => await service.TryResolveAsync("App.slnx", session.Server, CancellationToken.None);

        // McpException so the guidance survives the SDK's tool boundary instead of being replaced
        // by a generic "An error occurred invoking …".
        (await act.Should().ThrowAsync<McpException>())
            .WithMessage("*does not support workspace roots*absolute path*");
    }

    [Fact]
    public async Task TryResolveAsync_ClientWithRoots_ShouldResolveARelativePathAgainstTheRoot()
    {
        var filePath = _temp.CreateFile("App.slnx", "");
        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(() => [_temp.DirectoryPath]));
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        var resolved = await service.TryResolveAsync("App.slnx", session.Server, CancellationToken.None);

        resolved.Should().Be(filePath);
    }

    [Fact]
    public async Task TryResolveAsync_ClientWithMultipleRoots_ShouldSearchAllOfThem()
    {
        using var secondRoot = new TestDirectory();
        var filePath = secondRoot.CreateFile(Path.Combine("nested", "Deep.slnx"), "");

        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(
                () => [_temp.DirectoryPath, secondRoot.DirectoryPath]));
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        var resolved = await service.TryResolveAsync("Deep.slnx", session.Server, CancellationToken.None);

        resolved.Should().Be(filePath);
    }

    [Fact]
    public async Task TryResolveAsync_ConcurrentFirstCalls_ShouldFetchTheRootsOnlyOnce()
    {
        var filePath = _temp.CreateFile("App.slnx", "");
        var rootsRequests = 0;

        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(() =>
            {
                Interlocked.Increment(ref rootsRequests);
                return [_temp.DirectoryPath];
            }));
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        var resolved = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => service.TryResolveAsync("App.slnx", session.Server, CancellationToken.None)));

        // Initialization is guarded by a semaphore plus a re-check, so concurrent tool invocations
        // must not each issue their own roots/list round trip.
        resolved.Should().AllBe(filePath);
        Volatile.Read(ref rootsRequests).Should().Be(1);
    }

    [Fact]
    public async Task TryResolveAsync_AfterInvalidateRoots_ShouldRefetchWithoutReRegisteringTheHandler()
    {
        var filePath = _temp.CreateFile("App.slnx", "");
        var rootsRequests = 0;

        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(() =>
            {
                Interlocked.Increment(ref rootsRequests);
                return [_temp.DirectoryPath];
            }));
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        await service.TryResolveAsync("App.slnx", session.Server, CancellationToken.None);
        var firstRegistration = GetRootsChangedRegistration(service);

        service.InvalidateRoots();
        var resolved = await service.TryResolveAsync("App.slnx", session.Server, CancellationToken.None);

        resolved.Should().Be(filePath);
        Volatile.Read(ref rootsRequests).Should().Be(2, "invalidation must force a fresh roots/list");
        GetRootsChangedRegistration(service).Should().BeSameAs(firstRegistration,
            "the roots/list_changed handler is registered once per session, not once per refresh");
    }

    [Fact]
    public async Task RootsListChangedNotification_ShouldInvalidateTheCachedRoots()
    {
        using var secondRoot = new TestDirectory();
        var movedFile = secondRoot.CreateFile("Moved.slnx", "");

        var currentRoots = new List<string> { _temp.DirectoryPath };
        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(
                () => [.. Volatile.Read(ref currentRoots)]));
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        _temp.CreateFile("App.slnx", "");
        await service.TryResolveAsync("App.slnx", session.Server, CancellationToken.None);

        // The workspace switches to a different folder and the client announces it.
        Volatile.Write(ref currentRoots, [secondRoot.DirectoryPath]);
#pragma warning disable MCP9005 // Roots is deprecated (SEP-2577); still served for down-level clients.
        await session.Client.SendNotificationAsync(NotificationMethods.RootsListChangedNotification);
#pragma warning restore MCP9005

        await WaitForRootsInvalidationAsync(service);
        var resolved = await service.TryResolveAsync("Moved.slnx", session.Server, CancellationToken.None);

        resolved.Should().Be(movedFile, "the notification must drop the stale roots so the new one is used");
    }

    [Fact]
    public async Task DisposeAsync_AfterTheRootsHandlerWasRegistered_ShouldNotThrow()
    {
        _temp.CreateFile("App.slnx", "");
        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(() => [_temp.DirectoryPath]));
        var service = new WorkspaceRootService(new PhysicalFileSystem());

        await service.TryResolveAsync("App.slnx", session.Server, CancellationToken.None);
        GetRootsChangedRegistration(service).Should().NotBeNull();

        var act = async () => await service.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ResolveMatches_BareDirectoryNameInASubdirectory_ShouldResolveRecursively()
    {
        // The direct root+relative combine cannot find it, so this exercises the recursive search's
        // directory branch (as opposed to its file branch).
        var service = new WorkspaceRootService(new PhysicalFileSystem());
        var nested = Directory.CreateDirectory(
            Path.Combine(_temp.DirectoryPath, "src", "Domain", "Models")).FullName;

        var matches = service.ResolveMatches([_temp.DirectoryPath], "Models");

        matches.Should().ContainSingle().Which.Should().Be(nested);
    }

    [Fact]
    public void ResolveMatches_InaccessibleDirectory_ShouldBeSkippedRatherThanFailTheSearch()
    {
        using var accessibleRoot = new TestDirectory();
        var target = accessibleRoot.CreateFile(Path.Combine("nested", "Target.slnx"), "");

        // The first root's only subdirectory cannot be listed; the search must keep going instead
        // of surfacing the access failure to the client.
        var blocked = Directory.CreateDirectory(Path.Combine(_temp.DirectoryPath, "restricted")).FullName;
        var fileSystem = new BlockedDirectoryFileSystem(new PhysicalFileSystem(), blocked);
        var service = new WorkspaceRootService(fileSystem);

        var matches = service.ResolveMatches([_temp.DirectoryPath, accessibleRoot.DirectoryPath], "Target.slnx");

        matches.Should().ContainSingle().Which.Should().Be(target);
    }

    public void Dispose()
    {
        _temp.Dispose();
    }

    private static object? GetRootsChangedRegistration(WorkspaceRootService service)
    {
        return typeof(WorkspaceRootService)
            .GetField("_rootsChangedRegistration", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(service);
    }

    private static async Task WaitForRootsInvalidationAsync(WorkspaceRootService service)
    {
        var statusField = typeof(WorkspaceRootService)
            .GetField("_status", BindingFlags.NonPublic | BindingFlags.Instance)!;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (statusField.GetValue(service)!.ToString() != "Unknown")
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(20, timeout.Token);
        }
    }

    /// <summary>
    /// A file system that behaves like the real one except that listing files in one specific
    /// directory fails the way an unreadable directory would.
    /// </summary>
    /// <param name="inner">The real file system every other operation delegates to.</param>
    /// <param name="blockedDirectory">The directory whose file listing throws.</param>
    private sealed class BlockedDirectoryFileSystem(IFileSystem inner, string blockedDirectory) : IFileSystem
    {
        public string[] GetFiles(string path, string searchPattern)
        {
            if (string.Equals(path, blockedDirectory, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException($"Access to '{path}' is denied.");
            }

            return inner.GetFiles(path, searchPattern);
        }

        public bool FileExists(string path) => inner.FileExists(path);

        public string ReadAllText(string path) => inner.ReadAllText(path);

        public string GetFullPath(string path) => inner.GetFullPath(path);

        public string? GetDirectoryName(string path) => inner.GetDirectoryName(path);

        public string GetExtension(string path) => inner.GetExtension(path);

        public string Combine(params string[] paths) => inner.Combine(paths);

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
            => inner.ReadAllTextAsync(path, cancellationToken);

        public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
            => inner.WriteAllTextAsync(path, contents, cancellationToken);

        public void CreateDirectory(string path) => inner.CreateDirectory(path);

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);

        public string[] GetDirectories(string path) => inner.GetDirectories(path);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions options)
            => inner.EnumerateFiles(path, searchPattern, options);

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions options)
            => inner.EnumerateDirectories(path, searchPattern, options);

        public string GetCurrentDirectory() => inner.GetCurrentDirectory();
    }
}
