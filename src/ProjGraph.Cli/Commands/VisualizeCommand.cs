using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.ProjectGraph.Application;
using ProjGraph.Lib.ProjectGraph.Rendering;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

// ReSharper disable ClassNeverInstantiated.Global

namespace ProjGraph.Cli.Commands;

/// <summary>
/// Represents the command for visualizing the structure of a solution or project file.
/// </summary>
/// <remarks>
/// The <see cref="VisualizeCommand"/> class is an asynchronous command that uses the <see cref="Settings"/> class
/// to configure the path to the solution or project file and the desired output format. It processes the input
/// and renders the structure in the specified format (flat, tree or mermaid).
/// </remarks>
public sealed class VisualizeCommand(
    IGraphService graphService,
    IEnumerable<IDiagramRenderer<SolutionGraph>> renderers,
    IOutputConsole console)
    : AsyncCommand<VisualizeCommand.Settings>
{
    /// <summary>
    /// Represents the settings for the `VisualizeCommand`.
    /// </summary>
    /// <remarks>
    /// This class contains the configuration options for the `VisualizeCommand`, including the path to the solution or project file
    /// and the desired output format. It also provides validation for the input settings.
    /// </remarks>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the path to the .sln, .slnx, or .csproj file to be analyzed.
        /// </summary>
        [CommandArgument(0, "<PATH>")]
        [Description("The path to the .sln, .slnx, or .csproj file")]
        public string Path { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the output format for the visualization.
        /// Supported formats are "flat", "tree", and "mermaid".
        /// </summary>
        [CommandOption("-f|--format")]
        [Description("The output format (flat, tree, mermaid)")]
        [DefaultValue("mermaid")]
        public string Format { get; private set; } = "mermaid";

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

            if (!File.Exists(Path))
            {
                return ValidationResult.Error($"File not found: {Path}");
            }

            Format = Format.ToLowerInvariant();
            if (Format != "flat" && Format != "tree" && Format != "mermaid")
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
            if (settings.Format.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
            {
                // For mermaid, we want clean stdout, so all status goes to stderr
                console.WriteInfo($"Analyzing {settings.Path}...");

                var graph = await Task.Run(() => graphService.BuildGraph(settings.Path), cancellationToken);
                console.WriteLine(GetRenderer(settings.Format).Render(graph));
            }
            else
            {
                // We'll keep AnsiConsole.Status for now as it's a CLI UI feature, 
                // but we use the service for the final render if we refactor it.
                SolutionGraph? graph = null;
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync($"Analyzing [blue]{settings.Path}[/]...", async _ =>
                    {
                        graph = await Task.Run(() => graphService.BuildGraph(settings.Path), cancellationToken);
                    });

                if (graph is null)
                {
                    return 0;
                }

                console.WriteLine(GetRenderer(settings.Format).Render(graph));
            }

            return 0;
        }
        catch (Exception ex)
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
        return format.ToLowerInvariant() switch
        {
            "mermaid" => renderers.OfType<MermaidGraphRenderer>().First(),
            "tree" => renderers.OfType<TreeGraphRenderer>().First(),
            "flat" => renderers.OfType<FlatGraphRenderer>().First(),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }
}