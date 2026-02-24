namespace ProjGraph.Lib.Core.Abstractions;

/// <summary>
/// Represents options for rendering a diagram.
/// </summary>
/// <param name="ShowTitle">Whether to include the title in the rendered output.</param>
/// <param name="WrapInMarkdownFence">Whether to wrap the output in a ```mermaid code fence. Defaults to true for backward compatibility.</param>
/// <param name="IncludePackages">Whether to include NuGet package dependencies in the graph.</param>
public record DiagramOptions(
    bool ShowTitle = true,
    bool WrapInMarkdownFence = false,
    bool IncludePackages = false
);
