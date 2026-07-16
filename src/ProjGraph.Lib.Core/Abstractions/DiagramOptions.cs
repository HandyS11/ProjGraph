namespace ProjGraph.Lib.Core.Abstractions;

/// <summary>
/// Controls how EF Core owned types (<c>OwnsOne</c>/<c>OwnsMany</c>) appear in a rendered ERD.
/// </summary>
public enum ErdOwnedMode
{
    /// <summary>
    /// The physical view: an owned type that shares its owner's table is inlined onto the owner using
    /// EF's <c>Nav_Property</c> column naming; one on its own table gets its own entity box.
    /// </summary>
    MirrorEf = 0,

    /// <summary>
    /// The conceptual view: every owned type is its own entity box linked to the owner by an identifying
    /// relationship, regardless of table mapping.
    /// </summary>
    Classic = 1
}

/// <summary>
/// Represents options for rendering a diagram.
/// </summary>
/// <param name="ShowTitle">Whether to include the title in the rendered output.</param>
/// <param name="WrapInMarkdownFence">Whether to wrap the output in a ```mermaid code fence. Defaults to <see langword="true"/>.</param>
/// <param name="IncludePackages">Whether to include NuGet package dependencies in the graph.</param>
/// <param name="ErdOwnedMode">How EF Core owned types are represented in an ERD. Defaults to <see cref="ErdOwnedMode.MirrorEf"/>.</param>
public record DiagramOptions(
    bool ShowTitle = true,
    bool WrapInMarkdownFence = true,
    bool IncludePackages = false,
    ErdOwnedMode ErdOwnedMode = ErdOwnedMode.MirrorEf
);
