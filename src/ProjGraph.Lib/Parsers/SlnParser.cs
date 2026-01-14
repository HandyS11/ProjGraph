using Microsoft.Build.Construction;

namespace ProjGraph.Lib.Parsers;

public static class SlnParser
{
    public static IEnumerable<string> GetProjectPaths(string slnPath)
    {
        if (!File.Exists(slnPath))
        {
            return [];
        }

        var slnFile = SolutionFile.Parse(slnPath);

        return slnFile.ProjectsInOrder
            .Where(p => p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
            .Select(p => p.AbsolutePath);
    }
}