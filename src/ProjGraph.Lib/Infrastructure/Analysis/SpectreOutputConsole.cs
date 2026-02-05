using ProjGraph.Lib.Application.Interfaces;
using Spectre.Console;

namespace ProjGraph.Lib.Infrastructure.Analysis;

/// <summary>
/// Spectre.Console implementation of IOutputConsole.
/// </summary>
public class SpectreOutputConsole : IOutputConsole
{
    public void Write(string message)
    {
        AnsiConsole.Write(message);
    }

    public void WriteLine(string message)
    {
        AnsiConsole.WriteLine(message);
    }

    public void WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red]Error: {message}[/]");
    }

    public void WriteWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]Warning: {message}[/]");
    }

    public void WriteSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]{message}[/]");
    }

    public void WriteMarkup(string markup)
    {
        AnsiConsole.MarkupLine(markup);
    }
}
