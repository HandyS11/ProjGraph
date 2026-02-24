using ProjGraph.Lib.Core.Abstractions;
using Spectre.Console;

namespace ProjGraph.Lib.Core.Infrastructure;

/// <summary>
/// Spectre.Console implementation of IOutputConsole.
/// </summary>
public class SpectreOutputConsole : IOutputConsole
{
    /// <summary>
    /// Cached <see cref="IAnsiConsole"/> instance that writes to the standard error stream.
    /// Invalidated automatically when <see cref="Console.Error"/> changes (e.g., during tests).
    /// </summary>
#pragma warning disable IDE0032
    private static IAnsiConsole? _cachedStderr;
#pragma warning restore IDE0032

    /// <summary>
    /// The <see cref="TextWriter"/> that was active when <see cref="_cachedStderr"/> was created.
    /// Used to detect when <see cref="Console.Error"/> has been swapped.
    /// </summary>
    private static TextWriter? _cachedErrorWriter;

    /// <summary>
    /// Gets an <see cref="IAnsiConsole"/> that writes to the current standard error stream.
    /// The console is cached and reused as long as <see cref="Console.Error"/> has not changed.
    /// </summary>
    private static IAnsiConsole Stderr
    {
        get
        {
            if (_cachedStderr is not null && _cachedErrorWriter == Console.Error)
            {
                return _cachedStderr;
            }

            _cachedErrorWriter = Console.Error;
            var globalConsole = AnsiConsole.Console;
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = globalConsole.Profile.Capabilities.Ansi ? AnsiSupport.Yes : AnsiSupport.No,
                ColorSystem = ColorSystemSupport.Detect,
                Out = new AnsiConsoleOutput(Console.Error)
            });
            console.Profile.Capabilities.Unicode = globalConsole.Profile.Capabilities.Unicode;
            console.Profile.Width = globalConsole.Profile.Width;
            _cachedStderr = console;

            return _cachedStderr;
        }
    }

    /// <summary>
    /// Writes a message to the console without a newline.
    /// </summary>
    /// <param name="message">The message to write.</param>
    public void Write(string message)
    {
        AnsiConsole.Write(message);
    }

    /// <summary>
    /// Writes a message to the console followed by a newline.
    /// </summary>
    /// <param name="message">The message to write.</param>
    public void WriteLine(string message)
    {
        AnsiConsole.WriteLine(message);
    }

    /// <summary>
    /// Writes an informational message to the console.
    /// </summary>
    /// <param name="message">The informational message to write.</param>
    public void WriteInfo(string message)
    {
        Stderr.WriteLine(message);
    }

    /// <summary>
    /// Writes an error message to the console in red color.
    /// </summary>
    /// <param name="message">The error message to write.</param>
    public void WriteError(string message)
    {
        Stderr.MarkupLine($"[red]Error: {Markup.Escape(message)}[/]");
    }

    /// <summary>
    /// Writes a warning message to the console in yellow color.
    /// </summary>
    /// <param name="message">The warning message to write.</param>
    public void WriteWarning(string message)
    {
        Stderr.MarkupLine($"[yellow]Warning: {Markup.Escape(message)}[/]");
    }

    /// <summary>
    /// Writes a success message to the console in green color.
    /// </summary>
    /// <param name="message">The success message to write.</param>
    public void WriteSuccess(string message)
    {
        Stderr.MarkupLine($"[green]{Markup.Escape(message)}[/]");
    }

    /// <summary>
    /// Writes a message with markup to the console.
    /// </summary>
    /// <param name="markup">The markup content to write.</param>
    public void WriteMarkup(string markup)
    {
        AnsiConsole.MarkupLine(markup);
    }

    /// <inheritdoc />
    public async Task<string> PromptSelectionAsync(string title, IEnumerable<string> choices,
        CancellationToken cancellationToken = default)
    {
        return await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title(title)
                .AddChoices(choices),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task RunWithStatusAsync(string statusMessage, Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(statusMessage, async ctx =>
            {
                await using var registration = cancellationToken.Register(() => ctx.Status("Cancelling..."));
                cancellationToken.ThrowIfCancellationRequested();
                await action();
            });
    }
}
