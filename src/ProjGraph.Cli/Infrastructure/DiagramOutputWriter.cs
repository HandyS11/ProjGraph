using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Cli.Infrastructure;

/// <summary>
/// Provides a reusable helper for writing rendered diagram output either to a file or to the console.
/// </summary>
/// <param name="console">The output console for writing results.</param>
/// <param name="fileSystem">The file system abstraction for disk operations.</param>
internal sealed class DiagramOutputWriter(IOutputConsole console, IFileSystem fileSystem)
{
    /// <summary>
    /// Writes the rendered diagram to the specified output file, or to the console if no file path is given.
    /// </summary>
    /// <param name="rendered">The rendered diagram string to write.</param>
    /// <param name="outputPath">
    /// The optional file path to write to. When <see langword="null"/>, the output is written to the console.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task WriteAsync(string rendered, string? outputPath, CancellationToken cancellationToken)
    {
        if (outputPath is not null)
        {
            var directory = fileSystem.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                fileSystem.CreateDirectory(directory);
            }

            await fileSystem.WriteAllTextAsync(outputPath, rendered, cancellationToken);
            console.WriteInfo($"Saved to {outputPath}");
        }
        else
        {
            console.WriteLine(rendered);
        }
    }

    /// <summary>
    /// Determines whether the rendered output should be wrapped in a Markdown fence,
    /// based on the output file extension. Files ending in <c>.mmd</c> are not wrapped.
    /// When writing to the console (no output path), the output is wrapped.
    /// </summary>
    /// <param name="outputPath">The optional output file path.</param>
    /// <returns><see langword="true"/> if the output should be wrapped in a Markdown fence; otherwise, <see langword="false"/>.</returns>
    public static bool ShouldWrapInMarkdownFence(string? outputPath)
    {
        return outputPath?.EndsWith(".mmd", StringComparison.OrdinalIgnoreCase) is not true;
    }
}
