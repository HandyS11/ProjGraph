using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using System.Globalization;
using System.Text;

namespace ProjGraph.Lib.Dependencies.Rendering;

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

        // Map each project to a unique node id so that same-named projects (or names that
        // sanitize to the same string) do not collapse into a single Mermaid node.
        var nodeIds = BuildNodeIds(model.Projects);

        var hasPackages = model.Projects.Any(p => p.Type == ProjectType.Package);
        if (hasPackages)
        {
            sb.AppendLine("    classDef pkg stroke:#5b8ec4");
        }

        foreach (var project in model.Projects.OrderBy(p => p.Name))
        {
            var safeId = nodeIds[project.Id];
            string nodeDef;
            if (project.Type == ProjectType.Package)
            {
                // Version is stored in FullPath for package nodes
                // Hexagon shape: id{{"label"}} — use concatenation to avoid brace-escape complexity
                nodeDef = "    " + safeId + "{{\"" + EscapeLabel(project.Name + " " + project.FullPath) + "\"}}";
            }
            else
            {
                var typeLabel = project.Type switch
                {
                    ProjectType.Executable => " (Exe)",
                    ProjectType.Test => " (Test)",
                    _ => ""
                };
                nodeDef = $"    {safeId}[\"{EscapeLabel(project.Name + typeLabel)}\"]";
            }

            sb.AppendLine(nodeDef);
        }

        if (hasPackages)
        {
            foreach (var pkg in model.Projects.Where(p => p.Type == ProjectType.Package).OrderBy(p => p.Name))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    class {nodeIds[pkg.Id]} pkg");
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
                $"    {nodeIds[dep.Source!.Id]} {arrow} {nodeIds[dep.Target!.Id]}");
        }

        MermaidFenceHelper.AppendFenceEnd(sb, options);

        return sb.ToString();
    }

    /// <summary>
    /// Builds a map from each project's id to a unique Mermaid node id. The sanitized name is used
    /// as-is when unique; colliding names are disambiguated with a short suffix from the project id
    /// so distinct projects never share a node while readable ids are kept in the common case.
    /// </summary>
    /// <param name="projects">The projects to assign node ids to.</param>
    /// <returns>A map from project id to node id.</returns>
    private static Dictionary<Guid, string> BuildNodeIds(IReadOnlyCollection<Project> projects)
    {
        var baseIdCounts = projects
            .GroupBy(p => SanitizeId(p.Name), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var nodeIds = new Dictionary<Guid, string>();
        foreach (var project in projects)
        {
            var baseId = SanitizeId(project.Name);

            // On collision, append the full project id (guaranteed unique) rather than a truncated
            // slice that could itself collide. Node ids are internal — Mermaid renders the label —
            // so readability only matters for the common, non-colliding case.
            nodeIds[project.Id] = baseIdCounts[baseId] > 1
                ? $"{baseId}_{project.Id:N}"
                : baseId;
        }

        return nodeIds;
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

    /// <summary>
    /// Escapes a Mermaid quoted-label value so that a double quote in the text cannot terminate
    /// the label early. Mermaid renders the <c>#quot;</c> entity code as a double quote.
    /// </summary>
    /// <param name="label">The raw label text.</param>
    /// <returns>The label with double quotes replaced by the Mermaid entity code.</returns>
    private static string EscapeLabel(string label)
    {
        return label.Replace("\"", "#quot;", StringComparison.Ordinal);
    }
}
