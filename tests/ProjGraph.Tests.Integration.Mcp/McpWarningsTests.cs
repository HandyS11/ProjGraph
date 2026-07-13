using ProjGraph.Mcp;
using ProjGraph.Tests.Integration.Mcp.Helpers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Integration.Mcp;

/// <summary>
/// Verifies that skip/partial-analysis warnings (normally written to a console the MCP server
/// discards) are surfaced in the tool results instead of vanishing.
/// </summary>
public sealed class McpWarningsTests : IDisposable
{
    private readonly TestDirectory _temp = new();

    private string CreateSolutionWithMalformedProject()
    {
        var badDir = Directory.CreateDirectory(Path.Combine(_temp.DirectoryPath, "Bad")).FullName;
        File.WriteAllText(Path.Combine(badDir, "Bad.csproj"), "<Project><PropertyGroup>");
        return _temp.CreateFile("sol.slnx", "<Solution><Project Path=\"Bad/Bad.csproj\" /></Solution>");
    }

    [Fact]
    public async Task GetProjectGraph_WithMalformedProject_AppendsWarningComment()
    {
        var slnxPath = CreateSolutionWithMalformedProject();
        var tools = McpTestHelper.CreateTools(new CollectingOutputConsole());

        var result = await tools.GetProjectGraphAsync(slnxPath);

        result.Should().Contain("%% WARNING").And.Contain("Bad.csproj");
    }

    [Fact]
    public async Task GetProjectStats_WithMalformedProject_IncludesWarningsInJson()
    {
        var slnxPath = CreateSolutionWithMalformedProject();
        var tools = McpTestHelper.CreateTools(new CollectingOutputConsole());

        var json = await tools.GetProjectStatsAsync(slnxPath);

        json.Should().Contain("warnings").And.Contain("Bad.csproj");
    }

    [Fact]
    public async Task GetProjectGraph_HealthySolution_HasNoWarningComment()
    {
        var slnxPath = _temp.CreateFile("empty.slnx", "<Solution></Solution>");
        var tools = McpTestHelper.CreateTools(new CollectingOutputConsole());

        var result = await tools.GetProjectGraphAsync(slnxPath);

        result.Should().NotContain("%% WARNING");
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
