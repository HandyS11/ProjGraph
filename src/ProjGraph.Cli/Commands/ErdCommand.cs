using ProjGraph.Lib.Rendering;
using ProjGraph.Lib.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

// ReSharper disable ClassNeverInstantiated.Global

namespace ProjGraph.Cli.Commands;

public sealed class ErdCommand : AsyncCommand<ErdCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description(
            "Path to a .cs file containing a DbContext (optional, searches current directory if not specified)")]
        public string? Path { get; init; }

        [CommandOption("-c|--context")]
        [Description("The name of the DbContext to analyze (optional)")]
        public string? ContextName { get; init; }

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