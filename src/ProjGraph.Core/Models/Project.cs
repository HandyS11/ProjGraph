namespace ProjGraph.Core.Models;

/// <summary>
/// Represents the type of a project in the solution.
/// </summary>
public enum ProjectType
{
    /// <summary>
    /// Represents a library project.
    /// </summary>
    Library = 0,

    /// <summary>
    /// Represents an executable project.
    /// </summary>
    Executable = 1,

    /// <summary>
    /// Represents a test project.
    /// </summary>
    Test = 2,

    /// <summary>
    /// Represents a project of other types.
    /// </summary>
    Other = 3,

    /// <summary>
    /// Represents an external NuGet package dependency.
    /// </summary>
    Package = 4
}

/// <summary>
/// Represents a project in the solution.
/// </summary>
/// <param name="Id">The unique identifier of the project.</param>
/// <param name="Name">The name of the project.</param>
/// <param name="FullPath">The full file path to the project.</param>
/// <param name="RelativePath">The relative file path to the project.</param>
/// <param name="Framework">The target framework of the project.</param>
/// <param name="Type">The type of the project (e.g., Library, Executable, Test, Other).</param>
public record Project(
    Guid Id,
    string Name,
    string FullPath,
    string RelativePath,
    string Framework,
    ProjectType Type
);
