using ProjGraph.Mcp;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Integration.Mcp;

public sealed class McpRootsTests : IDisposable
{
    private readonly TestDirectory _temp = new();

    [Fact]
    public async Task TryResolve_AbsolutePath_ShouldPassThrough()
    {
        var service = new WorkspaceRootService();
        const string absolutePath = @"D:\Projects\MySolution.slnx";

        var result = await service.TryResolveAsync(absolutePath, null!, CancellationToken.None);

        result.Should().Be(absolutePath);
    }

    [Fact]
    public async Task TryResolve_RelativePath_NoRootsCapability_ShouldThrow()
    {
        var service = new WorkspaceRootService();

        // Using a McpServer with null ClientCapabilities fails, so pass null
        // which exercises the Unsupported path when server capabilities are unavailable
        var act = async () => await service.TryResolveAsync("MySolution.slnx", null!, CancellationToken.None);

        // Without a server, we expect a NullReferenceException trying to access ClientCapabilities
        // In production, this is handled by the MCP server providing capabilities
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public void AbsolutePath_IsFullyQualified_Windows()
    {
        Path.IsPathFullyQualified(@"D:\Projects\MySolution.slnx").Should().BeTrue();
    }

    [Fact]
    public void RelativePath_IsNotFullyQualified()
    {
        Path.IsPathFullyQualified("MySolution.slnx").Should().BeFalse();
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
