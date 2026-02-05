using Microsoft.Build.Construction;
using ProjGraph.Lib.Application.Interfaces;

namespace ProjGraph.Lib.Infrastructure.Parsers;

/// <summary>
/// Provides functionality to parse solution files and extract project paths.
/// </summary>
public sealed class SlnParser : ISlnParser
{
    /// <summary>
    /// Retrieves the paths of all projects in the specified solution file.
    /// </summary>
    /// <param name="slnPath">The file path to the solution file.</param>
    /// <returns>
    /// An enumerable collection of project file paths contained in the solution.
    /// If the solution file does not exist, an empty collection is returned.
    /// </returns>
    public IEnumerable<string> GetProjectPaths(string slnPath)
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
