using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Mcp;

/// <summary>
/// An <see cref="IOutputConsole"/> for the MCP server that collects warning messages in memory
/// instead of writing to stdout (which is reserved for the JSON-RPC transport). Collected warnings
/// can be drained and surfaced in a tool's result, so skipped/partial analysis is no longer silent.
/// All other output is discarded, exactly like <c>NullOutputConsole</c>.
/// </summary>
internal sealed class CollectingOutputConsole : IOutputConsole
{
    private readonly Lock _gate = new();
    private readonly List<string> _warnings = [];

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
        lock (_gate)
        {
            _warnings.Add(message);
        }
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
    /// Clears any collected warnings. Call before an operation to scope the subsequent
    /// <see cref="DrainWarnings"/> to that operation.
    /// </summary>
    public void ClearWarnings()
    {
        lock (_gate)
        {
            _warnings.Clear();
        }
    }

    /// <summary>
    /// Returns the collected warnings and clears the buffer.
    /// </summary>
    /// <returns>The warnings collected since the last clear/drain.</returns>
    public IReadOnlyList<string> DrainWarnings()
    {
        lock (_gate)
        {
            var drained = _warnings.ToArray();
            _warnings.Clear();
            return drained;
        }
    }
}
