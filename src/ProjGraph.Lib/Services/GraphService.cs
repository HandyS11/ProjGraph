using ProjGraph.Core.Models;
using ProjGraph.Lib.Parsers;

namespace ProjGraph.Lib.Services;

public class GraphService
{
    public SolutionGraph BuildGraph(string path)
    {
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

    private static IEnumerable<string> DiscoverProjectsRecursively(string rootProjectPath)
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

                    if (!discovered.Add(absoluteRefPath))
                    {
                        continue;
                    }

                    toProcess.Enqueue(absoluteRefPath);
                }
            }
            catch
            {
                // Skip projects that fail to parse
            }
        }

        return discovered;
    }
}
