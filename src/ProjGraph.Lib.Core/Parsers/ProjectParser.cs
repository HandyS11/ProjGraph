using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

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
    /// <item>
    /// <description>A collection of NuGet package references as <see cref="PackageReference"/> objects.</description>
    /// </item>
    /// </list>
    /// </returns>
    /// <exception cref="ParsingException">Thrown when the project file cannot be parsed.</exception>
    public (Project Project, IEnumerable<string> ProjectReferences, IEnumerable<PackageReference> PackageReferences)
        Parse(string projectPath)
    {
        ProjectRootElement root;
        try
        {
            root = ProjectRootElement.Open(projectPath)
                   ?? throw new ParsingException($"Failed to parse project file: {projectPath}");
        }
        catch (Exception ex) when (ex is InvalidProjectFileException or IOException or XmlException
                                       or InvalidOperationException)
        {
            throw new ParsingException($"Failed to parse project file: {projectPath}", ex);
        }

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

        var packageReferences = root.Items
            .Where(i => i.ItemType == "PackageReference")
            .Select(i =>
            {
                var version = i.Metadata.FirstOrDefault(m => m.Name == "Version")?.Value;
                if (string.IsNullOrEmpty(version))
                {
                    version = ResolveCentralPackageVersion(projectPath, i.Include);
                }

                return new PackageReference(i.Include, version ?? "unknown");
            })
            .ToList();

        return (project, projectReferences, packageReferences);
    }

    /// <summary>
    /// Resolves the version of a NuGet package from a <c>Directory.Packages.props</c> file
    /// when Central Package Management is used (i.e., no Version attribute on the PackageReference).
    /// Walks up the directory tree from the project file until a matching props file is found.
    /// </summary>
    /// <param name="projectPath">The path to the project file.</param>
    /// <param name="packageName">The package name to look up.</param>
    /// <returns>The resolved version string, or <see langword="null"/> if not found.</returns>
    private string? ResolveCentralPackageVersion(string projectPath, string packageName)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(projectPath));

        while (directory is not null)
        {
            var propsFile = Path.Combine(directory, "Directory.Packages.props");
            if (fileSystem.FileExists(propsFile))
            {
                try
                {
                    var propsRoot = ProjectRootElement.Open(propsFile);
                    var version = propsRoot?.Items
                        .FirstOrDefault(i =>
                            i.ItemType == "PackageVersion" &&
                            i.Include.Equals(packageName, StringComparison.OrdinalIgnoreCase))
                        ?.Metadata.FirstOrDefault(m => m.Name == "Version")
                        ?.Value;

                    if (!string.IsNullOrEmpty(version))
                    {
                        return version;
                    }
                }
                catch (Exception ex) when (ex is InvalidProjectFileException or IOException
                                               or InvalidOperationException or XmlException)
                {
                    // If we can't read the props file, continue searching up
                }
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    /// <summary>
    /// Generates a deterministic GUID from the normalized absolute path of the project file.
    /// Parsing the same project twice will always yield the same ID.
    /// </summary>
    /// <param name="projectPath">The file path of the project.</param>
    /// <returns>A deterministic <see cref="Guid"/> derived from the normalized path.</returns>
    private static Guid GenerateDeterministicId(string projectPath)
    {
        var normalizedPath = NormalizePath(projectPath);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// Normalizes a file path for use in deterministic ID generation.
    /// On case-insensitive file systems (Windows/macOS), the path is case-folded.
    /// On case-sensitive file systems (Linux), the exact case is preserved to avoid collisions.
    /// Directory separators are normalized to forward slashes on all platforms.
    /// </summary>
    /// <param name="path">The file path to normalize.</param>
    /// <returns>The normalized path string.</returns>
    private static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path).Replace('\\', '/');

        // Only case-fold on case-insensitive file systems (Windows and macOS)
        // Linux file systems are typically case-sensitive, so preserve exact case
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? fullPath.ToUpperInvariant()
            : fullPath;
    }
}
