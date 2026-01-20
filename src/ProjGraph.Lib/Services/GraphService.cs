using ProjGraph.Core.Models;
using ProjGraph.Lib.Interfaces;
using ProjGraph.Lib.Parsers;
using System.Diagnostics;

namespace ProjGraph.Lib.Services;

/// <summary>
/// Service responsible for building a solution graph from a given file path.
/// </summary>
public class GraphService : IGraphService
{
    /// <summary>
    /// Builds a solution graph by analyzing the specified file path.
    /// The file can be a solution file (.sln or .slnx) or a project file (.csproj).
    /// </summary>
    /// <param name="path">The path to the solution or project file.</param>
    /// <returns>A <see cref="SolutionGraph"/> object representing the projects and their dependencies.</returns>
    /// <exception cref="ArgumentException">Thrown when the file type is not supported.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    public SolutionGraph BuildGraph(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The specified file does not exist: {path}", path);
        }

        IEnumerable<string> projectFilePaths;

        if (path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            projectFilePaths = SlnxParser.GetProjectPaths(path);
        }
        else if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            projectFilePaths = SlnParser.GetProjectPaths(path);
        }
        else if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            projectFilePaths = DiscoverProjectsRecursively(path);
        }
        else
        {
            throw new ArgumentException("Unsupported file type: must be .sln, .slnx, or .csproj", nameof(path));
        }

        var projects = new List<Project>();
        var dependencies = new List<Dependency>();
        var pathToProject = new Dictionary<string, Project>(StringComparer.OrdinalIgnoreCase);
        var rawDependencies = new List<(string sourcePath, string targetPath)>();

        foreach (var projectPath in projectFilePaths)
        {
            var normalizedPath = Path.GetFullPath(projectPath);

            if (!File.Exists(normalizedPath))
            {
                continue;
            }

            try
            {
                var (project, refs) = ProjectParser.Parse(normalizedPath);
                projects.Add(project);
                pathToProject[project.FullPath] = project;

                rawDependencies.AddRange(refs
                    .Select(r => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(normalizedPath)!, r)))
                    .Select(absoluteRef => (project.FullPath, absoluteRef)));
            }
            catch
            {
                // For now, silently skip projects that fail to analyze
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

        return new SolutionGraph(
            Path.GetFileName(path),
            path,
            projects,
            dependencies
        );
    }

    /// <summary>
    /// Discovers all project file paths recursively starting from the specified root project path.
    /// </summary>
    /// <param name="rootProjectPath">The full path to the root project file to start the discovery from.</param>
    /// <returns>A <see cref="HashSet{T}"/> containing the full paths of all discovered project files.</returns>
    /// <remarks>
    /// This method parses the root project file to find its references and recursively discovers all referenced projects.
    /// It ensures that each project is only processed once by maintaining a set of discovered project paths.
    /// If a project fails to parse, it is skipped, and the error is logged for debugging purposes.
    /// </remarks>
    private static HashSet<string> DiscoverProjectsRecursively(string rootProjectPath)
    {
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toProcess = new Queue<string>();

        var rootFullPath = Path.GetFullPath(rootProjectPath);
        toProcess.Enqueue(rootFullPath);
        discovered.Add(rootFullPath);

        while (toProcess.Count > 0)
        {
            var currentPath = toProcess.Dequeue();

            if (!File.Exists(currentPath))
            {
                continue;
            }

            try
            {
                var (_, refs) = ProjectParser.Parse(currentPath);
                var projectDir = Path.GetDirectoryName(currentPath)!;

                foreach (var refPath in refs)
                {
                    var absoluteRefPath = Path.GetFullPath(Path.Combine(projectDir, refPath));

                    if (discovered.Add(absoluteRefPath))
                    {
                        toProcess.Enqueue(absoluteRefPath);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but continue - don't let one bad project stop the whole analysis
                Debug.WriteLine($"Failed to parse project {currentPath}: {ex.Message}");
            }
        }

        return discovered;
    }
}