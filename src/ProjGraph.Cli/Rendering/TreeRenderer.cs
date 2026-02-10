using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Domain.Algorithms;
using Spectre.Console;

namespace ProjGraph.Cli.Rendering;

/// <summary>
/// Provides methods for rendering a solution graph as a tree structure in the console.
/// </summary>
/// <remarks>
/// The <see cref="TreeRenderer"/> class includes methods to render the solution graph's header, 
/// projects, dependencies, and any detected cyclic dependencies with proper formatting and color coding.
/// </remarks>
public static class TreeRenderer
{
    /// <summary>
    /// Renders the solution graph as a flat list of projects and their direct dependencies.
    /// </summary>
    /// <param name="graph">
    /// The solution graph containing the projects and dependencies to be rendered.
    /// </param>
    public static void RenderFlat(SolutionGraph graph)
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

    /// <summary>
    /// Renders the solution graph as a real tree structure, starting from root projects.
    /// </summary>
    /// <param name="graph">
    /// The solution graph containing the projects and dependencies to be rendered.
    /// </param>
    public static void RenderTree(SolutionGraph graph)
    {
        RenderHeader(graph);

        // Identify incoming dependency counts to find roots
        var incomingCounts = graph.Projects.ToDictionary(p => p.Id, _ => 0);
        foreach (var dep in graph.Dependencies)
        {
            if (incomingCounts.TryGetValue(dep.TargetId, out var value))
            {
                incomingCounts[dep.TargetId] = ++value;
            }
        }

        var cycles = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);
        var cyclicProjectIds = cycles
            .Where(c => c.Count > 1)
            .SelectMany(c => c)
            .ToHashSet();

        var globalVisited = new HashSet<Guid>();

        // 1. Print the Solution Name as the main header
        AnsiConsole.MarkupLine($"[bold blue]{Markup.Escape(graph.Name.Trim())}[/]");

        // 2. Identify "Root" projects: projects with 0 incoming dependencies
        var rootProjects = graph.Projects
            .Where(p => incomingCounts[p.Id] == 0)
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Name)
            .ToList();

        // 3. Render each root branch as a separate tree to allow for true blank lines between them
        foreach (var project in rootProjects)
        {
            AnsiConsole.WriteLine(); // Spacing line between branches
            var rootLabel = GetProjectMarkup(project, cyclicProjectIds);
            var tree = new Tree(rootLabel);

            AddChildrenRecursive(tree, project, graph, [], globalVisited, cyclicProjectIds);
            AnsiConsole.Write(tree);
        }

        // 4. Add remaining projects (those not reachable from roots, e.g. purely cyclic clusters)
        var remainingProjects = graph.Projects
            .Where(p => !globalVisited.Contains(p.Id))
            .OrderBy(p => p.Name)
            .ToList();

        foreach (var project in remainingProjects.Where(project => !globalVisited.Contains(project.Id)))
        {
            AnsiConsole.WriteLine();
            var rootLabel = GetProjectMarkup(project, cyclicProjectIds);
            var tree = new Tree(rootLabel);

            AddChildrenRecursive(tree, project, graph, [], globalVisited, cyclicProjectIds);
            AnsiConsole.Write(tree);
        }

        RenderCycleWarning(cyclicProjectIds);
    }

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
            var projectName = Markup.Escape(dep.Name.Trim());

            if (isCycle)
            {
                parent.AddNode($"{typeIcon} [red]{projectName}[/] [italic red](cycle detected)[/]");
                continue;
            }

            var color = isCyclicProject ? "red" : "green";
            var label = $"{typeIcon} [{color}]{projectName}[/]";
            var node = parent.AddNode(label);

            AddChildrenRecursive(node, dep, graph, currentPath, globalVisited, cyclicProjectIds);
        }

        currentPath.Remove(project.Id);
    }

    private static string GetProjectMarkup(Project project, HashSet<Guid> cyclicProjectIds)
    {
        var typeIcon = GetProjectTypeIcon(project.Type);
        var color = cyclicProjectIds.Contains(project.Id) ? "red" : "green";
        var projectName = Markup.Escape(project.Name.Trim());
        return $"{typeIcon} [{color}]{projectName}[/]";
    }

    /// <summary>
    /// Renders the header of the solution graph with proper formatting and styling.
    /// </summary>
    /// <param name="graph">
    /// The solution graph containing the projects and dependencies to be displayed.
    /// The graph's name will be used as the title of the header.
    /// </param>
    private static void RenderHeader(SolutionGraph graph)
    {
        var graphName = Markup.Escape(graph.Name.Trim());
        AnsiConsole.Write(new Rule($"[yellow]Dependency Graph: {graphName}[/]") { Justification = Justify.Left });
        AnsiConsole.MarkupLine("[bold blue]Projects[/]");
    }

    /// <summary>
    /// Renders a project in the solution graph with appropriate formatting and color coding.
    /// </summary>
    /// <param name="project">The project to be rendered, represented as a <see cref="Project"/> object.</param>
    /// <param name="isLastProject">
    /// A boolean indicating whether the current project is the last in the list of projects.
    /// Used to determine the connector style.
    /// </param>
    /// <param name="cyclicProjectIds">
    /// A set of project IDs that are part of a circular dependency.
    /// If the project is part of this set, it will be highlighted in red.
    /// </param>
    private static void RenderProject(Project project, bool isLastProject, HashSet<Guid> cyclicProjectIds)
    {
        var pPrefix = isLastProject ? "└── " : "├── ";
        var color = cyclicProjectIds.Contains(project.Id) ? "red" : "green";
        var typeIcon = GetProjectTypeIcon(project.Type);
        var projectName = Markup.Escape(project.Name.Trim());

        AnsiConsole.MarkupLine($"{pPrefix}{typeIcon} [{color}]{projectName}[/]");
    }

    /// <summary>
    /// Renders the dependencies of a given project in the solution graph with proper formatting and color coding.
    /// </summary>
    /// <param name="graph">The solution graph containing all projects and their dependencies.</param>
    /// <param name="project">The project whose dependencies are to be rendered.</param>
    /// <param name="isLastProject">
    /// A boolean indicating whether the current project is the last in the list of projects.
    /// Used to determine the indentation style.
    /// </param>
    /// <param name="cyclicProjectIds">
    /// A set of project IDs that are part of a circular dependency.
    /// Dependencies in this set will be highlighted in red.
    /// </param>
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

    /// <summary>
    /// Renders a dependency in the project graph with appropriate formatting and color coding.
    /// </summary>
    /// <param name="dependency">The project that represents the dependency to be rendered.</param>
    /// <param name="isLastProject">
    /// A boolean indicating whether the current project is the last in the list of projects.
    /// Used to determine the indentation style.
    /// </param>
    /// <param name="isLastDep">
    /// A boolean indicating whether the current dependency is the last in the list of dependencies.
    /// Used to determine the connector style.
    /// </param>
    /// <param name="cyclicProjectIds">
    /// A set of project IDs that are part of a circular dependency.
    /// If the dependency is part of this set, it will be highlighted in red.
    /// </param>
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

    /// <summary>
    /// Retrieves the appropriate icon representation for a given project type.
    /// </summary>
    /// <param name="type">The type of the project, represented as a <see cref="ProjectType"/> enum.</param>
    /// <returns>
    /// A string containing an emoji that represents the project type:
    /// - "🚀" for executable projects.
    /// - "🧪" for test projects.
    /// - "📦" for other types of projects.
    /// </returns>
    private static string GetProjectTypeIcon(ProjectType type)
    {
        return type switch
        {
            ProjectType.Executable => "🚀",
            ProjectType.Test => "🧪",
            _ => "📦"
        };
    }

    /// <summary>
    /// Renders a warning message if there are any cyclic dependencies detected in the project graph.
    /// </summary>
    /// <param name="cyclicProjectIds">
    /// A set of project IDs that are part of a circular dependency.
    /// If the set is not empty, a warning message will be displayed.
    /// </param>
    private static void RenderCycleWarning(HashSet<Guid> cyclicProjectIds)
    {
        if (cyclicProjectIds.Count is not 0)
        {
            AnsiConsole.MarkupLine(
                "\n[red]⚠ Cycles detected![/] The projects in [red]red[/] are part of a circular dependency.");
        }
    }
}