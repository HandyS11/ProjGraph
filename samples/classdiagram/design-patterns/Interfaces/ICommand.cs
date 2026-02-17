namespace DesignPatterns.Interfaces;

/// <summary>
/// Command pattern
/// </summary>
public interface ICommand
{
    Task ExecuteAsync();
    bool CanExecute();
}

public interface ICommandFactory
{
    ICommand CreateCommand(string commandName, object? parameters = null);
}
