using ProjGraph.Core.Exceptions;
using ProjGraph.Lib.Core.Abstractions;
using System.Xml;
using System.Xml.Linq;

namespace ProjGraph.Lib.Core.Parsers;

/// <summary>
/// Provides functionality to parse `.slnx` files and extract project paths.
/// </summary>
/// <param name="fileSystem">The file system abstraction for file operations.</param>
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
    /// <exception cref="ParsingException">Thrown when the `.slnx` file contains malformed XML.</exception>
    public IEnumerable<string> GetProjectPaths(string path)
    {
        if (!fileSystem.FileExists(path))
        {
            return [];
        }

        XDocument doc;
        try
        {
            doc = XDocument.Load(path);
        }
        catch (XmlException ex)
        {
            throw new ParsingException($"Malformed .slnx file: {path}", ex);
        }

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
