using ProjGraph.Cli.Infrastructure;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.ProjectGraph.Application;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

// ReSharper disable ClassNeverInstantiated.Global
#pragma warning disable CA1812 // Types are instantiated by Spectre.Console DI via reflection

namespace ProjGraph.Cli.Commands;

/// <summary>
/// Represents the command for visualizing the structure of a solution or project file.
/// </summary>
/// <remarks>
/// The <see cref="VisualizeCommand"/> class is an asynchronous command that uses the <see cref="Settings"/> class
/// to configure the path to the solution or project file and the desired output format. It processes the input
/// and renders the structure in the specified format (flat, tree or mermaid).
/// </remarks>
/// <param name="graphService">The graph service used to build the dependency graph.</param>
/// <param name="renderers">The collection of diagram renderers for different output formats.</param>
/// <param name="console">The output console for writing results and errors.</param>
/// <param name="outputWriter">The helper for writing rendered output to file or console.</param>
/// <param name="fileSystem">The file system abstraction for path and file validation.</param>
internal sealed class VisualizeCommand(
    IGraphService graphService,
    IEnumerable<IDiagramRenderer<SolutionGraph>> renderers,
    IOutputConsole console,
    DiagramOutputWriter outputWriter,
    IFileSystem fileSystem)
    : AsyncCommand<VisualizeCommand.Settings>
{
    private const string FormatMermaid = "mermaid";
    private const string FormatTree = "tree";
    private const string FormatFlat = "flat";

    /// <summary>
    /// Represents the settings for the `VisualizeCommand`.
    /// </summary>
    /// <remarks>
    /// This class contains the configuration options for the `VisualizeCommand`, including the path to the solution or project file
    /// and the desired output format. It also provides validation for the input settings.
    /// </remarks>
    internal sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the path to the .sln, .slnx, or .csproj file to be analyzed.
        /// </summary>
        [CommandArgument(0, "[path]")]
        [Description("The path to the .sln, .slnx, or .csproj file")]
        public string Path { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the output format for the visualization.
        /// Supported formats are "flat", "tree", and "mermaid".
        /// </summary>
        [CommandOption("-f|--format")]
        [Description("The output format (flat, tree, mermaid)")]
        [DefaultValue("mermaid")]
        public string Format { get; init; } = "mermaid";

        /// <summary>
        /// Gets the normalized (lowercased) format string.
        /// </summary>
        public string NormalizedFormat => Format.ToLowerInvariant();

        /// <summary>
        /// Gets or sets a value indicating whether to include the title in the rendered output.
        /// </summary>
        [CommandOption("--show-title <true|false>")]
        [Description("Include the diagram title (default true)")]
        [DefaultValue(true)]
        public bool ShowTitle { get; init; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include NuGet package dependencies.
        /// </summary>
        [CommandOption("--include-packages")]
        [Description("Include NuGet package dependencies in the graph")]
        public bool IncludePackages { get; init; }

        /// <summary>
        /// Gets or sets the output file path.
        /// If specified, the diagram will be written to this file instead of stdout.
        /// </summary>
        [CommandOption("-o|--output <path>")]
        [Description("The output file path")]
        public string? Output { get; init; }

        /// <summary>
        /// Validates the settings provided for the command.
        /// Ensures that the specified path exists, is valid, and that the format is "flat", "tree" or "mermaid".
        /// </summary>
        /// <returns>
        /// A <see cref="ValidationResult"/> indicating whether the settings are valid.
        /// </returns>
        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                return ValidationResult.Error("Path is required");
            }

            if (NormalizedFormat is not FormatFlat &&
                NormalizedFormat is not FormatTree &&
                NormalizedFormat is not FormatMermaid)
            {
                return ValidationResult.Error("Format must be 'flat', 'tree' or 'mermaid'");
            }

            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// Executes the command asynchronously, analyzing the specified solution or project file and rendering its structure
    /// in the specified format (flat, tree or mermaid).
    /// </summary>
    /// <param name="context">
    /// The command context containing information about the execution environment.
    /// </param>
    /// <param name="settings">
    /// The settings for the command, including the path to the solution or project file and the desired output format.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests.
    /// </param>
    /// <returns>
    /// An integer representing the exit code of the command. Returns 0 if successful, 1 otherwise.
    /// </returns>
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!fileSystem.FileExists(settings.Path))
            {
                console.WriteError($"File not found: {settings.Path}");
                return 1;
            }

            var extension = Path.GetExtension(settings.Path);
            if (!extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                console.WriteError("File must be a .sln, .slnx, or .csproj file.");
                return 1;
            }

            SolutionGraph graph;
            if (settings.NormalizedFormat.Equals(FormatMermaid, StringComparison.OrdinalIgnoreCase))
            {
                // For mermaid, we want clean stdout, so all status goes to stderr
                console.WriteInfo($"Analyzing {settings.Path}...");
                graph = await graphService.BuildGraphAsync(settings.Path, settings.IncludePackages,
                    cancellationToken);
            }
            else
            {
                SolutionGraph? result = null;
                await console.RunWithStatusAsync($"Analyzing [blue]{settings.Path}[/]...",
                    async () => result =
                        await graphService.BuildGraphAsync(settings.Path, settings.IncludePackages,
                            cancellationToken),
                    cancellationToken);

                if (result is null)
                {
                    return 0;
                }

                graph = result;
            }

            var wrapInMarkdownFence = DiagramOutputWriter.ShouldWrapInMarkdownFence(settings.Output);

            var rendered = GetRenderer(settings.NormalizedFormat)
                .Render(graph, new DiagramOptions(settings.ShowTitle, wrapInMarkdownFence, settings.IncludePackages));

            await outputWriter.WriteAsync(rendered, settings.Output, cancellationToken);

            return 0;
        }
#pragma warning disable CA1031 // Do not catch general exception type — CLI handler intentionally catches all for user-friendly display
        catch (Exception ex)
#pragma warning restore CA1031
        {
            console.WriteError(ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// Retrieves the appropriate diagram renderer based on the specified format.
    /// </summary>
    /// <param name="format">The desired output format (e.g., "flat", "tree", "mermaid").</param>
    /// <returns>
    /// An instance of <see cref="IDiagramRenderer{T}"/> that matches the specified format.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when an unsupported format is specified.
    /// </exception>
    private IDiagramRenderer<SolutionGraph> GetRenderer(string format)
    {
        return renderers.FirstOrDefault(r => r.Format.Equals(format, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"Unsupported format: {format}");
    }
}
