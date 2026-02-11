using ProjGraph.Mcp;
using ProjGraph.Tests.Integration.Mcp.Helpers;

namespace ProjGraph.Tests.Integration.Mcp;

public class McpIntegrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetProjectGraph_NullOrEmptyPath_ShouldThrowArgumentException(string? path)
    {
        var tools = CreateTools();
        var act = () => tools.GetProjectGraph(path!);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetClassDiagram_NullOrEmptyPath_ShouldThrowArgumentException(string? path)
    {
        var tools = CreateTools();
        var act = () => tools.GetClassDiagram(path!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetErd_NullOrEmptyPath_ShouldThrowArgumentException(string? path)
    {
        var tools = CreateTools();
        var act = () => tools.GetErd(path!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static ProjGraphTools CreateTools()
    {
        return McpTestHelper.CreateTools();
    }
}
