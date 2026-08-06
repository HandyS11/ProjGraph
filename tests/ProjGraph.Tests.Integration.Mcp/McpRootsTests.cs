using ModelContextProtocol;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Mcp;
using ProjGraph.Tests.Integration.Mcp.Helpers;
using ProjGraph.Tests.Shared.Helpers;
using System.Reflection;

namespace ProjGraph.Tests.Integration.Mcp;

public sealed class McpRootsTests : IDisposable
{
    private readonly TestDirectory _temp = new();

    [Fact]
    public async Task TryResolve_AbsolutePath_ShouldPassThrough()
    {
        var service = new WorkspaceRootService(new PhysicalFileSystem());
        var absolutePath = Path.Combine(Path.GetTempPath(), "MySolution.slnx");

        var result = await service.TryResolveAsync(absolutePath, null!, CancellationToken.None);

        result.Should().Be(absolutePath);
    }

    [Fact]
    public async Task TryResolve_RelativePath_NoRootsCapability_ShouldThrow()
    {
        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithoutRoots());
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        var act = async () => await service.TryResolveAsync("MySolution.slnx", session.Server, CancellationToken.None);

        // McpException so the guidance reaches the client; the SDK strips the message from any
        // other exception type.
        await act.Should().ThrowAsync<McpException>()
            .WithMessage("*does not support workspace roots*absolute path*");
    }

    [Fact]
    public void ResolveMatches_RelativeDirectory_ResolvesToDirectory()
    {
        var service = new WorkspaceRootService(new PhysicalFileSystem());
        var root = _temp.DirectoryPath;
        var modelsDir = Directory.CreateDirectory(Path.Combine(root, "Models")).FullName;

        var matches = service.ResolveMatches([root], "Models");

        matches.Should().ContainSingle().Which.Should().Be(modelsDir);
    }

    [Fact]
    public void ResolveMatches_RelativeFileWithSubdirectory_ResolvesToFile()
    {
        var service = new WorkspaceRootService(new PhysicalFileSystem());
        var root = _temp.DirectoryPath;
        Directory.CreateDirectory(Path.Combine(root, "Models"));
        var file = Path.Combine(root, "Models", "Foo.cs");
        File.WriteAllText(file, "// x");

        var matches = service.ResolveMatches([root], Path.Combine("Models", "Foo.cs"));

        matches.Should().ContainSingle().Which.Should().Be(file);
    }

    [Fact]
    public void ResolveMatches_BareFilename_ResolvesRecursively()
    {
        var service = new WorkspaceRootService(new PhysicalFileSystem());
        var root = _temp.DirectoryPath;
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        var file = Path.Combine(root, "nested", "Bar.cs");
        File.WriteAllText(file, "// x");

        var matches = service.ResolveMatches([root], "Bar.cs");

        matches.Should().ContainSingle().Which.Should().Be(file);
    }

    [Fact]
    public void ResolveMatches_DotDotPrefixedName_ResolvesWithinRoot()
    {
        // A legitimate directory/file whose name merely starts with ".." (e.g. "..data") lives
        // inside the root and must resolve; only a real parent segment ("..") is traversal.
        var service = new WorkspaceRootService(new PhysicalFileSystem());
        var root = _temp.DirectoryPath;
        Directory.CreateDirectory(Path.Combine(root, "..data"));
        var file = Path.Combine(root, "..data", "Foo.cs");
        File.WriteAllText(file, "// x");

        var matches = service.ResolveMatches([root], Path.Combine("..data", "Foo.cs"));

        matches.Should().ContainSingle().Which.Should().Be(file);
    }

    [Fact]
    public void ResolveMatches_WildcardPattern_ShouldThrow()
    {
        var service = new WorkspaceRootService(new PhysicalFileSystem());

        var act = () => service.ResolveMatches([_temp.DirectoryPath], "*.cs");

        // McpException so the wildcard guidance reaches the client instead of a stripped generic error.
        act.Should().Throw<McpException>().WithMessage("*wildcard*");
    }

    [Fact]
    public void ResolveMatches_ParentTraversal_DoesNotEscapeRoot()
    {
        var service = new WorkspaceRootService(new PhysicalFileSystem());
        var root = Directory.CreateDirectory(Path.Combine(_temp.DirectoryPath, "workspace")).FullName;
        // A file that exists just outside the root must not be resolvable via "..".
        File.WriteAllText(Path.Combine(_temp.DirectoryPath, "outside.cs"), "// x");

        var matches = service.ResolveMatches([root], Path.Combine("..", "outside.cs"));

        matches.Should().BeEmpty();
    }

    [Fact]
    public void InvalidateRoots_ResetsStatusToUnknown()
    {
        // A roots/list_changed notification must invalidate the cached roots so the next
        // resolution re-fetches them.
        var service = new WorkspaceRootService(new PhysicalFileSystem());
        var statusField = typeof(WorkspaceRootService)
            .GetField("_status", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var statusEnum = typeof(WorkspaceRootService).GetNestedType("RootsStatusKind", BindingFlags.NonPublic)!;
        statusField.SetValue(service, Enum.Parse(statusEnum, "Ready"));

        service.InvalidateRoots();

        statusField.GetValue(service)!.ToString().Should().Be("Unknown");
    }

    [Fact]
    public void AbsolutePath_IsFullyQualified()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), "MySolution.slnx");
        Path.IsPathFullyQualified(absolutePath).Should().BeTrue();
    }

    [Fact]
    public void RelativePath_IsNotFullyQualified()
    {
        Path.IsPathFullyQualified("MySolution.slnx").Should().BeFalse();
    }

    [Fact]
    public async Task TryResolve_RelativePath_FileFoundInRoot_ShouldReturnFullPath()
    {
        const string fileName = "MySolution.slnx";
        var filePath = _temp.CreateFile(fileName, "");

        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(() => [_temp.DirectoryPath]));
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        var result = await service.TryResolveAsync(fileName, session.Server, CancellationToken.None);

        result.Should().Be(filePath);
    }

    [Fact]
    public async Task TryResolve_RelativePath_FileFoundInSubdirectory_ShouldReturnFullPath()
    {
        const string fileName = "Deep.slnx";
        var filePath = _temp.CreateFile(Path.Combine("src", "nested", fileName), "");

        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(() => [_temp.DirectoryPath]));
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        var result = await service.TryResolveAsync(fileName, session.Server, CancellationToken.None);

        result.Should().Be(filePath);
    }

    [Fact]
    public async Task TryResolve_RelativePath_FileNotFound_ShouldThrowMcpException()
    {
        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(() => [_temp.DirectoryPath]));
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        var act = async () => await service.TryResolveAsync("missing.slnx", session.Server, CancellationToken.None);

        // McpException so the not-found guidance reaches the client instead of a stripped generic error.
        await act.Should().ThrowAsync<McpException>()
            .WithMessage("*missing.slnx*");
    }

    [Fact]
    public async Task TryResolve_RelativePath_AmbiguousMatch_ShouldThrowMcpException()
    {
        const string fileName = "Shared.slnx";
        _temp.CreateFile(fileName, "");

        using var temp2 = new TestDirectory();
        temp2.CreateFile(fileName, "");

        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(
                () => [_temp.DirectoryPath, temp2.DirectoryPath]));
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        var act = async () => await service.TryResolveAsync(fileName, session.Server, CancellationToken.None);

        // McpException so the ambiguity guidance reaches the client instead of a stripped generic error.
        await act.Should().ThrowAsync<McpException>()
            .WithMessage("*Shared.slnx*");
    }

    [Fact]
    public async Task TryResolve_RelativePath_FileInsideBinDirectory_ShouldNotBeFound()
    {
        const string fileName = "Hidden.slnx";
        _temp.CreateFile(Path.Combine("bin", fileName), "");

        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(() => [_temp.DirectoryPath]));
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        var act = async () => await service.TryResolveAsync(fileName, session.Server, CancellationToken.None);

        await act.Should().ThrowAsync<McpException>();
    }

    [Fact]
    public async Task TryResolve_RelativePath_FileInsideObjDirectory_ShouldNotBeFound()
    {
        const string fileName = "Artifact.slnx";
        _temp.CreateFile(Path.Combine("obj", fileName), "");

        await using var session = await InProcessMcpSession.StartAsync(
            clientOptions: InProcessMcpSession.CreateClientOptionsWithRoots(() => [_temp.DirectoryPath]));
        await using var service = new WorkspaceRootService(new PhysicalFileSystem());

        var act = async () => await service.TryResolveAsync(fileName, session.Server, CancellationToken.None);

        await act.Should().ThrowAsync<McpException>();
    }

    [Fact]
    public async Task DisposeAsync_ShouldReleaseSemaphore_WithoutThrowing()
    {
        var service = new WorkspaceRootService(new PhysicalFileSystem());
        var act = async () => await service.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
