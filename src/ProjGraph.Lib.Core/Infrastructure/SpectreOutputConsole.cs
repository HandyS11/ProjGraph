using ProjGraph.Lib.Core.Abstractions;
using Spectre.Console;

namespace ProjGraph.Lib.Core.Infrastructure;

/// <summary>
/// Spectre.Console implementation of IOutputConsole.
/// </summary>
public class SpectreOutputConsole : IOutputConsole
{
    /// <summary>
    /// Gets an <see cref="IAnsiConsole"/> that writes to the current standard error stream.
    /// </summary>
    private static IAnsiConsole Stderr
    {
        get
        {
            var globalConsole = AnsiConsole.Console;
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = globalConsole.Profile.Capabilities.Ansi ? AnsiSupport.Yes : AnsiSupport.No,
                ColorSystem = ColorSystemSupport.Detect,
                Out = new AnsiConsoleOutput(Console.Error)
            });
            console.Profile.Capabilities.Unicode = globalConsole.Profile.Capabilities.Unicode;
            console.Profile.Width = globalConsole.Profile.Width;
            return console;
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
        Stderr.MarkupLine($"[red]Error: {message}[/]");
    }

    /// <summary>
    /// Writes a warning message to the console in yellow color.
    /// </summary>
    /// <param name="message">The warning message to write.</param>
    public void WriteWarning(string message)
    {
        Stderr.MarkupLine($"[yellow]Warning: {message}[/]");
    }

    /// <summary>
    /// Writes a success message to the console in green color.
    /// </summary>
    /// <param name="message">The success message to write.</param>
    public void WriteSuccess(string message)
    {
        Stderr.MarkupLine($"[green]{message}[/]");
    }

    /// <summary>
    /// Writes a message with markup to the console.
    /// </summary>
    /// <param name="markup">The markup content to write.</param>
    public void WriteMarkup(string markup)
    {
        AnsiConsole.MarkupLine(markup);
    }
}