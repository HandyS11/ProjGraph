using ModelContextProtocol;
using ProjGraph.Mcp;
using ProjGraph.Tests.Integration.Mcp.Helpers;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpIntegrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetProjectGraph_NullOrEmptyPath_ShouldThrowMcpException(string? path)
    {
        var tools = CreateTools();
        var act = async () => await tools.GetProjectGraphAsync(path!);
        // McpException so the guidance reaches the client; the SDK strips the message
        // from any other exception type.
        await act.Should().ThrowAsync<McpException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetClassDiagram_NullOrEmptyPath_ShouldThrowMcpException(string? path)
    {
        var tools = CreateTools();
        var act = async () => await tools.GetClassDiagramAsync(path!);
        // McpException so the guidance reaches the client; the SDK strips the message
        // from any other exception type.
        await act.Should().ThrowAsync<McpException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetErd_NullOrEmptyPath_ShouldThrowMcpException(string? path)
    {
        var tools = CreateTools();
        var act = async () => await tools.GetErdAsync(path!);
        // McpException so the guidance reaches the client; the SDK strips the message
        // from any other exception type.
        await act.Should().ThrowAsync<McpException>();
    }

    private static ProjGraphTools CreateTools()
    {
        return McpTestHelper.CreateTools();
    }
}
