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
    /// up to date by the ProjectReference). ProjGraph.Mcp is a self-contained exe, so its build
    /// lands in a RID subdirectory and must be launched via its apphost — the DLL that the
    /// ProjectReference copies into the test output has no runtime next to it and cannot start.
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
        Directory.Exists(binRoot).Should().BeTrue(
            $"the MCP server build output must exist at {binRoot}");
        var exeName = OperatingSystem.IsWindows() ? "ProjGraph.Mcp.exe" : "ProjGraph.Mcp";

        // The build RID matches the machine that built it, so probing the RID subdirectories
        // is exact enough without reconstructing the RID by hand. Preferring the most recently
        // written apphost keeps a dev machine with stale cross-RID leftovers deterministic.
        var serverExe = Directory.GetDirectories(binRoot)
            .Select(ridDir => new FileInfo(Path.Combine(ridDir, exeName)))
            .Where(apphost => apphost.Exists)
            .OrderByDescending(apphost => apphost.LastWriteTimeUtc)
            .FirstOrDefault();

        serverExe.Should().NotBeNull($"the MCP server apphost must be present under {binRoot}");
        return serverExe.FullName;
    }
}
