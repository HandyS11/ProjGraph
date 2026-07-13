using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using Spectre.Console;

namespace ProjGraph.Lib.Dependencies.Rendering;

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
    /// <param name="model">The <see cref="SolutionGraph"/> to render.</param>
    /// <param name="options">The options for rendering the diagram.</param>
    /// <returns>A string representation of the solution graph rendered as a flat list.</returns>
    /// <remarks>
    /// This method renders each project in sorted order (by type and name) followed by its direct dependencies.
    /// Projects involved in cyclic dependencies are highlighted in red. Cycle detection is performed using the
    /// <see cref="SolutionGraphRendererBase.GetCyclicProjectIds"/> method.
    /// </remarks>
    public override string Render(SolutionGraph model, DiagramOptions? options = null)
    {
        var context = CreateRenderContext();
        var console = context.Console;

        RenderHeader(console, model, options);

        var cyclicProjectIds = GetCyclicProjectIds(model);

        var sortedProjects = model.Projects
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToList();

        for (var i = 0; i < sortedProjects.Count; i++)
        {
            var project = sortedProjects[i];
            var isLastProject = i == sortedProjects.Count - 1;

            RenderProject(console, project, isLastProject, cyclicProjectIds);
            RenderDependencies(console, model, project, isLastProject, cyclicProjectIds);
        }

        RenderCycleWarning(console, cyclicProjectIds);

        return context.Writer.ToString();
    }

    /// <summary>
    /// Renders a single <see cref="Project"/> with appropriate visual formatting and color coding.
    /// </summary>
    /// <param name="console">The console to render to.</param>
    /// <param name="project">The <see cref="Project"/> to render.</param>
    /// <param name="isLastProject">A value indicating whether this is the last project in the list.</param>
    /// <param name="cyclicProjectIds">A <see cref="HashSet{T}"/> of <see cref="Guid"/>s representing projects involved in cycles.</param>
    /// <remarks>
    /// The project is displayed with a tree connector prefix (└── for last, ├── for others), a type icon obtained from
    /// <see cref="SolutionGraphRendererBase.GetProjectTypeIcon"/>, and color-coded based on whether it's part of a cycle.
    /// </remarks>
    private static void RenderProject(IAnsiConsole console, Project project, bool isLastProject,
        HashSet<Guid> cyclicProjectIds)
    {
        var pPrefix = isLastProject ? "└── " : "├── ";
        var typeIcon = GetProjectTypeIcon(project.Type);

        string label;
        if (project.Type is ProjectType.Package)
        {
            // FullPath holds the version for package nodes
            var version = Markup.Escape(project.FullPath.Trim());
            var packageName = Markup.Escape(project.Name.Trim());
            label = $"{pPrefix}{typeIcon} [yellow]{packageName}[/] [dim yellow]({version})[/]";
        }
        else
        {
            var color = cyclicProjectIds.Contains(project.Id) ? "red" : "green";
            var projectName = Markup.Escape(project.Name.Trim());
            label = $"{pPrefix}{typeIcon} [{color}]{projectName}[/]";
        }

        console.MarkupLine(label);
    }

    /// <summary>
    /// Renders all direct dependencies of the specified <see cref="Project"/>.
    /// </summary>
    /// <param name="console">The console to render to.</param>
    /// <param name="model">The <see cref="SolutionGraph"/> containing project relationships.</param>
    /// <param name="project">The <see cref="Project"/> whose dependencies should be rendered.</param>
    /// <param name="isLastProject">A value indicating whether the parent project is the last in the list.</param>
    /// <param name="cyclicProjectIds">A <see cref="HashSet{T}"/> of <see cref="Guid"/>s representing projects involved in cycles.</param>
    /// <remarks>
    /// Dependencies are rendered in sorted order by project name. The visual formatting is adjusted based on whether
    /// the parent project is the last in its list using the <see cref="RenderDependency"/> method.
    /// </remarks>
    private static void RenderDependencies(
        IAnsiConsole console,
        SolutionGraph model,
        Project project,
        bool isLastProject,
        HashSet<Guid> cyclicProjectIds)
    {
        var dependencies = model.Dependencies
            .Where(d => d.SourceId == project.Id)
            .Select(d => model.Projects.FirstOrDefault(p => p.Id == d.TargetId))
            .Where(p => p != null)
            .OrderBy(p => p!.Name)
            .ToList();

        for (var j = 0; j < dependencies.Count; j++)
        {
            var dep = dependencies[j]!;
            var isLastDep = j == dependencies.Count - 1;

            RenderDependency(console, dep, isLastProject, isLastDep, cyclicProjectIds);
        }
    }

    /// <summary>
    /// Renders a single dependency with appropriate tree formatting and color coding.
    /// </summary>
    /// <param name="console">The console to render to.</param>
    /// <param name="dependency">The <see cref="Project"/> representing the dependency to render.</param>
    /// <param name="isLastProject">A value indicating whether the parent project is the last in the list.</param>
    /// <param name="isLastDep">A value indicating whether this is the last dependency in the parent's dependency list.</param>
    /// <param name="cyclicProjectIds">A <see cref="HashSet{T}"/> of <see cref="Guid"/>s representing projects involved in cycles.</param>
    /// <remarks>
    /// Dependencies are rendered with appropriate indentation and tree connectors (└── for last, ├── for others).
    /// Colors are determined by whether the dependency is involved in a cycle (red) or not (grey).
    /// </remarks>
    private static void RenderDependency(
        IAnsiConsole console,
        Project dependency,
        bool isLastProject,
        bool isLastDep,
        HashSet<Guid> cyclicProjectIds)
    {
        var dPrefix = isLastProject ? "    " : "│   ";
        var dConnector = isLastDep ? "└── " : "├── ";

        string label;
        if (dependency.Type is ProjectType.Package)
        {
            var version = Markup.Escape(dependency.FullPath.Trim());
            var depName = Markup.Escape(dependency.Name.Trim());
            label = $"{dPrefix}{dConnector}[italic yellow]→ {depName}[/] [dim yellow]({version})[/]";
        }
        else
        {
            var depColor = cyclicProjectIds.Contains(dependency.Id) ? "red" : "grey";
            var depName = Markup.Escape(dependency.Name.Trim());
            label = $"{dPrefix}{dConnector}[italic {depColor}]→ {depName}[/]";
        }

        console.MarkupLine(label);
    }
}
