using Microsoft.Build.Construction;
using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace ProjGraph.Lib.Core.Parsers;

/// <summary>
/// Provides functionality to parse project files and extract project details and references.
/// </summary>
/// <param name="fileSystem">The file system abstraction for file operations.</param>
public sealed class ProjectParser(IFileSystem fileSystem) : IProjectParser
{
    /// <summary>
    /// Parses the specified project file and extracts project details and its references.
    /// </summary>
    /// <param name="projectPath">The file path to the project file to be parsed.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    /// <item>
    /// <description>The parsed <see cref="Project"/> object with details such as ID, name, path, framework, and type.</description>
    /// </item>
    /// <item>
    /// <description>A collection of project references as strings.</description>
    /// </item>
    /// </list>
    /// </returns>
    /// <exception cref="ParsingException">Thrown when the project file cannot be parsed.</exception>
    public (Project Project, IEnumerable<string> ProjectReferences) Parse(string projectPath)
    {
        var root = ProjectRootElement.Open(projectPath)
                   ?? throw new ParsingException($"Failed to parse project file: {projectPath}");

        var name = Path.GetFileNameWithoutExtension(projectPath);
        var relativePath = Path.GetRelativePath(fileSystem.GetCurrentDirectory(), projectPath);

        // Fast extraction of properties
        var framework = root.Properties.FirstOrDefault(p => p.Name == "TargetFramework")?.Value ??
                        root.Properties.FirstOrDefault(p => p.Name == "TargetFrameworks")?.Value ?? "unknown";
        var outputType = root.Properties.FirstOrDefault(p => p.Name == "OutputType")?.Value ?? "";

        var type = outputType.Contains("Exe", StringComparison.OrdinalIgnoreCase)
            ? ProjectType.Executable
            : ProjectType.Library;

        if (name.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            root.Properties.Any(p =>
                p.Name == "IsTestProject" && p.Value.Equals("true", StringComparison.OrdinalIgnoreCase)))
        {
            type = ProjectType.Test;
        }

        var id = GenerateDeterministicId(projectPath);
        var project = new Project(id, name, projectPath, relativePath, framework, type);

        var projectReferences = root.Items
            .Where(i => i.ItemType == "ProjectReference")
            .Select(i => i.Include)
            .ToList();

        return (project, projectReferences);
    }

    /// <summary>
    /// Generates a deterministic GUID from the normalized absolute path of the project file.
    /// Parsing the same project twice will always yield the same ID.
    /// </summary>
    /// <param name="projectPath">The file path of the project.</param>
    /// <returns>A deterministic <see cref="Guid"/> derived from the normalized path.</returns>
    private static Guid GenerateDeterministicId(string projectPath)
    {
        var normalizedPath = Path.GetFullPath(projectPath)
            .Replace('\\', '/')
            .ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));
        return new Guid(hash.AsSpan(0, 16));
    }
}
