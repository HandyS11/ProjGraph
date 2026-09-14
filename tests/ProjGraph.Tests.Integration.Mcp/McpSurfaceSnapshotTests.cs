using ProjGraph.Tests.Integration.Mcp.Helpers;
using ProjGraph.Tests.Shared.Helpers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProjGraph.Tests.Integration.Mcp;

/// <summary>
/// Pins the tool and prompt surface the real server executable advertises: names, descriptions,
/// tool input schemas, and prompt arguments. It guards serializer changes, such as moving
/// registration onto source-generated JSON metadata, that would silently change what clients see.
/// </summary>
public sealed class McpSurfaceSnapshotTests
{
    private const string SnapshotRelativePath = "tests/ProjGraph.Tests.Integration.Mcp/Snapshots/mcp-surface.json";

    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    [Fact]
    public async Task ToolsAndPrompts_ShouldMatchTheCommittedSnapshot()
    {
        await using var client = await McpServerProcess.ConnectAsync();
        var tools = await client.ListToolsAsync();
        var prompts = await client.ListPromptsAsync();

        var actual = new JsonObject
        {
            ["tools"] = new JsonArray([
                .. tools.OrderBy(tool => tool.Name, StringComparer.Ordinal).Select(tool => new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["inputSchema"] = JsonNode.Parse(tool.ProtocolTool.InputSchema.GetRawText())
                })
            ]),
            ["prompts"] = new JsonArray([
                .. prompts.OrderBy(prompt => prompt.Name, StringComparer.Ordinal).Select(prompt => new JsonObject
                {
                    ["name"] = prompt.Name,
                    ["description"] = prompt.Description,
                    ["arguments"] = new JsonArray([
                        .. (prompt.ProtocolPrompt.Arguments ?? []).Select(argument => new JsonObject
                        {
                            ["name"] = argument.Name,
                            ["description"] = argument.Description,
                            ["required"] = argument.Required
                        })
                    ])
                })
            ])
        };

        var snapshotPath = TestPathHelper.GetRootPath(SnapshotRelativePath);
        if (!File.Exists(snapshotPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            await File.WriteAllTextAsync(snapshotPath, actual.ToJsonString(IndentedOptions) + "\n");
            Assert.Fail($"Snapshot created at {snapshotPath}. Review and commit it, then re-run.");
        }

        var expected = JsonNode.Parse(await File.ReadAllTextAsync(snapshotPath));
        JsonNode.DeepEquals(expected, actual).Should().BeTrue(
            $"the advertised MCP surface must match {SnapshotRelativePath}; actual:\n{actual.ToJsonString(IndentedOptions)}");
    }
}
