using Microsoft.Build.Construction;
using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Parsers;

public static class ProjectParser
{
    public static (Project Project, IEnumerable<string> ProjectReferences) Parse(string projectPath)
    {
        var root = ProjectRootElement.Open(projectPath)
                   ?? throw new InvalidOperationException($"Failed to parse project file: {projectPath}");

        var name = Path.GetFileNameWithoutExtension(projectPath);
        var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), projectPath);

        // Fast extraction of properties
        var framework = root.Properties.FirstOrDefault(p => p.Name == "TargetFramework")?.Value ??
                        root.Properties.FirstOrDefault(p => p.Name == "TargetFrameworks")?.Value ?? "unknown";
        var outputType = root.Properties.FirstOrDefault(p => p.Name == "OutputType")?.Value ?? "";

        var type = outputType.Contains("Exe", StringComparison.OrdinalIgnoreCase)
            ? ProjectType.Executable
            : ProjectType.Library;

        if (name.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            root.Properties.Any(p => p.Name == "IsTestProject" && p.Value.Equals("true", StringComparison.OrdinalIgnoreCase)))
            type = ProjectType.Test;

        var id = Guid.NewGuid();
        var project = new Project(id, name, projectPath, relativePath, framework, type);

        var projectReferences = root.Items
            .Where(i => i.ItemType == "ProjectReference")
            .Select(i => i.Include)
            .ToList();

        return (project, projectReferences);
    }
}
