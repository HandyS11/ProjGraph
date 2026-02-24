using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using System.Globalization;
using System.Text;

namespace ProjGraph.Lib.ProjectGraph.Rendering;

/// <summary>
/// Provides functionality to render a solution graph in Mermaid.js format.
/// </summary>
public sealed class MermaidGraphRenderer : IDiagramRenderer<SolutionGraph>
{
    /// <inheritdoc />
    public string Format => "mermaid";

    /// <summary>
    /// Renders a solution graph into a Mermaid.js graph definition.
    /// </summary>
    /// <param name="model">The solution graph to render.</param>
    /// <param name="options">The options for rendering the diagram.</param>
    /// <returns>A string containing the Mermaid.js graph definition.</returns>
    public string Render(SolutionGraph model, DiagramOptions? options = null)
    {
        var sb = new StringBuilder();

        MermaidFenceHelper.AppendFenceStart(sb, options, model.Name);

        sb.AppendLine("graph TD");

        var hasPackages = model.Projects.Any(p => p.Type == ProjectType.Package);
        if (hasPackages)
        {
            sb.AppendLine("    classDef pkg stroke:#5b8ec4");
        }

        foreach (var project in model.Projects.OrderBy(p => p.Name))
        {
            var safeId = SanitizeId(project.Name);
            string nodeDef;
            if (project.Type == ProjectType.Package)
            {
                // Version is stored in FullPath for package nodes
                // Hexagon shape: id{{"label"}} — use concatenation to avoid brace-escape complexity
                nodeDef = "    " + safeId + "{{\"" + project.Name + " " + project.FullPath + "\"}}";
            }
            else
            {
                var typeLabel = project.Type switch
                {
                    ProjectType.Executable => " (Exe)",
                    ProjectType.Test => " (Test)",
                    _ => ""
                };
                nodeDef = $"    {safeId}[\"{project.Name}{typeLabel}\"]";
            }

            sb.AppendLine(nodeDef);
        }

        if (hasPackages)
        {
            foreach (var pkg in model.Projects.Where(p => p.Type == ProjectType.Package).OrderBy(p => p.Name))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    class {SanitizeId(pkg.Name)} pkg");
            }
        }

        var sortedDependencies = model.Dependencies
            .Select(d => new
            {
                Source = model.Projects.FirstOrDefault(p => p.Id == d.SourceId),
                Target = model.Projects.FirstOrDefault(p => p.Id == d.TargetId),
                d.Type
            })
            .Where(d => d.Source != null && d.Target != null)
            .OrderBy(d => d.Source!.Name)
            .ThenBy(d => d.Target!.Name)
            .ToList();

        foreach (var dep in sortedDependencies)
        {
            var arrow = dep.Type == DependencyType.PackageReference ? "-.->" : "-->";
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"    {SanitizeId(dep.Source!.Name)} {arrow} {SanitizeId(dep.Target!.Name)}");
        }

        MermaidFenceHelper.AppendFenceEnd(sb, options);

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
        return name
            .Replace(".", "_", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);
    }
}
