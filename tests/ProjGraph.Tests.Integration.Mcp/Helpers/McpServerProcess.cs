using ModelContextProtocol.Client;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Integration.Mcp.Helpers;

/// <summary>
/// Launches the real MCP server executable from ProjGraph.Mcp's own build output and connects a
/// <see cref="McpClient"/> to it over the stdio transport.
/// </summary>
internal static class McpServerProcess
{
    /// <summary>
    /// Connects a client to the server apphost in ProjGraph.Mcp's own build output (guaranteed
    /// up to date by the ProjectReference). ProjGraph.Mcp builds framework-dependent, so the apphost
    /// sits directly in <c>bin/{Configuration}/{tfm}/</c>; RID subdirectories hold
    /// <c>dotnet pack -r</c> or <c>dotnet publish -r</c> output and are ignored.
    /// The client deliberately advertises no capabilities — in particular no workspace roots.
    /// </summary>
    /// <returns>A connected client; disposing it stops the server process.</returns>
    public static async Task<McpClient> ConnectAsync()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ProjGraph e2e",
            Command = LocateServerExecutable()
        });

        return await McpClient.CreateAsync(transport);
    }

    private static string LocateServerExecutable()
    {
        // .../tests/ProjGraph.Tests.Integration.Mcp/bin/{Configuration}/{tfm}/
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var binRoot = TestPathHelper.GetRootPath(Path.Combine(
            "src", "ProjGraph.Mcp", "bin", testOutput.Parent!.Name, testOutput.Name));
        var exeName = OperatingSystem.IsWindows() ? "ProjGraph.Mcp.exe" : "ProjGraph.Mcp";
        var serverExe = new FileInfo(Path.Combine(binRoot, exeName));

        serverExe.Exists.Should().BeTrue($"the MCP server apphost must be present at {serverExe.FullName}");
        return serverExe.FullName;
    }
}
