using System.Globalization;
using System.Text;

namespace ProjGraph.Lib.Core.Abstractions;

/// <summary>
/// Shared helper for wrapping Mermaid diagram output in a Markdown code fence
/// and rendering the optional YAML front-matter title block.
/// </summary>
public static class MermaidFenceHelper
{
    /// <summary>
    /// Appends the opening fence (<c>```mermaid</c>) and optional title block to the <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="options">Diagram rendering options (may be <c>null</c>).</param>
    /// <param name="title">The diagram title. Ignored when <c>null</c> or whitespace.</param>
    public static void AppendFenceStart(StringBuilder sb, DiagramOptions? options, string? title)
    {
        var wrapFence = options?.WrapInMarkdownFence ?? true;

        if (wrapFence)
        {
            sb.AppendLine("```mermaid");
        }

        if ((options?.ShowTitle ?? true) && !string.IsNullOrWhiteSpace(title))
        {
            sb.AppendLine("---")
                .AppendLine(CultureInfo.InvariantCulture, $"title: {title}")
                .AppendLine("---");
        }
    }

    /// <summary>
    /// Appends the closing fence (<c>```</c>) to the <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="options">Diagram rendering options (may be <c>null</c>).</param>
    public static void AppendFenceEnd(StringBuilder sb, DiagramOptions? options)
    {
        var wrapFence = options?.WrapInMarkdownFence ?? true;

        if (wrapFence)
        {
            sb.AppendLine("```");
        }
    }
}
