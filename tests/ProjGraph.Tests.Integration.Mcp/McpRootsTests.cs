using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Mcp;
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
        var service = new WorkspaceRootService(new PhysicalFileSystem());

        // Using a McpServer with null ClientCapabilities fails, so pass null
        // which exercises the Unsupported path when server capabilities are unavailable
        var act = async () => await service.TryResolveAsync("MySolution.slnx", null!, CancellationToken.None);

        // Without a server, we expect a NullReferenceException trying to access ClientCapabilities
        // In production, this is handled by the MCP server providing capabilities
        await act.Should().ThrowAsync<Exception>();
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

        var service = new WorkspaceRootService(new PhysicalFileSystem());
        SetRoots(service, [_temp.DirectoryPath]);

        var result = await service.TryResolveAsync(fileName, null!, CancellationToken.None);

        result.Should().Be(filePath);
    }

    [Fact]
    public async Task TryResolve_RelativePath_FileFoundInSubdirectory_ShouldReturnFullPath()
    {
        const string fileName = "Deep.slnx";
        var filePath = _temp.CreateFile(Path.Combine("src", "nested", fileName), "");

        var service = new WorkspaceRootService(new PhysicalFileSystem());
        SetRoots(service, [_temp.DirectoryPath]);

        var result = await service.TryResolveAsync(fileName, null!, CancellationToken.None);

        result.Should().Be(filePath);
    }

    [Fact]
    public async Task TryResolve_RelativePath_FileNotFound_ShouldThrowFileNotFoundException()
    {
        var service = new WorkspaceRootService(new PhysicalFileSystem());
        SetRoots(service, [_temp.DirectoryPath]);

        var act = async () => await service.TryResolveAsync("missing.slnx", null!, CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*missing.slnx*");
    }

    [Fact]
    public async Task TryResolve_RelativePath_AmbiguousMatch_ShouldThrowAmbiguousMatchException()
    {
        const string fileName = "Shared.slnx";
        _temp.CreateFile(fileName, "");

        using var temp2 = new TestDirectory();
        temp2.CreateFile(fileName, "");

        var service = new WorkspaceRootService(new PhysicalFileSystem());
        SetRoots(service, [_temp.DirectoryPath, temp2.DirectoryPath]);

        var act = async () => await service.TryResolveAsync(fileName, null!, CancellationToken.None);

        await act.Should().ThrowAsync<AmbiguousMatchException>()
            .WithMessage("*Shared.slnx*");
    }

    [Fact]
    public async Task TryResolve_RelativePath_FileInsideBinDirectory_ShouldNotBeFound()
    {
        const string fileName = "Hidden.slnx";
        _temp.CreateFile(Path.Combine("bin", fileName), "");

        var service = new WorkspaceRootService(new PhysicalFileSystem());
        SetRoots(service, [_temp.DirectoryPath]);

        var act = async () => await service.TryResolveAsync(fileName, null!, CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task TryResolve_RelativePath_FileInsideObjDirectory_ShouldNotBeFound()
    {
        const string fileName = "Artifact.slnx";
        _temp.CreateFile(Path.Combine("obj", fileName), "");

        var service = new WorkspaceRootService(new PhysicalFileSystem());
        SetRoots(service, [_temp.DirectoryPath]);

        var act = async () => await service.TryResolveAsync(fileName, null!, CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public void Dispose_ShouldReleaseSemaphore_WithoutThrowing()
    {
        var service = new WorkspaceRootService(new PhysicalFileSystem());
        var act = service.Dispose;
        act.Should().NotThrow();
    }

    private static void SetRoots(WorkspaceRootService service, IEnumerable<string> roots)
    {
        var type = typeof(WorkspaceRootService);
        var rootPathsField = type.GetField("_rootPaths", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var statusField = type.GetField("_status", BindingFlags.NonPublic | BindingFlags.Instance)!;
        rootPathsField.SetValue(service, roots.ToList());
        // RootsStatusKind.Ready = 2 (private enum inside WorkspaceRootService)
        statusField.SetValue(service, Enum.ToObject(statusField.FieldType, 2));
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
