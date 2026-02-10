namespace ProjGraph.Lib.Core.Abstractions;

/// <summary>
/// Represents options for rendering a diagram.
/// </summary>
/// <param name="ShowTitle">Whether to include the title in the rendered output.</param>
public record DiagramOptions(bool ShowTitle = true);