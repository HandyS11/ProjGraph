using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ProjGraph.Tests.Shared.Helpers;
using ProjGraph.Tests.Smoke.Aot.Helpers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProjGraph.Tests.Smoke.Aot;

/// <summary>
/// Starts the native MCP server and the JIT build of the same commit over stdio, sends both the
/// same requests, and requires identical protocol results.
/// </summary>
/// <param name="servers">The native and reference servers shared by this class.</param>
public sealed class McpParityTests(McpServerPair servers) : IClassFixture<McpServerPair>
{
    [AotSmokeFact]
    public async Task Initialize_ShouldAdvertiseTheSameServer()
    {
        var (native, reference) = await servers.GetClientsAsync();

        reference.ServerInfo.Name.Should().Be("ProjGraph");
        AssertSameJson(native.ServerInfo, reference.ServerInfo);
        AssertSameJson(native.ServerCapabilities, reference.ServerCapabilities);
    }

    [AotSmokeFact]
    public async Task ListTools_ShouldMatchReference()
    {
        await AssertSameResultAsync(client => client.ListToolsAsync(new ListToolsRequestParams()));
    }

    [AotSmokeFact]
    public async Task ListPrompts_ShouldMatchReference()
    {
        await AssertSameResultAsync(client => client.ListPromptsAsync(new ListPromptsRequestParams()));
    }

    [AotSmokeFact]
    public async Task GetPrompt_ShouldMatchReference()
    {
        // Prompt results serialize IEnumerable<ChatMessage> through McpJsonContext.
        var path = SmokeEnvironment.GetRootPath("samples/classdiagram/complex-hierarchy/Domain/Models/CEO.cs");

        await AssertSameResultAsync(client => client.GetPromptAsync(
            "class_structure_review", new Dictionary<string, object?> { ["path"] = path }));
    }

    [AotSmokeFact]
    public async Task ReadWelcomeResource_ShouldMatchReference()
    {
        await AssertSameResultAsync(client => client.ReadResourceAsync(new Uri("projgraph://welcome")));
    }

    [AotSmokeFact]
    public async Task GetProjectGraph_ShouldMatchReference()
    {
        await AssertSameToolResultAsync("get_project_graph", new Dictionary<string, object?>
        {
            ["path"] = SmokeEnvironment.GetRootPath("samples/visualize/modular-architecture/ModularArchitecture.slnx")
        });
    }

    [AotSmokeFact]
    public async Task GetProjectStats_ShouldMatchReference()
    {
        await AssertSameToolResultAsync("get_project_stats", new Dictionary<string, object?>
        {
            ["path"] = SmokeEnvironment.GetRootPath("samples/visualize/modular-architecture/ModularArchitecture.slnx")
        });
    }

    [AotSmokeFact]
    public async Task GetProjectStats_SolutionWithMalformedProject_ShouldMatchReference()
    {
        // The warnings array is built with JsonNode APIs that fail without reflection-based JSON.
        using var temp = new TestDirectory();
        temp.CreateFile("Good/Good.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        temp.CreateFile("Bad/Bad.csproj", "<Project><PropertyGroup></Project>");
        var solution = temp.CreateFile("Malformed.slnx",
            "<Solution><Project Path=\"Good/Good.csproj\" /><Project Path=\"Bad/Bad.csproj\" /></Solution>");

        var reference = await AssertSameToolResultAsync(
            "get_project_stats", new Dictionary<string, object?> { ["path"] = solution });

        var warnings = JsonNode.Parse(JoinText(reference))?["warnings"]?.AsArray();
        warnings.Should().NotBeNullOrEmpty("the malformed project must surface as a warning");
    }

    [AotSmokeFact]
    public async Task GetClassDiagram_ShouldMatchReference()
    {
        await AssertSameToolResultAsync("get_class_diagram", new Dictionary<string, object?>
        {
            ["path"] = SmokeEnvironment.GetRootPath("samples/classdiagram/complex-hierarchy/Domain/Models/CEO.cs"),
            ["options"] = new Dictionary<string, object?> { ["includeInheritance"] = true }
        });
    }

    [AotSmokeFact]
    public async Task GetErd_ShouldMatchReference()
    {
        await AssertSameToolResultAsync("get_erd", new Dictionary<string, object?>
        {
            ["path"] = SmokeEnvironment.GetRootPath("samples/erd/complex-ecommerce/Data/MyDbContext.cs")
        });
    }

    private async Task<T> AssertSameResultAsync<T>(Func<McpClient, ValueTask<T>> request)
    {
        var (native, reference) = await servers.GetClientsAsync();

        var referenceResult = await request(reference);
        AssertSameJson(await request(native), referenceResult);
        return referenceResult;
    }

    private async Task<CallToolResult> AssertSameToolResultAsync(
        string toolName, Dictionary<string, object?> arguments)
    {
        var reference = await AssertSameResultAsync(client => client.CallToolAsync(toolName, arguments));

        // Pins the reference outcome, so an error both builds return identically cannot pass as parity.
        reference.IsError.Should().NotBe(true, JoinText(reference));
        return reference;
    }

    private static void AssertSameJson<T>(T native, T reference)
    {
        var nativeJson = JsonSerializer.SerializeToNode(native, McpJsonUtilities.DefaultOptions);
        var referenceJson = JsonSerializer.SerializeToNode(reference, McpJsonUtilities.DefaultOptions);

        JsonNode.DeepEquals(nativeJson, referenceJson).Should().BeTrue(
            $"the native server must return the reference result.\nNative:\n{nativeJson}\nReference:\n{referenceJson}");
    }

    private static string JoinText(CallToolResult result)
    {
        return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
    }
}
