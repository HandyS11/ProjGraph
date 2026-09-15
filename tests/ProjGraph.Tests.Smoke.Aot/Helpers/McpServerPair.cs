using ModelContextProtocol.Client;
using System.Collections.Concurrent;

namespace ProjGraph.Tests.Smoke.Aot.Helpers;

/// <summary>
/// One native and one reference MCP server, shared by every test in a class. Stopping a stdio
/// client takes seconds, and one long-lived session per build is also how clients use the server.
/// The servers start on first use, so a skipped suite never launches them.
/// </summary>
public sealed class McpServerPair : IAsyncLifetime
{
    private readonly ConcurrentQueue<string> _nativeStandardError = new();
    private readonly ConcurrentQueue<string> _referenceStandardError = new();
    private McpClient? _native;
    private McpClient? _reference;

    /// <summary>
    /// Connects to both servers on the first call and returns the same clients afterwards.
    /// </summary>
    /// <returns>The native and the reference client.</returns>
    public async Task<(McpClient Native, McpClient Reference)> GetClientsAsync()
    {
        _native ??= await ConnectAsync(SmokeEnvironment.McpNative, _nativeStandardError);
        _reference ??= await ConnectAsync(SmokeEnvironment.McpReference, _referenceStandardError);
        return (_native, _reference);
    }

    /// <summary>
    /// Describes everything both servers have written to standard error so far. The servers log
    /// warnings and unhandled exceptions there, which the protocol results alone never show.
    /// </summary>
    /// <returns>The native and the reference server's standard error, labelled.</returns>
    public string DescribeStandardError()
    {
        return $"Native server stderr:\n{string.Join('\n', _nativeStandardError)}\n" +
               $"Reference server stderr:\n{string.Join('\n', _referenceStandardError)}";
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

    private static async Task<McpClient> ConnectAsync(SmokeCommand command, ConcurrentQueue<string> standardError)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ProjGraph AOT smoke",
            Command = command.FileName,
            Arguments = [.. command.LeadingArguments],
            WorkingDirectory = SmokeEnvironment.RepositoryRoot,
            StandardErrorLines = standardError.Enqueue
        });

        return await McpClient.CreateAsync(transport);
    }
}
