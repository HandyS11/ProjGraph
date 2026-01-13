namespace ProjGraph.Core.Models;

public enum ProjectType
{
    Library,
    Executable,
    Test,
    Other
}

public record Project(
    Guid Id,
    string Name,
    string FullPath,
    string RelativePath,
    string Framework,
    ProjectType Type
);
