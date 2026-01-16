using ProjGraph.Cli.Rendering;
using ProjGraph.Lib.Rendering;
using ProjGraph.Lib.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

// ReSharper disable ClassNeverInstantiated.Global

namespace ProjGraph.Cli.Commands;

public sealed class VisualizeCommand : AsyncCommand<VisualizeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<PATH>")]
        [Description("The path to the .sln, .slnx, or .csproj file")]
        public string Path { get; init; } = string.Empty;

        [CommandOption("-f|--format")]
        [Description("The output format (tree, mermaid)")]
        [DefaultValue("tree")]
        public string Format { get; init; } = "tree";

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

            if (Format != "tree" && Format != "mermaid")
            {
                return ValidationResult.Error("Format must be 'tree' or 'mermaid'");
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
            if (settings.Format.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
            {
                // For mermaid, we want clean stdout, so all status goes to stderr
                await Console.Error.WriteLineAsync($"Analyzing {settings.Path}...");

                var graphService = new GraphService();
                var graph = await Task.Run(() => graphService.BuildGraph(settings.Path), cancellationToken);
                Console.WriteLine(MermaidGraphRenderer.Render(graph));
            }
            else
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync($"Analyzing [blue]{settings.Path}[/]...", async _ =>
                    {
                        var graphService = new GraphService();
                        var graph = await Task.Run(() => graphService.BuildGraph(settings.Path), cancellationToken);
                        TreeRenderer.Render(graph);
                    });
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}