using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Domain.Algorithms;
using Spectre.Console;

namespace ProjGraph.Lib.ProjectGraph.Rendering;

/// <summary>
/// Base class for rendering solution graphs with ANSI console support.
/// </summary>
public abstract class SolutionGraphRendererBase : IDiagramRenderer<SolutionGraph>
{
    protected readonly IAnsiConsole _console;
    protected readonly StringWriter _writer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SolutionGraphRendererBase"/> class.
    /// </summary>
    protected SolutionGraphRendererBase()
    {
        _writer = new StringWriter();
        var globalConsole = AnsiConsole.Console;
        _console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = globalConsole.Profile.Capabilities.Ansi ? AnsiSupport.Yes : AnsiSupport.No,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(_writer)
        });

        // Inherit capabilities from the global console (like Unicode support)
        _console.Profile.Capabilities.Unicode = globalConsole.Profile.Capabilities.Unicode;
        _console.Profile.Width = globalConsole.Profile.Width;
    }

    /// <summary>
    /// Renders the specified <see cref="SolutionGraph"/> to a string representation.
    /// </summary>
    /// <param name="graph">The solution graph to render.</param>
    /// <returns>A string representation of the rendered graph.</returns>
    public abstract string Render(SolutionGraph graph);

    /// <summary>
    /// Renders the header section of the solution graph visualization.
    /// </summary>
    /// <param name="graph">The solution graph to render the header for.</param>
    protected void RenderHeader(SolutionGraph graph)
    {
        var graphName = Markup.Escape(graph.Name.Trim());
        _console.Write(new Rule($"[yellow]Dependency Graph: {graphName}[/]") { Justification = Justify.Left });
        _console.MarkupLine("[bold blue]Projects[/]");
    }

    /// <summary>
    /// Gets the icon representation for the specified project type.
    /// </summary>
    /// <param name="type">The <see cref="ProjectType"/> to get the icon for.</param>
    /// <returns>A string containing the icon emoji for the project type.</returns>
    protected static string GetProjectTypeIcon(ProjectType type)
    {
        return type switch
        {
            ProjectType.Executable => "🚀",
            ProjectType.Test => "🧪",
            _ => "📦"
        };
    }

    /// <summary>
    /// Renders a warning message if cyclic dependencies are detected in the solution graph.
    /// </summary>
    /// <param name="cyclicProjectIds">A set of project IDs that are part of cyclic dependencies.</param>
    protected void RenderCycleWarning(HashSet<Guid> cyclicProjectIds)
    {
        if (cyclicProjectIds.Count is not 0)
        {
            _console.MarkupLine(
                "\n[red]⚠ Cycles detected![/] The projects in [red]red[/] are part of a circular dependency.");
        }
    }

    /// <summary>
    /// Identifies all projects that are part of cyclic dependencies in the solution graph.
    /// </summary>
    /// <param name="graph">The <see cref="SolutionGraph"/> to analyze for cycles.</param>
    /// <returns>A <see cref="HashSet{T}"/> of project <see cref="Guid"/>s that are part of cycles.</returns>
    protected static HashSet<Guid> GetCyclicProjectIds(SolutionGraph graph)
    {
        var cycles = TarjanSccAlgorithm.FindStronglyConnectedComponents(graph);
        return cycles
            .Where(c => c.Count > 1)
            .SelectMany(c => c)
            .ToHashSet();
    }
}