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

/// <summary>
/// Parses the <c>--owned-mode</c> (CLI) / <c>ownedMode</c> (MCP) string option into an
/// <see cref="ErdOwnedMode"/>. Both surfaces call this single helper so the recognized values and their
/// mapping cannot drift apart — the CLI validates and converts through it, and the MCP tool must too,
/// rather than silently defaulting any unrecognized string to <see cref="ErdOwnedMode.MirrorEf"/>, which
/// gives an LLM caller a plausible-but-wrong result with no signal that its input was never applied.
/// </summary>
public static class ErdOwnedModeParser
{
    /// <summary>The accepted value for <see cref="ErdOwnedMode.MirrorEf"/>.</summary>
    public const string Mirror = "mirror";

    /// <summary>The accepted value for <see cref="ErdOwnedMode.Classic"/>.</summary>
    public const string Classic = "classic";

    /// <summary>
    /// Attempts to parse <paramref name="value"/> (case-insensitive) as an <see cref="ErdOwnedMode"/>.
    /// </summary>
    /// <param name="value">The raw option value (e.g. <c>"mirror"</c> or <c>"classic"</c>).</param>
    /// <param name="mode">The parsed mode when this method returns <see langword="true"/>; otherwise <see cref="ErdOwnedMode.MirrorEf"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a recognized value; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string value, out ErdOwnedMode mode)
    {
        if (value.Equals(Mirror, StringComparison.OrdinalIgnoreCase))
        {
            mode = ErdOwnedMode.MirrorEf;
            return true;
        }

        if (value.Equals(Classic, StringComparison.OrdinalIgnoreCase))
        {
            mode = ErdOwnedMode.Classic;
            return true;
        }

        mode = ErdOwnedMode.MirrorEf;
        return false;
    }
}
