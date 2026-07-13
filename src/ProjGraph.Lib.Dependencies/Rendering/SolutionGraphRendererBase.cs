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
    /// A per-render pair of an isolated ANSI console and the string writer that captures its output.
    /// Passing this through the call chain (instead of storing it on the renderer) keeps the
    /// renderers free of mutable instance state, so a single singleton instance is safe to use from
    /// multiple threads concurrently.
    /// </summary>
    protected sealed class RenderContext
    {
        /// <summary>Gets the isolated ANSI console to write rendered output to.</summary>
        public required IAnsiConsole Console { get; init; }

        /// <summary>Gets the writer capturing the console output; its final text is the render result.</summary>
        public required StringWriter Writer { get; init; }
    }

    /// <summary>
    /// Creates a fresh, isolated <see cref="RenderContext"/> for a single render pass.
    /// Must be called at the start of every <see cref="Render"/> implementation.
    /// </summary>
    /// <returns>A new render context.</returns>
    protected static RenderContext CreateRenderContext()
    {
        var writer = new StringWriter();
        var globalConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = globalConsole.Profile.Capabilities.Ansi ? AnsiSupport.Yes : AnsiSupport.No,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(writer)
        });

        // Inherit capabilities from the global console (like Unicode support)
        console.Profile.Capabilities.Unicode = globalConsole.Profile.Capabilities.Unicode;
        console.Profile.Width = globalConsole.Profile.Width;

        return new RenderContext { Console = console, Writer = writer };
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
    /// <param name="console">The console to render to.</param>
    /// <param name="graph">The solution graph to render the header for.</param>
    /// <param name="options">The options for rendering the diagram.</param>
    protected static void RenderHeader(IAnsiConsole console, SolutionGraph graph, DiagramOptions? options = null)
    {
        if (options?.ShowTitle ?? true)
        {
            var graphName = Markup.Escape(graph.Name.Trim());
            console.Write(new Rule($"[yellow]Dependency Graph: {graphName}[/]")
            {
                Justification = Justify.Left
            });
        }

        console.MarkupLine("[bold blue]Projects[/]");
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
    /// <param name="console">The console to render to.</param>
    /// <param name="cyclicProjectIds">A set of project IDs that are part of cyclic dependencies.</param>
    protected static void RenderCycleWarning(IAnsiConsole console, HashSet<Guid> cyclicProjectIds)
    {
        if (cyclicProjectIds.Count is not 0)
        {
            console.MarkupLine(
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
