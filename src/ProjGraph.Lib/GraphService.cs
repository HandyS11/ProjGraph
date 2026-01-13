using ProjGraph.Core.Models;
using ProjGraph.Lib.Parsers;
using Microsoft.Build.Construction;

namespace ProjGraph.Lib;

public class GraphService
{
    private readonly ProjectParser _projectParser = new();
    private readonly SlnxParser _slnxParser = new();

    public SolutionGraph BuildGraph(string path)
    {
        IEnumerable<string> projectFilePaths;

        if (path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            projectFilePaths = _slnxParser.GetProjectPaths(path);
        }
        else if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            var slnFile = SolutionFile.Parse(path);
            projectFilePaths = slnFile.ProjectsInOrder
                .Where(p => p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
                .Select(p => p.AbsolutePath);
        }
        else if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            projectFilePaths = [Path.GetFullPath(path)];
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
            if (!File.Exists(projectPath)) continue;

            try
            {
                var (project, refs) = _projectParser.Parse(projectPath);
                projects.Add(project);
                pathToProject[project.FullPath] = project;

                rawDependencies.AddRange(refs.Select(r => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, r))).Select(absoluteRef => (project.FullPath, absoluteRef)));
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
}
