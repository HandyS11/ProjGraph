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
        [CommandOption("-p|--path")]
        [Description("The path to the .sln, .slnx, or .csproj file")]
        public string? Path { get; init; }

        [CommandOption("-f|--file")]
        [Description("The path to a specific .cs file containing a DbContext")]
        public string? File { get; init; }

        [CommandOption("-c|--context")]
        [Description("The name of the DbContext to analyze (optional)")]
        public string? ContextName { get; init; }

        public override ValidationResult Validate()
        {
            if (!string.IsNullOrWhiteSpace(Path) && !System.IO.File.Exists(Path) && !Directory.Exists(Path))
            {
                return ValidationResult.Error($"Path not found: {Path}");
            }

            if (!string.IsNullOrWhiteSpace(File) && !System.IO.File.Exists(File))
            {
                return ValidationResult.Error($"File not found: {File}");
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
            var targetPath = settings.File ?? settings.Path;
            var efService = new EfAnalysisService();

            if (string.IsNullOrEmpty(targetPath))
            {
                var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.sln")
                    .Concat(Directory.GetFiles(Directory.GetCurrentDirectory(), "*.slnx"))
                    .Concat(Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj"))
                    .ToList();

                switch (files.Count)
                {
                    case 0:
                        AnsiConsole.MarkupLine(
                            "[red]Error:[/] No solution or project file found in current directory. Please specify --path or --file.");
                        return 1;
                    case 1:
                        targetPath = files[0];
                        AnsiConsole.MarkupLine(
                            $"[grey]Using [white]{Path.GetFileName(targetPath)}[/] from current directory...[/]");
                        break;
                    default:
                        targetPath = await AnsiConsole.PromptAsync(
                            new SelectionPrompt<string>()
                                .Title("Multiple project files found. Please select one:")
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
        catch (NotSupportedException ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}