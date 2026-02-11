using ProjGraph.Mcp;
using ProjGraph.Tests.Integration.Mcp.Helpers;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpIntegrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetProjectGraph_NullOrEmptyPath_ShouldThrowArgumentException(string? path)
    {
        var tools = CreateTools();
        var act = async () => await tools.GetProjectGraphAsync(path!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetClassDiagram_NullOrEmptyPath_ShouldThrowArgumentException(string? path)
    {
        var tools = CreateTools();
        var act = async () => await tools.GetClassDiagramAsync(path!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetErd_NullOrEmptyPath_ShouldThrowArgumentException(string? path)
    {
        var tools = CreateTools();
        var act = async () => await tools.GetErdAsync(path!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static ProjGraphTools CreateTools()
    {
        return McpTestHelper.CreateTools();
    }
}
