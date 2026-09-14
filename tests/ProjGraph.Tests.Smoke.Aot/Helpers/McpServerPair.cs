using ModelContextProtocol.Client;

namespace ProjGraph.Tests.Smoke.Aot.Helpers;

/// <summary>
/// One native and one reference MCP server, shared by every test in a class. Stopping a stdio
/// client takes seconds, and one long-lived session per build is also how clients use the server.
/// The servers start on first use, so a skipped suite never launches them.
/// </summary>
public sealed class McpServerPair : IAsyncLifetime
{
    private McpClient? _native;
    private McpClient? _reference;

    /// <summary>
    /// Connects to both servers on the first call and returns the same clients afterwards.
    /// </summary>
    /// <returns>The native and the reference client.</returns>
    public async Task<(McpClient Native, McpClient Reference)> GetClientsAsync()
    {
        _native ??= await ConnectAsync(SmokeEnvironment.McpNative);
        _reference ??= await ConnectAsync(SmokeEnvironment.McpReference);
        return (_native, _reference);
    }

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_native is not null)
        {
            await _native.DisposeAsync();
        }

        if (_reference is not null)
        {
            await _reference.DisposeAsync();
        }
    }

    private static async Task<McpClient> ConnectAsync(SmokeCommand command)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ProjGraph AOT smoke",
            Command = command.FileName,
            Arguments = [.. command.LeadingArguments],
            WorkingDirectory = SmokeEnvironment.RepositoryRoot
        });

        return await McpClient.CreateAsync(transport);
    }
}
