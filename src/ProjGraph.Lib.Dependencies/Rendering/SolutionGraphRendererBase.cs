using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Domain.Algorithms;
using Spectre.Console;

namespace ProjGraph.Lib.Dependencies.Rendering;

/// <summary>
/// Base class for rendering solution graphs with ANSI console support.
/// </summary>
public abstract class SolutionGraphRendererBase : IDiagramRenderer<SolutionGraph>
{
    /// <inheritdoc />
    public abstract string Format { get; }

    /// <summary>
    /// Gets the ANSI console used for rendering output.
    /// Set per <see cref="Render"/> call via <see cref="CreateRenderContext"/>.
    /// </summary>
    protected IAnsiConsole RenderConsole { get; private set; } = null!;

    /// <summary>
    /// Gets the string writer that captures rendered output.
    /// Set per <see cref="Render"/> call via <see cref="CreateRenderContext"/>.
    /// </summary>
    protected StringWriter OutputWriter { get; private set; } = null!;

    /// <summary>
    /// Creates a fresh <see cref="OutputWriter"/> and <see cref="RenderConsole"/> pair for a single render pass.
    /// Must be called at the start of every <see cref="Render"/> implementation.
    /// </summary>
    protected void CreateRenderContext()
    {
        OutputWriter = new StringWriter();
        var globalConsole = AnsiConsole.Console;
        RenderConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = globalConsole.Profile.Capabilities.Ansi ? AnsiSupport.Yes : AnsiSupport.No,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(OutputWriter)
        });

        // Inherit capabilities from the global console (like Unicode support)
        RenderConsole.Profile.Capabilities.Unicode = globalConsole.Profile.Capabilities.Unicode;
        RenderConsole.Profile.Width = globalConsole.Profile.Width;
    }

    /// <summary>
    /// Renders the specified <see cref="SolutionGraph"/> to a string representation.
    /// </summary>
    /// <param name="model">The solution graph to render.</param>
    /// <param name="options">The options for rendering the diagram.</param>
    /// <returns>A string representation of the rendered graph.</returns>
    public abstract string Render(SolutionGraph model, DiagramOptions? options = null);

    /// <summary>
    /// Renders the header section of the solution graph visualization.
    /// </summary>
    /// <param name="graph">The solution graph to render the header for.</param>
    /// <param name="options">The options for rendering the diagram.</param>
    protected void RenderHeader(SolutionGraph graph, DiagramOptions? options = null)
    {
        if (options?.ShowTitle ?? true)
        {
            var graphName = Markup.Escape(graph.Name.Trim());
            RenderConsole.Write(new Rule($"[yellow]Dependency Graph: {graphName}[/]")
            {
                Justification = Justify.Left
            });
        }

        RenderConsole.MarkupLine("[bold blue]Projects[/]");
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
            ProjectType.Package => "📦",
            _ => "🔷"
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
            RenderConsole.MarkupLine(
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
        var cyclic = new HashSet<Guid>(cycles.Where(c => c.Count > 1).SelectMany(c => c));

        // A self-referencing project forms a single-node SCC that the size check above misses.
        foreach (var dependency in graph.Dependencies)
        {
            if (dependency.SourceId == dependency.TargetId)
            {
                cyclic.Add(dependency.SourceId);
            }
        }

        return cyclic;
    }
}
