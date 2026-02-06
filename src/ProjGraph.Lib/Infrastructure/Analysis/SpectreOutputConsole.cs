using ProjGraph.Lib.Application.Interfaces;
using Spectre.Console;

namespace ProjGraph.Lib.Infrastructure.Analysis;

/// <summary>
/// Spectre.Console implementation of IOutputConsole.
/// </summary>
public class SpectreOutputConsole : IOutputConsole
{
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
    /// Writes an error message to the console in red color.
    /// </summary>
    /// <param name="message">The error message to write.</param>
    public void WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red]Error: {message}[/]");
    }

    /// <summary>
    /// Writes a warning message to the console in yellow color.
    /// </summary>
    /// <param name="message">The warning message to write.</param>
    public void WriteWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]Warning: {message}[/]");
    }

    /// <summary>
    /// Writes a success message to the console in green color.
    /// </summary>
    /// <param name="message">The success message to write.</param>
    public void WriteSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]{message}[/]");
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