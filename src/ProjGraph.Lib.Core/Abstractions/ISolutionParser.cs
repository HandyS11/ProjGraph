namespace ProjGraph.Lib.Core.Abstractions;

/// <summary>
/// Defines a contract for parsing solution files and extracting project paths.
/// </summary>
public interface ISolutionParser
{
    /// <summary>
    /// Retrieves the paths of all projects in the specified solution file.
    /// </summary>
    /// <param name="path">The file path to the solution file.</param>
    /// <returns>An enumerable collection of project file paths contained in the solution.</returns>
    IEnumerable<string> GetProjectPaths(string path);
}




