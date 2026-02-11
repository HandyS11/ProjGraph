using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Mcp;

/// <summary>
/// A no-op implementation of IOutputConsole for the MCP server.
/// Prevents ANSI markup from being written to stdout, which is used
/// for JSON-RPC transport in the MCP protocol.
/// </summary>
internal sealed class NullOutputConsole : IOutputConsole
{
    public void Write(string message) { }
    public void WriteLine(string message) { }
    public void WriteInfo(string message) { }
    public void WriteError(string message) { }
    public void WriteWarning(string message) { }
    public void WriteSuccess(string message) { }
    public void WriteMarkup(string markup) { }

    public Task<string> PromptSelectionAsync(string title, IEnumerable<string> choices,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(choices.First());
    }

    public Task RunWithStatusAsync(string statusMessage, Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        return action();
    }
}
