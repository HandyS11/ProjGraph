using ProjGraph.Core.Models;
using ProjGraph.Lib.Algorithms;
using Spectre.Console;

namespace ProjGraph.Cli.Rendering;

public static class TreeRenderer
{
    public static void Render(SolutionGraph graph)
    {
        RenderHeader(graph);

        var cycles = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);
        var cyclicProjectIds = cycles
            .Where(c => c.Count > 1)
            .SelectMany(c => c)
            .ToHashSet();

        var sortedProjects = graph.Projects
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToList();

        for (var i = 0; i < sortedProjects.Count; i++)
        {
            var project = sortedProjects[i];
            var isLastProject = i == sortedProjects.Count - 1;

            RenderProject(project, isLastProject, cyclicProjectIds);
            RenderDependencies(graph, project, isLastProject, cyclicProjectIds);
        }

        RenderCycleWarning(cyclicProjectIds);
    }

    private static void RenderHeader(SolutionGraph graph)
    {
        var graphName = Markup.Escape(graph.Name.Trim());
        AnsiConsole.Write(new Rule($"[yellow]Dependency Graph: {graphName}[/]") { Justification = Justify.Left });
        AnsiConsole.MarkupLine("[bold blue]Projects[/]");
    }

    private static void RenderProject(Project project, bool isLastProject, HashSet<Guid> cyclicProjectIds)
    {
        var pPrefix = isLastProject ? "└── " : "├── ";
        var color = cyclicProjectIds.Contains(project.Id) ? "red" : "green";
        var typeIcon = GetProjectTypeIcon(project.Type);
        var projectName = Markup.Escape(project.Name.Trim());

        AnsiConsole.MarkupLine($"{pPrefix}{typeIcon} [{color}]{projectName}[/]");
    }

    private static void RenderDependencies(
        SolutionGraph graph,
        Project project,
        bool isLastProject,
        HashSet<Guid> cyclicProjectIds)
    {
        var dependencies = graph.Dependencies
            .Where(d => d.SourceId == project.Id)
            .Select(d => graph.Projects.FirstOrDefault(p => p.Id == d.TargetId))
            .Where(p => p != null)
            .OrderBy(p => p!.Name)
            .ToList();

        for (var j = 0; j < dependencies.Count; j++)
        {
            var dep = dependencies[j]!;
            var isLastDep = j == dependencies.Count - 1;

            RenderDependency(dep, isLastProject, isLastDep, cyclicProjectIds);
        }
    }

    private static void RenderDependency(
        Project dependency,
        bool isLastProject,
        bool isLastDep,
        HashSet<Guid> cyclicProjectIds)
    {
        var dPrefix = isLastProject ? "    " : "│   ";
        var dConnector = isLastDep ? "└── " : "├── ";
        var depColor = cyclicProjectIds.Contains(dependency.Id) ? "red" : "grey";
        var depName = Markup.Escape(dependency.Name.Trim());

        AnsiConsole.MarkupLine($"{dPrefix}{dConnector}[italic {depColor}]→ {depName}[/]");
    }

    private static string GetProjectTypeIcon(ProjectType type)
    {
        return type switch
        {
            ProjectType.Executable => "🚀",
            ProjectType.Test => "🧪",
            _ => "📦"
        };
    }

    private static void RenderCycleWarning(HashSet<Guid> cyclicProjectIds)
    {
        if (cyclicProjectIds.Count is not 0)
        {
            AnsiConsole.MarkupLine("\n[red]⚠ Cycles detected![/] The projects in [red]red[/] are part of a circular dependency.");
        }
    }
}
