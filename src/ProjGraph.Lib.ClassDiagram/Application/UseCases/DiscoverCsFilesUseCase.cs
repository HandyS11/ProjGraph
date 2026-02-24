using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;

namespace ProjGraph.Lib.ClassDiagram.Application.UseCases;

/// <summary>
/// Use case for discovering all C# source files in a directory recursively, excluding standard artifact and noise folders.
/// </summary>
/// <param name="fileSystem">The file system abstraction.</param>
public class DiscoverCsFilesUseCase(IFileSystem fileSystem)
{
    /// <summary>
    /// Executes the discovery of C# source files within the specified directory path.
    /// </summary>
    /// <param name="directoryPath">The root directory path to start the discovery from.</param>
    /// <returns>An enumerable of absolute paths to discovered C# files.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory does not exist.</exception>
    public virtual IReadOnlyList<string> Execute(string directoryPath)
    {
        if (!fileSystem.DirectoryExists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var startPath = fileSystem.GetFullPath(directoryPath);
        var discoveredFiles = new List<string>();
        var toProcess = new Queue<string>();

        toProcess.Enqueue(startPath);

        while (toProcess.Count > 0)
        {
            var currentDir = toProcess.Dequeue();

            // Handle standard excludes
            if (DirectoryFilters.ShouldSkipDirectory(currentDir))
            {
                continue;
            }

            try
            {
                // Collect .cs files
                discoveredFiles.AddRange(fileSystem.GetFiles(currentDir, $"*{DirectoryFilters.CSharpExtension}"));

                // Enqueue subdirectories
                foreach (var dir in fileSystem.GetDirectories(currentDir))
                {
                    toProcess.Enqueue(dir);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we cannot access
            }
            catch (IOException)
            {
                // Just skip directories we cannot access
            }
        }

        return discoveredFiles;
    }
}
