using Microsoft.Build.Construction;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Lib.Core.Parsers;

/// <summary>
/// Provides functionality to parse solution files and extract project paths.
/// </summary>
/// <param name="fileSystem">The file system abstraction for file operations.</param>
public sealed class SlnParser(IFileSystem fileSystem) : ISlnParser
{
    /// <summary>
    /// Retrieves the paths of all projects in the specified solution file.
    /// </summary>
    /// <param name="path">The file path to the solution file.</param>
    /// <returns>
    /// An enumerable collection of project file paths contained in the solution.
    /// If the solution file does not exist, an empty collection is returned.
    /// </returns>
    public IEnumerable<string> GetProjectPaths(string path)
    {
        if (!fileSystem.FileExists(path))
        {
            return [];
        }

        var fullPath = fileSystem.GetFullPath(path);
        var slnFile = SolutionFile.Parse(fullPath);

        return slnFile.ProjectsInOrder
            .Where(p => p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
            .Select(p => p.AbsolutePath);
    }
}
