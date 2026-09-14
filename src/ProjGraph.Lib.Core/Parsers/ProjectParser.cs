using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ProjGraph.Lib.Core.Parsers;

/// <summary>
/// Provides functionality to parse project files and extract project details and references.
/// </summary>
/// <param name="fileSystem">The file system abstraction for file operations.</param>
public sealed class ProjectParser(IFileSystem fileSystem) : IProjectParser
{
    /// <summary>The legacy MSBuild 2003 XML namespace, still used by non-SDK-style projects.</summary>
    private const string MsBuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

    /// <summary>The item operations of which an item outside a <c>Target</c> must define one.</summary>
    private static readonly XName[] ItemOperations = ["Include", "Update", "Remove"];

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
        XDocument root;
        try
        {
            root = LoadXml(projectPath);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException
                                       or InvalidDataException)
        {
            throw new ParsingException($"Failed to parse project file: {projectPath}", ex);
        }

        var name = Path.GetFileNameWithoutExtension(projectPath);
        var relativePath = Path.GetRelativePath(fileSystem.GetCurrentDirectory(), projectPath);

        // Fast extraction of properties from the project itself (a null-or-whitespace value counts
        // as "not defined" so it falls back to Directory.Build.props / "unknown").
        var ownFramework = GetPropertyValue(root, "TargetFramework") ?? GetPropertyValue(root, "TargetFrameworks");
        var ownOutputType = GetPropertyValue(root, "OutputType");
        var ownIsTestProject = GetPropertyValue(root, "IsTestProject");

        // Repos commonly set these centrally in Directory.Build.props; fall back to it (a single
        // walk up the tree) only for the values the project does not define locally.
        Dictionary<string, string>? inherited = null;
        if (ownFramework is null || ownOutputType is null || ownIsTestProject is null)
        {
            inherited = ResolveInheritedProperties(projectPath,
                ["TargetFramework", "TargetFrameworks", "OutputType", "IsTestProject"]);
        }

        var framework = ownFramework
                        ?? inherited?.GetValueOrDefault("TargetFramework")
                        ?? inherited?.GetValueOrDefault("TargetFrameworks")
                        ?? "unknown";
        var outputType = ownOutputType ?? inherited?.GetValueOrDefault("OutputType") ?? "";
        var isTestProject = ownIsTestProject ?? inherited?.GetValueOrDefault("IsTestProject");

        var type = outputType.Contains("Exe", StringComparison.OrdinalIgnoreCase)
            ? ProjectType.Executable
            : ProjectType.Library;

        if (name.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(isTestProject, "true", StringComparison.OrdinalIgnoreCase))
        {
            type = ProjectType.Test;
        }

        var id = GenerateDeterministicId(projectPath);
        var project = new Project(id, name, projectPath, relativePath, framework, type);

        var projectReferences = GetItems(root, "ProjectReference")
            .Select(GetInclude)
            .ToList();

        var packageReferences = GetItems(root, "PackageReference")
            .Select(item =>
            {
                var include = GetInclude(item);
                var version = GetMetadataValue(item, "Version");
                if (string.IsNullOrEmpty(version))
                {
                    version = ResolveCentralPackageVersion(projectPath, include);
                }

                return new PackageReference(include, version ?? "unknown");
            })
            .ToList();

        return (project, projectReferences, packageReferences);
    }

    /// <summary>
    /// Reads and parses an MSBuild XML file through the file-system abstraction, rejecting the
    /// structural errors MSBuild itself rejects: a root element other than <c>Project</c>, a
    /// namespace other than none or the MSBuild 2003 namespace, and an item outside a
    /// <c>Target</c> without a non-empty <c>Include</c>, <c>Update</c>, or <c>Remove</c>.
    /// </summary>
    /// <param name="path">The project or props file path.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="XmlException">Thrown when the file is not well-formed XML.</exception>
    /// <exception cref="InvalidDataException">Thrown when the XML is not a valid MSBuild project.</exception>
    private XDocument LoadXml(string path)
    {
        var document = XDocument.Parse(fileSystem.ReadAllText(path));
        var root = document.Root!;
        if (root.Name.LocalName != "Project" ||
            root.Name.NamespaceName is not ("" or MsBuildNamespace))
        {
            throw new InvalidDataException($"'{path}' is not an MSBuild project: unexpected root element {root.Name}.");
        }

        var invalidItem = document.Descendants()
            .Where(e => e.Parent?.Name.LocalName == "ItemGroup" && !e.Ancestors().Any(a => a.Name.LocalName == "Target"))
            .FirstOrDefault(e =>
            {
                var operations = ItemOperations.Select(e.Attribute).OfType<XAttribute>().ToList();
                return operations.Count == 0 || operations.Exists(a => a.Value.Length == 0);
            });
        if (invalidItem is not null)
        {
            throw new InvalidDataException(
                $"'{path}' is not a valid MSBuild project: <{invalidItem.Name.LocalName}> needs a non-empty Include, Update, or Remove.");
        }

        return document;
    }

    /// <summary>
    /// Reads a single MSBuild property value using a case-insensitive name match (MSBuild property
    /// names are case-insensitive). A property is any element whose parent is a
    /// <c>PropertyGroup</c>, wherever that group sits (including inside <c>Target</c> and
    /// <c>Choose</c>), and the first one in document order wins. A null-or-whitespace value is
    /// treated as undefined and returns <see langword="null"/>.
    /// </summary>
    /// <param name="document">The project or props document to read from.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property value, or <see langword="null"/> when unset or whitespace.</returns>
    private static string? GetPropertyValue(XDocument document, string name)
    {
        var value = document.Descendants()
            .FirstOrDefault(e => e.Parent?.Name.LocalName == "PropertyGroup" &&
                                 string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Returns the items of the given type: elements whose parent is an <c>ItemGroup</c> and whose
    /// name matches <paramref name="itemType"/> exactly (item types are matched case-sensitively).
    /// </summary>
    /// <param name="document">The project or props document to read from.</param>
    /// <param name="itemType">The item type, e.g. <c>PackageReference</c>.</param>
    /// <returns>The matching item elements in document order.</returns>
    private static IEnumerable<XElement> GetItems(XDocument document, string itemType)
    {
        return document.Descendants()
            .Where(e => e.Parent?.Name.LocalName == "ItemGroup" && e.Name.LocalName == itemType);
    }

    /// <summary>
    /// Returns the item's <c>Include</c> attribute, or an empty string for <c>Update</c>/<c>Remove</c> items.
    /// </summary>
    /// <param name="item">The item element.</param>
    /// <returns>The include value.</returns>
    private static string GetInclude(XElement item)
    {
        return item.Attribute("Include")?.Value ?? "";
    }

    /// <summary>
    /// Reads item metadata expressed either as an attribute or as a child element. Metadata names
    /// are matched case-sensitively.
    /// </summary>
    /// <param name="item">The item element.</param>
    /// <param name="name">The metadata name, e.g. <c>Version</c>.</param>
    /// <returns>The metadata value, or <see langword="null"/> when absent.</returns>
    private static string? GetMetadataValue(XElement item, string name)
    {
        return item.Attribute(name)?.Value
               ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
    }

    /// <summary>
    /// Resolves MSBuild properties inherited from <c>Directory.Build.props</c> for a project that
    /// does not define them locally. Walks up the directory tree from the project file, nearest
    /// first, recording the first value found for each requested property.
    /// </summary>
    /// <param name="projectPath">The path to the project file.</param>
    /// <param name="names">The property names to resolve.</param>
    /// <returns>A map of the resolved property names to their inherited values.</returns>
    private Dictionary<string, string> ResolveInheritedProperties(
        string projectPath,
        IReadOnlyCollection<string> names)
    {
        // Property names are matched case-insensitively (MSBuild semantics).
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var directory = Path.GetDirectoryName(Path.GetFullPath(projectPath));

        while (directory is not null && result.Count < names.Count)
        {
            MergeInheritedProperties(Path.Combine(directory, "Directory.Build.props"), names, result);
            directory = Path.GetDirectoryName(directory);
        }

        return result;
    }

    /// <summary>
    /// Merges the requested properties found in a single <c>Directory.Build.props</c> file into
    /// <paramref name="result"/>, keeping the nearest (first-seen) value for each name. A missing or
    /// unreadable props file contributes nothing.
    /// </summary>
    /// <param name="propsFile">The <c>Directory.Build.props</c> path to read.</param>
    /// <param name="names">The property names still being resolved.</param>
    /// <param name="result">The accumulator of resolved property values, augmented in place.</param>
    private void MergeInheritedProperties(
        string propsFile,
        IReadOnlyCollection<string> names,
        Dictionary<string, string> result)
    {
        if (!fileSystem.FileExists(propsFile))
        {
            return;
        }

        XDocument propsRoot;
        try
        {
            propsRoot = LoadXml(propsFile);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException
                                       or InvalidDataException)
        {
            // If we can't read the props file, continue searching up.
            return;
        }

        foreach (var propertyName in names)
        {
            if (result.ContainsKey(propertyName))
            {
                continue;
            }

            var value = GetPropertyValue(propsRoot, propertyName);
            if (value is not null)
            {
                result[propertyName] = value;
            }
        }
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
                    var propsRoot = LoadXml(propsFile);
                    var packageVersion = GetItems(propsRoot, "PackageVersion")
                        .FirstOrDefault(i => GetInclude(i).Equals(packageName, StringComparison.OrdinalIgnoreCase));
                    var version = packageVersion is null ? null : GetMetadataValue(packageVersion, "Version");

                    if (!string.IsNullOrEmpty(version))
                    {
                        return version;
                    }
                }
                catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException
                                               or InvalidDataException)
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
