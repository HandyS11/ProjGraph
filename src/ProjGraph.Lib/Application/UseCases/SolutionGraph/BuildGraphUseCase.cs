using ProjGraph.Core.Models;
using ProjGraph.Lib.Application.Interfaces;

namespace ProjGraph.Lib.Application.UseCases.SolutionGraph;

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
    /// <returns>A <see cref="Core.Models.SolutionGraph"/> representing the solution structure.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown when the file type is unsupported.</exception>
    public Core.Models.SolutionGraph Execute(string path)
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

        foreach (var projectPath in projectFilePaths)
        {
            var fullPath = fileSystem.GetFullPath(projectPath);
            var normalizedPath = discoveryService.NormalizePath(fullPath);

            if (!fileSystem.FileExists(fullPath))
            {
                continue;
            }

            try
            {
                var (project, refs) = projectParser.Parse(fullPath);
                projects.Add(project);
                pathToProject[normalizedPath] = project;

                rawDependencies.AddRange(from refPath in refs
                    select discoveryService.ResolveProjectReferencePath(fullPath, refPath)
                    into absoluteRefPath
                    select discoveryService.NormalizePath(absoluteRefPath)
                    into normalizedRefPath
                    select (normalizedPath, normalizedRefPath));
            }
            catch
            {
                // Silently skip projects that fail to analyze
            }
        }

        foreach (var (sourcePath, targetPath) in rawDependencies)
        {
            if (pathToProject.TryGetValue(sourcePath, out var source) &&
                pathToProject.TryGetValue(targetPath, out var target))
            {
                dependencies.Add(new Dependency(source.Id, target.Id, DependencyType.ProjectReference));
            }
        }

        return new Core.Models.SolutionGraph(
            Path.GetFileName(path),
            path,
            projects,
            dependencies
        );
    }
}