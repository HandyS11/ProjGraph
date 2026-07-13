using Microsoft.Extensions.Logging;
using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace ProjGraph.Lib.Dependencies.Application.UseCases;

/// <summary>
/// Use case for building a solution graph from a given file path.
/// </summary>
/// <param name="slnParser">The parser for .sln solution files.</param>
/// <param name="slnxParser">The parser for .slnx solution files.</param>
/// <param name="projectParser">The parser for individual project files.</param>
/// <param name="discoveryService">The service for discovering and resolving project references.</param>
/// <param name="fileSystem">The file system abstraction for file operations.</param>
/// <param name="console">The output console for displaying warnings.</param>
/// <param name="logger">The logger for diagnostic messages.</param>
public partial class BuildGraphUseCase(
    ISlnParser slnParser,
    ISlnxParser slnxParser,
    IProjectParser projectParser,
    IProjectDiscoveryService discoveryService,
    IFileSystem fileSystem,
    IOutputConsole console,
    ILogger<BuildGraphUseCase> logger)
{
    /// <summary>
    /// Executes the use case to build a solution graph from the specified file path.
    /// </summary>
    /// <param name="path">The file path to the solution or project file.</param>
    /// <param name="includePackages">Whether to include NuGet package dependencies in the graph.</param>
    /// <returns>A <see cref="ProjGraph.Core.Models.SolutionGraph"/> representing the solution structure.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown when the file type is unsupported.</exception>
    public SolutionGraph Execute(string path, bool includePackages = false)
    {
        if (!fileSystem.FileExists(path))
        {
            throw new FileNotFoundException($"The specified file does not exist: {path}", path);
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();

        var projectFilePaths = extension switch
        {
            ".slnx" => slnxParser.GetProjectPaths(path),
            ".sln" => slnParser.GetProjectPaths(path),
            ".csproj" => discoveryService.DiscoverProjectsRecursively(path),
            _ => throw new ArgumentException("Unsupported file type: must be .sln, .slnx, or .csproj", nameof(path))
        };

        var projects = new List<Project>();
        var dependencies = new List<Dependency>();

        // Match the parser's deterministic-Id semantics: paths are case-folded on the
        // case-insensitive file systems (Windows/macOS) and kept exact on Linux.
        var pathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var pathToProject = new Dictionary<string, Project>(pathComparer);
        var seenProjectIds = new HashSet<Guid>();
        var packageToProject = new Dictionary<(string Name, string Version), Project>();
        var rawDependencies = new List<(string sourcePath, string targetPath)>();

        var processedPaths = projectFilePaths
            .Select(fileSystem.GetFullPath)
            .Where(fileSystem.FileExists)
            .Select(fp => (FullPath: fp, NormalizedPath: discoveryService.NormalizePath(fp)));

        foreach (var (fullPath, normalizedPath) in processedPaths)
        {
            // Skip duplicate solution entries: parsing the same path twice would produce two
            // Project records sharing one deterministic Id, crashing downstream ToDictionary(p => p.Id).
            if (pathToProject.ContainsKey(normalizedPath))
            {
                continue;
            }

            try
            {
                var (project, refs, packages) = projectParser.Parse(fullPath);
                pathToProject[normalizedPath] = project;

                // Second-level dedupe on the deterministic Id: case-variant spellings of the same
                // path can slip past the key check yet still case-fold to the same Id. The path
                // mapping above is kept so edges from either spelling resolve to the single node.
                if (!seenProjectIds.Add(project.Id))
                {
                    continue;
                }

                projects.Add(project);

                rawDependencies.AddRange(refs
                    .Select(r => discoveryService.ResolveProjectReferencePath(fullPath, r))
                    .Select(discoveryService.NormalizePath)
                    .Select(np => (normalizedPath, np)));

                if (includePackages)
                {
                    foreach (var pkg in packages)
                    {
                        if (!packageToProject.TryGetValue((pkg.Name, pkg.Version), out var packageNode))
                        {
                            // Create a deterministic ID based on package name and version
                            var pkgKey = $"{pkg.Name}:{pkg.Version}";
                            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(pkgKey));
                            var pkgId = new Guid(hash.AsMemory(0, 16).Span);

                            packageNode = new Project(
                                pkgId,
                                pkg.Name,
                                pkg.Version,
                                pkg.Version,
                                project.Framework,
                                ProjectType.Package
                            );
                            packageToProject[(pkg.Name, pkg.Version)] = packageNode;
                        }

                        dependencies.Add(new Dependency(project.Id, packageNode.Id, DependencyType.PackageReference));
                    }
                }
            }
            catch (Exception ex) when (ex is ParsingException or IOException or InvalidOperationException
                                           or XmlException)
            {
                LogProjectSkipped(logger, ex, Path.GetFileName(fullPath));
                console.WriteWarning($"Skipped project '{Path.GetFileName(fullPath)}': {ex.Message}");
            }
        }

        // Add all unique packages to the projects list
        projects.AddRange(packageToProject.Values);

        dependencies.AddRange(rawDependencies
            .Select(d => (
                Src: pathToProject.GetValueOrDefault(d.sourcePath),
                Tgt: pathToProject.GetValueOrDefault(d.targetPath)))
            .Where(x => x is { Src: not null, Tgt: not null })
            .Select(x => new Dependency(x.Src!.Id, x.Tgt!.Id, DependencyType.ProjectReference)));

        return new SolutionGraph(
            Path.GetFileName(path),
            path,
            projects,
            dependencies
        );
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped project '{ProjectFile}'")]
    private static partial void LogProjectSkipped(ILogger logger, Exception ex, string projectFile);
}
