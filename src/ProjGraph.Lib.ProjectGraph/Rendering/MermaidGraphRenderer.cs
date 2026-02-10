using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using System.Text;

namespace ProjGraph.Lib.ProjectGraph.Rendering;

/// <summary>
/// Provides functionality to render a solution graph in Mermaid.js format.
/// </summary>
public sealed class MermaidGraphRenderer : IDiagramRenderer<SolutionGraph>
{
    /// <summary>
    /// Gets or sets a value indicating whether to include the title in the rendered output.
    /// </summary>
    public bool IncludeTitle { get; set; } = true;

    /// <summary>
    /// Renders a solution graph into a Mermaid.js graph definition.
    /// </summary>
    /// <param name="graph">The solution graph to render.</param>
    /// <returns>A string containing the Mermaid.js graph definition.</returns>
    public string Render(SolutionGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");

        if (IncludeTitle && !string.IsNullOrWhiteSpace(graph.Name))
        {
            sb.AppendLine("---");
            sb.AppendLine($"title: {graph.Name}");
            sb.AppendLine("---");
        }

        sb.AppendLine("graph TD");

        foreach (var project in graph.Projects.OrderBy(p => p.Name))
        {
            var typeLabel = project.Type switch
            {
                ProjectType.Executable => " (Exe)",
                ProjectType.Test => " (Test)",
                _ => ""
            };
            var safeId = SanitizeId(project.Name);
            sb.AppendLine($"    {safeId}[\"{project.Name}{typeLabel}\"]");
        }

        var sortedDependencies = graph.Dependencies
            .Select(d => new
            {
                Source = graph.Projects.FirstOrDefault(p => p.Id == d.SourceId),
                Target = graph.Projects.FirstOrDefault(p => p.Id == d.TargetId)
            })
            .Where(d => d.Source != null && d.Target != null)
            .OrderBy(d => d.Source!.Name)
            .ThenBy(d => d.Target!.Name);

        foreach (var dep in sortedDependencies)
        {
            sb.AppendLine($"    {SanitizeId(dep.Source!.Name)} --> {SanitizeId(dep.Target!.Name)}");
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    /// <summary>
    /// Sanitizes a project name to create a valid Mermaid.js node ID.
    /// Replaces invalid characters (e.g., '.', '-', ' ') with underscores.
    /// </summary>
    /// <param name="name">The project name to sanitize.</param>
    /// <returns>A sanitized string that can be used as a valid node ID.</returns>
    private static string SanitizeId(string name)
    {
        return name.Replace(".", "_").Replace("-", "_").Replace(" ", "_");
    }
}