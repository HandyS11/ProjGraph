using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using ProjGraph.Core.Exceptions;
using ProjGraph.Lib.Core.Abstractions;
using System.Xml;

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
    /// <exception cref="ParsingException">Thrown when the solution file cannot be parsed.</exception>
    public IEnumerable<string> GetProjectPaths(string path)
    {
        if (!fileSystem.FileExists(path))
        {
            return [];
        }

        var fullPath = fileSystem.GetFullPath(path);

        SolutionFile slnFile;
        try
        {
            slnFile = SolutionFile.Parse(fullPath);
        }
        catch (Exception ex) when (ex is InvalidProjectFileException or IOException or XmlException
                                       or InvalidOperationException or UnauthorizedAccessException)
        {
            throw new ParsingException($"Failed to read or parse .sln file: {path}", ex);
        }

        return slnFile.ProjectsInOrder
            .Where(p => p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
            .Select(p => p.AbsolutePath);
    }
}
