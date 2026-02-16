namespace ProjGraph.Lib.ClassDiagram.Application;

/// <summary>
/// Represents the options for configuring the analysis process, such as depth and inclusion of relationships.
/// </summary>
public sealed class AnalysisOptions
{
    /// <summary>
    /// The maximum depth of type relationships to analyze.
    /// </summary>
    public required int MaxDepth { get; init; }

    /// <summary>
    /// Indicates whether inheritance relationships should be included in the analysis.
    /// </summary>
    public required bool IncludeInheritance { get; init; }

    /// <summary>
    /// Indicates whether dependency relationships should be included in the analysis.
    /// </summary>
    public required bool IncludeDependencies { get; init; }

    /// <summary>
    /// Indicates whether properties and fields should be included in the analysis.
    /// </summary>
    public required bool IncludeProperties { get; init; }

    /// <summary>
    /// Indicates whether functions and methods should be included in the analysis.
    /// </summary>
    public required bool IncludeFunctions { get; init; }
}
