using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using Spectre.Console;

namespace ProjGraph.Lib.ProjectGraph.Rendering;

/// <summary>
/// Renders a solution graph as a flat list of projects and their direct dependencies using Spectre.Console.
/// </summary>
public sealed class FlatGraphRenderer : SolutionGraphRendererBase
{
    /// <inheritdoc />
    public override string Format => "flat";

    /// <summary>
    /// Renders a <see cref="SolutionGraph"/> as a flat list of projects and their direct dependencies.
    /// </summary>
    /// <param name="graph">The <see cref="SolutionGraph"/> to render.</param>
    /// <param name="options">The options for rendering the diagram.</param>
    /// <returns>A string representation of the solution graph rendered as a flat list.</returns>
    /// <remarks>
    /// This method renders each project in sorted order (by type and name) followed by its direct dependencies.
    /// Projects involved in cyclic dependencies are highlighted in red. Cycle detection is performed using the 
    /// <see cref="SolutionGraphRendererBase.GetCyclicProjectIds"/> method.
    /// </remarks>
    public override string Render(SolutionGraph graph, DiagramOptions? options = null)
    {
        _writer.GetStringBuilder().Clear();

        RenderHeader(graph, options);

        var cyclicProjectIds = GetCyclicProjectIds(graph);

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

        return _writer.ToString();
    }

    /// <summary>
    /// Renders a single <see cref="Project"/> with appropriate visual formatting and color coding.
    /// </summary>
    /// <param name="project">The <see cref="Project"/> to render.</param>
    /// <param name="isLastProject">A value indicating whether this is the last project in the list.</param>
    /// <param name="cyclicProjectIds">A <see cref="HashSet{T}"/> of <see cref="Guid"/>s representing projects involved in cycles.</param>
    /// <remarks>
    /// The project is displayed with a tree connector prefix (└── for last, ├── for others), a type icon obtained from 
    /// <see cref="SolutionGraphRendererBase.GetProjectTypeIcon"/>, and color-coded based on whether it's part of a cycle.
    /// </remarks>
    private void RenderProject(Project project, bool isLastProject, HashSet<Guid> cyclicProjectIds)
    {
        var pPrefix = isLastProject ? "└── " : "├── ";
        var color = cyclicProjectIds.Contains(project.Id) ? "red" : "green";
        var typeIcon = GetProjectTypeIcon(project.Type);
        var projectName = Markup.Escape(project.Name.Trim());

        _console.MarkupLine($"{pPrefix}{typeIcon} [{color}]{projectName}[/]");
    }

    /// <summary>
    /// Renders all direct dependencies of the specified <see cref="Project"/>.
    /// </summary>
    /// <param name="graph">The <see cref="SolutionGraph"/> containing project relationships.</param>
    /// <param name="project">The <see cref="Project"/> whose dependencies should be rendered.</param>
    /// <param name="isLastProject">A value indicating whether the parent project is the last in the list.</param>
    /// <param name="cyclicProjectIds">A <see cref="HashSet{T}"/> of <see cref="Guid"/>s representing projects involved in cycles.</param>
    /// <remarks>
    /// Dependencies are rendered in sorted order by project name. The visual formatting is adjusted based on whether 
    /// the parent project is the last in its list using the <see cref="RenderDependency"/> method.
    /// </remarks>
    private void RenderDependencies(
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

    /// <summary>
    /// Renders a single dependency with appropriate tree formatting and color coding.
    /// </summary>
    /// <param name="dependency">The <see cref="Project"/> representing the dependency to render.</param>
    /// <param name="isLastProject">A value indicating whether the parent project is the last in the list.</param>
    /// <param name="isLastDep">A value indicating whether this is the last dependency in the parent's dependency list.</param>
    /// <param name="cyclicProjectIds">A <see cref="HashSet{T}"/> of <see cref="Guid"/>s representing projects involved in cycles.</param>
    /// <remarks>
    /// Dependencies are rendered with appropriate indentation and tree connectors (└── for last, ├── for others). 
    /// Colors are determined by whether the dependency is involved in a cycle (red) or not (grey).
    /// </remarks>
    private void RenderDependency(
        Project dependency,
        bool isLastProject,
        bool isLastDep,
        HashSet<Guid> cyclicProjectIds)
    {
        var dPrefix = isLastProject ? "    " : "│   ";
        var dConnector = isLastDep ? "└── " : "├── ";
        var depColor = cyclicProjectIds.Contains(dependency.Id) ? "red" : "grey";
        var depName = Markup.Escape(dependency.Name.Trim());

        _console.MarkupLine($"{dPrefix}{dConnector}[italic {depColor}]→ {depName}[/]");
    }
}
