using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Lib.ProjectGraph.Application.UseCases;

/// <summary>
/// Use case for building a solution graph from a given file path.
/// </summary>
public class BuildGraphUseCase(
    ISlnParser slnParser,
    ISlnxParser slnxParser,
    IProjectParser projectParser,
    IProjectDiscoveryService discoveryService,
    IFileSystem fileSystem)
{
    /// <summary>
    /// Executes the use case to build a solution graph from the specified file path.
    /// </summary>
    /// <param name="path">The file path to the solution or project file.</param>
    /// <returns>A <see cref="ProjGraph.Core.Models.SolutionGraph"/> representing the solution structure.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown when the file type is unsupported.</exception>
    public SolutionGraph Execute(string path)
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
        var pathToProject = new Dictionary<string, Project>();
        var rawDependencies = new List<(string sourcePath, string targetPath)>();

        var processedPaths = projectFilePaths
            .Select(fileSystem.GetFullPath)
            .Where(fileSystem.FileExists)
            .Select(fp => (FullPath: fp, NormalizedPath: discoveryService.NormalizePath(fp)));

        foreach (var (fullPath, normalizedPath) in processedPaths)
        {
            try
            {
                var (project, refs) = projectParser.Parse(fullPath);
                projects.Add(project);
                pathToProject[normalizedPath] = project;

                rawDependencies.AddRange(refs
                    .Select(r => discoveryService.ResolveProjectReferencePath(fullPath, r))
                    .Select(discoveryService.NormalizePath)
                    .Select(np => (normalizedPath, np)));
            }
            catch
            {
                // Silently skip projects that fail to analyze
            }
        }

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
}




