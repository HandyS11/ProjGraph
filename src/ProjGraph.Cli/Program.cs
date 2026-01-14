using ProjGraph.Cli.Commands;
using Spectre.Console.Cli;

namespace ProjGraph.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandApp();

        app.Configure(config =>
        {
            config.SetApplicationName("projgraph");

            config.AddCommand<VisualizeCommand>("visualize")
                .WithDescription("Visualize the dependency graph of a solution or project")
                .WithExample(["visualize", "MySolution.sln"])
                .WithExample(["visualize", "MySolution.sln", "--format", "mermaid"]);
        });

        return app.Run(args);
    }
}
