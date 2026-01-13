using ProjGraph.Core.Models;
using ProjGraph.Lib.Algorithms;
using Spectre.Console;

namespace ProjGraph.Cli.Rendering;

public class TreeRenderer
{
    public void Render(SolutionGraph graph)
    {
        var graphName = Markup.Escape(graph.Name.Trim());
        AnsiConsole.Write(new Rule($"[yellow]Dependency Graph: {graphName}[/]") { Justification = Justify.Left });

        AnsiConsole.MarkupLine("[bold blue]Projects[/]");

        var sccAlgorithm = new TarjanSccAlgorithm();
        var cycles = sccAlgorithm.FindStronglyConnectedComponents(graph);
        var cyclicProjectIds = cycles.Where(c => c.Count > 1).SelectMany(c => c).ToHashSet();

        var sortedProjects = graph.Projects
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToList();

        for (int i = 0; i < sortedProjects.Count; i++)
        {
            var project = sortedProjects[i];
            bool isLastProject = i == sortedProjects.Count - 1;
            string pPrefix = isLastProject ? "└── " : "├── ";

            var color = cyclicProjectIds.Contains(project.Id) ? "red" : "green";
            var typeIcon = project.Type switch
            {
                ProjectType.Executable => "🚀",
                ProjectType.Test => "🧪",
                _ => "📦"
            };

            var projectName = Markup.Escape(project.Name.Trim());
            AnsiConsole.MarkupLine($"{pPrefix}{typeIcon} [{color}]{projectName}[/]");

            var dependencies = graph.Dependencies
                .Where(d => d.SourceId == project.Id)
                .Select(d => graph.Projects.FirstOrDefault(p => p.Id == d.TargetId))
                .Where(p => p != null)
                .OrderBy(p => p!.Name)
                .ToList();

            for (int j = 0; j < dependencies.Count; j++)
            {
                var dep = dependencies[j]!;
                bool isLastDep = j == dependencies.Count - 1;
                string dPrefix = isLastProject ? "    " : "│   ";
                string dConnector = isLastDep ? "└── " : "├── ";
                var depColor = cyclicProjectIds.Contains(dep.Id) ? "red" : "grey";
                var depName = Markup.Escape(dep.Name.Trim());

                AnsiConsole.MarkupLine($"{dPrefix}{dConnector}[italic {depColor}]→ {depName}[/]");
            }
        }

        if (cyclicProjectIds.Any())
        {
            AnsiConsole.MarkupLine("\n[red]⚠ Cycles detected![/] The projects in [red]red[/] are part of a circular dependency.");
        }
    }
}
