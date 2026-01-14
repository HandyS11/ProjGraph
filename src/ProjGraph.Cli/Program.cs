using ProjGraph.Cli.Rendering;
using ProjGraph.Lib;
using Spectre.Console;
using System.CommandLine;

namespace ProjGraph.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = CreateRootCommand();
        return await rootCommand.InvokeAsync(args);
    }

    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("ProjGraph - Visualize .NET dependencies");

        var visualizeCommand = new Command("visualize", "Visualize the dependency graph of a solution or project");
        var pathArgument = new Argument<string>("path", "The path to the .sln, .slnx, or .csproj file");
        var formatOption = new Option<string>(
            ["--format", "-f"],
            () => "tree",
            "The output format (tree, mermaid)") { ArgumentHelpName = "format" };
        formatOption.FromAmong("tree", "mermaid");

        visualizeCommand.AddArgument(pathArgument);
        visualizeCommand.AddOption(formatOption);

        visualizeCommand.SetHandler(async context =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var format = context.ParseResult.GetValueForOption(formatOption) ?? "tree";
            var console = context.Console;

            if (!File.Exists(path))
            {
                console.Error.Write($"Error: File not found: {path}\n");
                return;
            }

            if (format.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
            {
                // For mermaid, we want clean stdout, so all status goes to stderr
                console.Error.Write($"Analyzing {path}...\n");
                try
                {
                    var graphService = new GraphService();
                    var graph = await Task.Run(() => graphService.BuildGraph(path));
                    console.Out.Write(MermaidRenderer.Render(graph) + "\n");
                }
                catch (Exception ex)
                {
                    console.Error.Write($"Error: {ex.Message}\n");
                }
            }
            else
            {
                // Spectre.Console still uses its own internal state, but we can't easily fix that here without more refactoring
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync($"Analyzing [blue]{path}[/]...", async _ =>
                    {
                        try
                        {
                            var graphService = new GraphService();
                            var graph = await Task.Run(() => graphService.BuildGraph(path));
                            TreeRenderer.Render(graph);
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.WriteException(ex);
                        }
                    });
            }
        });

        rootCommand.AddCommand(visualizeCommand);
        return rootCommand;
    }
}
