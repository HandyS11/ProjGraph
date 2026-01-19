using ProjGraph.Core.Models;
using System.Text;

namespace ProjGraph.Lib.Rendering;

/// <summary>
/// Provides functionality to render a solution graph in Mermaid.js format.
/// </summary>
public static class MermaidGraphRenderer
{
    /// <summary>
    /// Renders a solution graph into a Mermaid.js graph definition.
    /// </summary>
    /// <param name="graph">The solution graph to render.</param>
    /// <returns>A string containing the Mermaid.js graph definition.</returns>
    public static string Render(SolutionGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph TD");

        foreach (var project in graph.Projects)
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

        foreach (var dep in graph.Dependencies)
        {
            var source = graph.Projects.FirstOrDefault(p => p.Id == dep.SourceId);
            var target = graph.Projects.FirstOrDefault(p => p.Id == dep.TargetId);

            if (source != null && target != null)
            {
                sb.AppendLine($"    {SanitizeId(source.Name)} --> {SanitizeId(target.Name)}");
            }
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