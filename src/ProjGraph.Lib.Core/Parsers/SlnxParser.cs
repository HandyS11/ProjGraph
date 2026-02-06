using ProjGraph.Lib.Core.Abstractions;
using System.Xml.Linq;

namespace ProjGraph.Lib.Core.Parsers;

/// <summary>
/// Provides functionality to parse `.slnx` files and extract project paths.
/// </summary>
public sealed class SlnxParser(IFileSystem fileSystem) : ISlnxParser
{
    /// <summary>
    /// Retrieves the paths of all projects in the specified `.slnx` file.
    /// </summary>
    /// <param name="path">The file path to the `.slnx` file.</param>
    /// <returns>
    /// An enumerable collection of project file paths contained in the `.slnx` file.
    /// If the `.slnx` file does not exist, an empty collection is returned.
    /// </returns>
    public IEnumerable<string> GetProjectPaths(string path)
    {
        if (!fileSystem.FileExists(path))
        {
            return [];
        }

        var doc = XDocument.Load(path);
        var solutionDir = fileSystem.GetDirectoryName(path) ?? "";

        return doc.Descendants("Project")
            .Select(x => x.Attribute("Path")?.Value)
            .Where(p => p != null)
            .Select(p =>
            {
                // Normalize path separators to be platform-appropriate before combining
                var normalizedPath = p!.Replace('\\', Path.DirectorySeparatorChar);
                return fileSystem.GetFullPath(fileSystem.Combine(solutionDir, normalizedPath));
            });
    }
}





