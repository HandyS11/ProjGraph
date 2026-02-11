namespace ProjGraph.Lib.Core.Abstractions;

/// <summary>
/// Provides an abstraction for console output to improve testability and decouple from Spectre.Console.
/// </summary>
public interface IOutputConsole
{
    /// <summary>
    /// Writes a message to the console without a newline.
    /// </summary>
    /// <param name="message">The message to write.</param>
    void Write(string message);

    /// <summary>
    /// Writes a message to the console followed by a newline.
    /// </summary>
    /// <param name="message">The message to write.</param>
    void WriteLine(string message);

    /// <summary>
    /// Writes an informational message to the console.
    /// </summary>
    /// <param name="message">The informational message to write.</param>
    void WriteInfo(string message);

    /// <summary>
    /// Writes an error message to the console.
    /// </summary>
    /// <param name="message">The error message to write.</param>
    void WriteError(string message);

    /// <summary>
    /// Writes a warning message to the console.
    /// </summary>
    /// <param name="message">The warning message to write.</param>
    void WriteWarning(string message);

    /// <summary>
    /// Writes a success message to the console.
    /// </summary>
    /// <param name="message">The success message to write.</param>
    void WriteSuccess(string message);

    /// <summary>
    /// Writes a message with markup to the console.
    /// </summary>
    /// <param name="markup">The markup content to write.</param>
    void WriteMarkup(string markup);

    /// <summary>
    /// Displays a selection prompt and returns the user's choice.
    /// </summary>
    /// <param name="title">The prompt title.</param>
    /// <param name="choices">The available choices.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The selected choice.</returns>
    Task<string> PromptSelectionAsync(string title, IEnumerable<string> choices,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays a status spinner while executing an asynchronous action.
    /// </summary>
    /// <param name="statusMessage">The status message to display.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    Task RunWithStatusAsync(string statusMessage, Func<Task> action, CancellationToken cancellationToken = default);
}
