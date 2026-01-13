using System.Xml.Linq;

namespace ProjGraph.Lib.Parsers;

public class SlnxParser
{
    public IEnumerable<string> GetProjectPaths(string slnxPath)
    {
        if (!File.Exists(slnxPath))
            return [];

        var doc = XDocument.Load(slnxPath);
        var solutionDir = Path.GetDirectoryName(slnxPath) ?? "";

        return doc.Descendants("Project")
            .Select(x => x.Attribute("Path")?.Value)
            .Where(path => path != null)
            .Select(path => Path.GetFullPath(Path.Combine(solutionDir, path!)));
    }
}
