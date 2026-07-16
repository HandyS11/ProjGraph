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

    [Fact]
    public async Task GetClassDiagram_ManyFiles_AppendsFileCountWarningAfterDiagram()
    {
        // Arrange — 51 files crosses the >50 threshold for the large-scan warning.
        for (var i = 0; i < 51; i++)
        {
            _temp.CreateFile(Path.Combine("many", $"C{i}.cs"), $"namespace Many; public class C{i} {{ }}");
        }

        var tools = McpTestHelper.CreateTools(new CollectingOutputConsole());

        // Act
        var result = await tools.GetClassDiagramAsync(Path.Combine(_temp.DirectoryPath, "many"));

        // Assert — the warning must TRAIL the diagram like get_project_graph's warnings do:
        // prepended ahead of the YAML front-matter, strict Mermaid parsers reject the diagram.
        result.Should().Contain("%% WARNING: Scanning 51 files");
        result.TrimStart().Should().NotStartWith("%% WARNING");
        result.IndexOf("classDiagram", StringComparison.Ordinal).Should().BeLessThan(
            result.IndexOf("%% WARNING", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CollectingOutputConsole_ScopesWarningsToAsyncFlow()
    {
        // Two independent async flows using the same (singleton) console must not see each other's
        // warnings — the buffer is scoped per async flow, not shared process-wide.
        var console = new CollectingOutputConsole();

        async Task<IReadOnlyList<string>> CollectAsync(string message)
        {
            console.ClearWarnings();
            await Task.Yield();
            console.WriteWarning(message);
            await Task.Yield();
            return console.DrainWarnings();
        }

        var results = await Task.WhenAll(CollectAsync("first"), CollectAsync("second"));

        results.Should().OnlyContain(r => r.Count == 1);
        results.SelectMany(r => r).Should().Contain("first").And.Contain("second");
    }

    public void Dispose()
    {
        _temp.Dispose();
    }
}
