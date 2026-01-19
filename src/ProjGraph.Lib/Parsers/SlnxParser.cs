using System.Xml.Linq;

namespace ProjGraph.Lib.Parsers;

/// <summary>
/// Provides functionality to parse `.slnx` files and extract project paths.
/// </summary>
public static class SlnxParser
{
    /// <summary>
    /// Retrieves the paths of all projects in the specified `.slnx` file.
    /// </summary>
    /// <param name="slnxPath">The file path to the `.slnx` file.</param>
    /// <returns>
    /// An enumerable collection of project file paths contained in the `.slnx` file.
    /// If the `.slnx` file does not exist, an empty collection is returned.
    /// </returns>
    public static IEnumerable<string> GetProjectPaths(string slnxPath)
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
            .Select(path => Path.GetFullPath(Path.Combine(solutionDir, path!)));
    }
}
