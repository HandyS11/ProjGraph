using ProjGraph.Lib.Rendering;
using ProjGraph.Lib.Services.EfAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

// ReSharper disable ClassNeverInstantiated.Global

namespace ProjGraph.Cli.Commands;

/// <summary>
/// Represents the command for generating an Entity Relationship Diagram (ERD) from a DbContext file.
/// </summary>
/// <remarks>
/// The <see cref="ErdCommand"/> class is an asynchronous command that utilizes the `Settings` class
/// to configure the path to the DbContext file and the optional DbContext name. It processes the input
/// and generates a Mermaid ERD diagram based on the analyzed DbContext.
/// </remarks>
public sealed class ErdCommand : AsyncCommand<ErdCommand.Settings>
{
    /// <summary>
    /// Represents the settings for the `ErdCommand`.
    /// </summary>
    /// <remarks>
    /// This class contains the configuration options for the `ErdCommand`, including the path to the DbContext file
    /// and the optional DbContext name. It also provides validation for the input settings.
    /// </remarks>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the path to a .cs file containing a DbContext.
        /// If not specified, the current directory will be searched for DbContext files.
        /// </summary>
        [CommandArgument(0, "[path]")]
        [Description(
            "Path to a .cs file containing a DbContext (optional, searches current directory if not specified)")]
        public string? Path { get; init; }

        /// <summary>
        /// Gets or sets the name of the DbContext to analyze.
        /// This is an optional parameter.
        /// </summary>
        [CommandOption("-c|--context")]
        [Description("The name of the DbContext to analyze (optional)")]
        public string? ContextName { get; init; }

        /// <summary>
        /// Validates the settings provided for the command.
        /// Ensures that the specified path exists, is a .cs file, or is left empty to search the current directory.
        /// </summary>
        /// <returns>
        /// A <see cref="ValidationResult"/> indicating whether the settings are valid.
        /// </returns>
        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                return ValidationResult.Success();
            }

            if (!File.Exists(Path))
            {
                return ValidationResult.Error($"File not found: {Path}");
            }

            if (!Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Error($"Only .cs files are supported. Got: {Path}");
            }

            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// Executes the command asynchronously, analyzing the specified DbContext file and generating a Mermaid ERD diagram.
    /// </summary>
    /// <param name="context">
    /// The command context containing information about the execution environment.
    /// </param>
    /// <param name="settings">
    /// The settings for the command, including the path to the DbContext file and the optional DbContext name.
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
            var targetPath = settings.Path;
            var efService = new EfAnalysisService();

            if (string.IsNullOrEmpty(targetPath))
            {
                // Search for DbContext .cs files in current directory
                var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*DbContext.cs")
                    .ToList();

                switch (files.Count)
                {
                    case 0:
                        AnsiConsole.MarkupLine(
                            "[red]Error:[/] No DbContext .cs file found in current directory.");
                        AnsiConsole.MarkupLine("[grey]Usage: projgraph erd path/to/YourDbContext.cs[/]");
                        return 1;
                    case 1:
                        targetPath = files[0];
                        AnsiConsole.MarkupLine(
                            $"[grey]Using [white]{Path.GetFileName(targetPath)}[/] from current directory...[/]");
                        break;
                    default:
                        targetPath = await AnsiConsole.PromptAsync(
                            new SelectionPrompt<string>()
                                .Title("Multiple DbContext files found. Please select one:")
                                .AddChoices(files.Select(f => Path.GetFileName(f))),
                            cancellationToken);
                        targetPath = files.First(f => Path.GetFileName(f) == targetPath);
                        break;
                }
            }

            var contexts = await efService.DiscoverContextsAsync(targetPath);
            var selectedContext = settings.ContextName;

            if (string.IsNullOrEmpty(selectedContext))
            {
                switch (contexts.Count)
                {
                    case 0:
                        AnsiConsole.MarkupLine($"[red]Error:[/] No DbContext found in '{targetPath}'.");
                        return 1;
                    case > 1:
                        selectedContext = await AnsiConsole.PromptAsync(
                            new SelectionPrompt<string>()
                                .Title("Multiple DbContexts found. Please select one:")
                                .AddChoices(contexts),
                            cancellationToken);
                        break;
                    default:
                        selectedContext = contexts[0];
                        break;
                }
            }

            var model = await efService.AnalyzeContextAsync(targetPath, selectedContext);
            var mermaid = MermaidErdRenderer.Render(model);

            Console.WriteLine(mermaid);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}