using System.ComponentModel;

namespace ProjGraph.Lib.ClassDiagram.Application;

/// <summary>
/// Represents the options for configuring the analysis process, such as depth and inclusion of relationships.
/// </summary>
/// <param name="MaxDepth">The maximum depth of type relationships to analyze.</param>
/// <param name="IncludeInheritance">Indicates whether inheritance relationships should be included in the analysis.</param>
/// <param name="IncludeDependencies">Indicates whether dependency relationships should be included in the analysis.</param>
/// <param name="IncludeProperties">Indicates whether properties and fields should be included in the analysis.</param>
/// <param name="IncludeFunctions">Indicates whether functions and methods should be included in the analysis.</param>
public record AnalysisOptions(
    [Description("How many levels of relationships to follow (default: 1).")]
    int MaxDepth = 1,
    [Description("Whether to search the workspace for base classes and interfaces.")]
    bool IncludeInheritance = false,
    [Description("Whether to search for and include other classes used as properties or fields.")]
    bool IncludeDependencies = false,
    [Description("Whether to display properties and fields in the class diagram (default: true).")]
    bool IncludeProperties = true,
    [Description("Whether to display functions/methods in the class diagram (default: true).")]
    bool IncludeFunctions = true);
