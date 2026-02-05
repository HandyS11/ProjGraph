using ProjGraph.Lib.Application.Interfaces;
using System.Xml.Linq;

namespace ProjGraph.Lib.Infrastructure.Parsers;

/// <summary>
/// Provides functionality to parse `.slnx` files and extract project paths.
/// </summary>
public sealed class SlnxParser : ISlnxParser
{
    /// <summary>
    /// Retrieves the paths of all projects in the specified `.slnx` file.
    /// </summary>
    /// <param name="slnxPath">The file path to the `.slnx` file.</param>
    /// <returns>
    /// An enumerable collection of project file paths contained in the `.slnx` file.
    /// If the `.slnx` file does not exist, an empty collection is returned.
    /// </returns>
    public IEnumerable<string> GetProjectPaths(string slnxPath)
    {
        if (!File.Exists(slnxPath))
        {
            return [];
        }

        var doc = XDocument.Load(slnxPath);
        var solutionDir = Path.GetDirectoryName(slnxPath) ?? "";

        return doc.Descendants("Project")
            .Select(x => x.Attribute("Path")?.Value)
            .Where(path => path != null)
            .Select(path =>
            {
                // Normalize path separators to be platform-appropriate before combining
                var normalizedPath = path!.Replace('\\', Path.DirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(solutionDir, normalizedPath));
            });
    }
}
