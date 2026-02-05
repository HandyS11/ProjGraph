namespace ProjGraph.Lib.Application.Interfaces;

/// <summary>
/// Provides an abstraction for console output to improve testability and decouple from Spectre.Console.
/// </summary>
public interface IOutputConsole
{
    void Write(string message);
    void WriteLine(string message);
    void WriteError(string message);
    void WriteWarning(string message);
    void WriteSuccess(string message);
    void WriteMarkup(string markup);
}
