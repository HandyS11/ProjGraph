namespace ProjGraph.Lib.ClassDiagram.Application.UseCases;

/// <summary>
/// Discovers all C# source files in a directory recursively, excluding standard artifact and noise folders.
/// </summary>
public interface IDiscoverCsFilesUseCase
{
    /// <summary>
    /// Executes the discovery of C# source files within the specified directory path.
    /// </summary>
    /// <param name="directoryPath">The root directory path to start the discovery from.</param>
    /// <returns>An enumerable of absolute paths to discovered C# files.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory does not exist.</exception>
    IReadOnlyList<string> Execute(string directoryPath);
}
