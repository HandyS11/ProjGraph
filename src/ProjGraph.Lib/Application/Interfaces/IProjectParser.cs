using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Application.Interfaces;

/// <summary>
/// Defines a contract for parsing project files and extracting project details and references.
/// </summary>
public interface IProjectParser
{
    /// <summary>
    /// Parses the specified project file and extracts project details and its references.
    /// </summary>
    /// <param name="projectPath">The file path to the project file to be parsed.</param>
    /// <returns>A tuple containing the parsed <see cref="Project"/> and a collection of its project references.</returns>
    (Project Project, IEnumerable<string> ProjectReferences) Parse(string projectPath);
}