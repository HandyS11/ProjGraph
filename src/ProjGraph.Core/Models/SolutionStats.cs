using System.Text.Json.Serialization;

namespace ProjGraph.Core.Models;

/// <summary>
/// Aggregated metrics snapshot for one solution or project analysis run.
/// </summary>
/// <param name="SolutionName">The name of the analysed solution or project.</param>
/// <param name="SolutionPath">The absolute path to the analysed file.</param>
/// <param name="TotalProjectCount">Number of non-Package projects in the graph.</param>
/// <param name="TypeBreakdown">Count per project type. Keys are <see cref="ProjectType"/> names (Library, Executable, Test, Other).</param>
/// <param name="DepthStats">Aggregate dependency depth statistics. All values are <see langword="null"/> when <paramref name="HasCycles"/> is <see langword="true"/>.</param>
/// <param name="HotspotProjects">Top-N most directly-referenced projects, ranked descending by direct in-degree.</param>
/// <param name="HasCycles">
/// <see langword="true"/> if a dependency cycle was detected in the graph.
/// When <see langword="true"/>, <see cref="DepthStats"/> values are all <see langword="null"/>.
/// </param>
public record SolutionStats(
    [property: JsonPropertyName("solutionName")]
    string SolutionName,
    [property: JsonPropertyName("solutionPath")]
    string SolutionPath,
    [property: JsonPropertyName("totalProjectCount")]
    int TotalProjectCount,
    [property: JsonPropertyName("typeBreakdown")]
    IReadOnlyDictionary<string, int> TypeBreakdown,
    [property: JsonPropertyName("depthStats")]
    DependencyDepthStats DepthStats,
    [property: JsonPropertyName("hotspotProjects")]
    IReadOnlyList<HotspotProject> HotspotProjects,
    [property: JsonPropertyName("hasCycles")]
    bool HasCycles
);

/// <summary>
/// Aggregate dependency depth statistics across all projects in the graph.
/// When cycles are detected, all values are <c>null</c>.
/// </summary>
/// <param name="Average">Mean longest-path depth across all projects; <c>null</c> if cycles are detected.</param>
/// <param name="Min">Minimum depth (0 for leaf projects with no dependencies); <c>null</c> if cycles are detected.</param>
/// <param name="Max">Maximum depth (the longest dependency chain in the graph); <c>null</c> if cycles are detected.</param>
public record DependencyDepthStats(
    [property: JsonPropertyName("average")]
    double? Average,
    [property: JsonPropertyName("min")] int? Min,
    [property: JsonPropertyName("max")] int? Max
);

/// <summary>
/// A single entry in the ranked hotspot list — a project identified as heavily referenced by others.
/// </summary>
/// <param name="Name">The project name.</param>
/// <param name="InDegree">Number of other projects that directly reference this project.</param>
public record HotspotProject(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("inDegree")]
    int InDegree
);
