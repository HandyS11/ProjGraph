using System.Text;
using ProjGraph.Core.Models;

namespace ProjGraph.Cli.Rendering;

public class MermaidRenderer
{
    public string Render(SolutionGraph graph)
    {
        var sb = new StringBuilder();
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

        return sb.ToString();
    }

    private string SanitizeId(string name)
    {
        return name.Replace(".", "_").Replace("-", "_").Replace(" ", "_");
    }
}
