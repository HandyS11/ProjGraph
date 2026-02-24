using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using Spectre.Console;

namespace ProjGraph.Lib.ProjectGraph.Rendering;

/// <summary>
/// Renders a solution graph as a tree structure using Spectre.Console.
/// </summary>
public sealed class TreeGraphRenderer : SolutionGraphRendererBase
{
    /// <inheritdoc />
    public override string Format => "tree";

    /// <summary>
    /// Renders a <see cref="SolutionGraph"/> as a tree structure, displaying project dependencies hierarchically.
    /// </summary>
    /// <param name="model">The <see cref="SolutionGraph"/> to render.</param>
    /// <param name="options">The options for rendering the diagram.</param>
    /// <returns>A string representation of the solution graph rendered as a tree.</returns>
    /// <remarks>
    /// This method identifies root projects (those with no incoming dependencies) and renders each as a separate tree branch.
    /// Projects involved in cyclic dependencies are highlighted in red, and cycle detection is performed using the
    /// <see cref="SolutionGraphRendererBase.GetCyclicProjectIds"/> method.
    /// </remarks>
    public override string Render(SolutionGraph model, DiagramOptions? options = null)
    {
        OutputWriter.GetStringBuilder().Clear();

        RenderHeader(model, options);

        // Identify incoming dependency counts to find roots
        var incomingCounts = model.Projects.ToDictionary(p => p.Id, _ => 0);
        foreach (var dep in model.Dependencies.Where(d => incomingCounts.ContainsKey(d.TargetId)))
        {
            incomingCounts[dep.TargetId]++;
        }

        var cyclicProjectIds = GetCyclicProjectIds(model);

        var globalVisited = new HashSet<Guid>();

        // 1. Print the Solution Name as the main header
        RenderConsole.MarkupLine($"[bold blue]{Markup.Escape(model.Name.Trim())}[/]");

        // 2. Identify "Root" projects: projects with 0 incoming dependencies
        var rootProjects = model.Projects
            .Where(p => incomingCounts[p.Id] == 0)
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToList();

        // 3. Render each root branch as a separate tree
        foreach (var project in rootProjects)
        {
            RenderConsole.WriteLine(); // Spacing line between branches
            var rootLabel = GetProjectMarkup(project, cyclicProjectIds);
            var tree = new Tree(rootLabel);

            AddChildrenRecursive(tree, project, model, [], globalVisited, cyclicProjectIds);
            RenderConsole.Write(tree);
        }

        // 4. Add remaining projects (those not reachable from roots)
        var remainingProjects = model.Projects
            .Where(p => !globalVisited.Contains(p.Id))
            .OrderBy(p => p.Name)
            .ToList();

        foreach (var project in remainingProjects.Where(project => !globalVisited.Contains(project.Id)))
        {
            RenderConsole.WriteLine();
            var rootLabel = GetProjectMarkup(project, cyclicProjectIds);
            var tree = new Tree(rootLabel);

            AddChildrenRecursive(tree, project, model, [], globalVisited, cyclicProjectIds);
            RenderConsole.Write(tree);
        }

        RenderCycleWarning(cyclicProjectIds);

        return OutputWriter.ToString();
    }

    /// <summary>
    /// Recursively adds child nodes to the tree for all dependencies of the specified project.
    /// </summary>
    /// <param name="parent">The parent <see cref="IHasTreeNodes"/> node to add children to.</param>
    /// <param name="project">The <see cref="Project"/> whose dependencies should be rendered.</param>
    /// <param name="graph">The <see cref="SolutionGraph"/> containing the project relationships.</param>
    /// <param name="currentPath">A <see cref="HashSet{T}"/> tracking the current path to detect cycles.</param>
    /// <param name="globalVisited">A <see cref="HashSet{T}"/> tracking all visited <see cref="Project"/> IDs to avoid duplicate rendering.</param>
    /// <param name="cyclicProjectIds">A <see cref="HashSet{T}"/> of <see cref="Guid"/>s representing projects involved in cycles.</param>
    /// <remarks>
    /// This method uses depth-first traversal to build the tree structure. It detects cycles by checking if a dependency
    /// is already in the <paramref name="currentPath"/> and marks cycle edges with red color and "(cycle detected)" label.
    /// Cyclic projects are colored differently from normal dependencies using the <see cref="SolutionGraphRendererBase.GetProjectTypeIcon"/> method.
    /// </remarks>
    private static void AddChildrenRecursive(
        IHasTreeNodes parent,
        Project project,
        SolutionGraph graph,
        HashSet<Guid> currentPath,
        HashSet<Guid> globalVisited,
        HashSet<Guid> cyclicProjectIds)
    {
        globalVisited.Add(project.Id);
        currentPath.Add(project.Id);

        var dependencies = graph.Dependencies
            .Where(d => d.SourceId == project.Id)
            .Select(d => graph.Projects.FirstOrDefault(p => p.Id == d.TargetId))
            .Where(p => p != null)
            .OrderBy(p => p!.Name)
            .ToList();

        foreach (var dep in dependencies)
        {
            var isCycle = currentPath.Contains(dep!.Id);
            var typeIcon = GetProjectTypeIcon(dep.Type);
            var isCyclicProject = cyclicProjectIds.Contains(dep.Id);

            if (isCycle)
            {
                var depName = Markup.Escape(dep.Name.Trim());
                parent.AddNode($"[red]{depName}[/] [italic red](cycle detected)[/]");
                continue;
            }

            string label;
            if (dep.Type == ProjectType.Package)
            {
                var version = Markup.Escape(dep.FullPath.Trim());
                var depName = Markup.Escape(dep.Name.Trim());
                label = $"{typeIcon} [yellow]{depName}[/] [dim yellow]({version})[/]";
            }
            else
            {
                var color = isCyclicProject ? "red" : "green";
                var depName = Markup.Escape(dep.Name.Trim());
                label = $"[{color}]{depName}[/]";
            }

            var node = parent.AddNode(label);

            AddChildrenRecursive(node, dep, graph, currentPath, globalVisited, cyclicProjectIds);
        }

        currentPath.Remove(project.Id);
    }

    /// <summary>
    /// Generates markup text for a <see cref="Project"/> with appropriate color and icon based on its cyclic status.
    /// </summary>
    /// <param name="project">The <see cref="Project"/> to generate markup for.</param>
    /// <param name="cyclicProjectIds">A <see cref="HashSet{T}"/> of <see cref="Guid"/>s representing projects involved in cycles.</param>
    /// <returns>A markup string containing the project type icon, name, and appropriate color coding.</returns>
    /// <remarks>
    /// Projects that are part of cyclic dependencies are colored red, while normal projects are colored green.
    /// The project type icon is obtained using <see cref="SolutionGraphRendererBase.GetProjectTypeIcon"/>.
    /// </remarks>
    private static string GetProjectMarkup(Project project, HashSet<Guid> cyclicProjectIds)
    {
        var typeIcon = GetProjectTypeIcon(project.Type);

        if (project.Type == ProjectType.Package)
        {
            var version = Markup.Escape(project.FullPath.Trim());
            var projectName = Markup.Escape(project.Name.Trim());
            return $"{typeIcon} [yellow]{projectName}[/] [dim yellow]({version})[/]";
        }

        var color = cyclicProjectIds.Contains(project.Id) ? "red" : "green";
        var name = Markup.Escape(project.Name.Trim());
        return $"{typeIcon} [{color}]{name}[/]";
    }
}
