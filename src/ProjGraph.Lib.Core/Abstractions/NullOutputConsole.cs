namespace ProjGraph.Lib.Core.Abstractions;

/// <summary>
/// A no-op implementation of <see cref="IOutputConsole"/> that discards all output.
/// Useful in contexts where console output is not needed, such as MCP servers
/// (where stdout is reserved for JSON-RPC transport) or unit tests.
/// </summary>
public sealed class NullOutputConsole : IOutputConsole
{
    /// <inheritdoc />
    public void Write(string message) { }

    /// <inheritdoc />
    public void WriteLine(string message) { }

    /// <inheritdoc />
    public void WriteInfo(string message) { }

    /// <inheritdoc />
    public void WriteError(string message) { }

    /// <inheritdoc />
    public void WriteWarning(string message) { }

    /// <inheritdoc />
    public void WriteSuccess(string message) { }

    /// <inheritdoc />
    public void WriteMarkup(string markup) { }

    /// <inheritdoc />
    /// <remarks>Returns the first available choice without prompting, or throws if no choices are available.</remarks>
    public Task<string> PromptSelectionAsync(string title, IEnumerable<string> choices,
        CancellationToken cancellationToken = default)
    {
        var first = choices.FirstOrDefault()
                    ?? throw new InvalidOperationException($"No choices available for prompt '{title}'.");
        return Task.FromResult(first);
    }

    /// <inheritdoc />
    /// <remarks>Executes the action immediately without displaying status.</remarks>
    public Task RunWithStatusAsync(string statusMessage, Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        return action();
    }
}
