using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Mcp;

/// <summary>
/// An <see cref="IOutputConsole"/> for the MCP server that collects warning messages in memory
/// instead of writing to stdout (which is reserved for the JSON-RPC transport). Collected warnings
/// can be drained and surfaced in a tool's result, so skipped/partial analysis is no longer silent.
/// All other output is discarded, exactly like <c>NullOutputConsole</c>.
/// </summary>
/// <remarks>
/// The warning buffer is stored in an <see cref="AsyncLocal{T}"/> so that each request's async flow
/// collects into its own list. This keeps concurrent tool invocations isolated — one request cannot
/// clear or drain another's warnings — even though the console is registered as a singleton.
/// </remarks>
internal sealed class CollectingOutputConsole : IOutputConsole
{
    private readonly AsyncLocal<List<string>?> _warnings = new();

    /// <inheritdoc />
    public void Write(string message) { }

    /// <inheritdoc />
    public void WriteLine(string message) { }

    /// <inheritdoc />
    public void WriteInfo(string message) { }

    /// <inheritdoc />
    public void WriteError(string message) { }

    /// <inheritdoc />
    public void WriteWarning(string message)
    {
        // Only collected when a scope is active for the current flow (after ClearWarnings); the
        // list reference is stable within the flow, so mutating it is visible to the drainer.
        _warnings.Value?.Add(message);
    }

    /// <inheritdoc />
    public void WriteSuccess(string message) { }

    /// <inheritdoc />
    public void WriteMarkup(string markup) { }

    /// <inheritdoc />
    public Task<string> PromptSelectionAsync(string title, IEnumerable<string> choices,
        CancellationToken cancellationToken = default)
    {
        var first = choices.FirstOrDefault()
                    ?? throw new InvalidOperationException($"No choices available for prompt '{title}'.");
        return Task.FromResult(first);
    }

    /// <inheritdoc />
    public Task RunWithStatusAsync(string statusMessage, Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        return action();
    }

    /// <summary>
    /// Starts a fresh warning collection for the current async flow. Call before an operation to
    /// scope the subsequent <see cref="DrainWarnings"/> to that operation.
    /// </summary>
    public void ClearWarnings()
    {
        _warnings.Value = [];
    }

    /// <summary>
    /// Returns the warnings collected for the current async flow and ends the collection.
    /// </summary>
    /// <returns>The warnings collected since the last <see cref="ClearWarnings"/>.</returns>
    public IReadOnlyList<string> DrainWarnings()
    {
        var collected = _warnings.Value;
        _warnings.Value = null;
        return collected is null ? [] : collected.ToArray();
    }
}
