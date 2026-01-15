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
                .WithExample("visualize", "MySolution.sln")
                .WithExample("visualize", "MySolution.sln", "--format", "mermaid");

            config.AddCommand<ErdCommand>("erd")
                .WithDescription("Generate a Mermaid ERD for an Entity Framework Core DbContext")
                .WithExample("erd", "--path", "MySolution.sln")
                .WithExample("erd", "--file", "Data/AppDbContext.cs")
                .WithExample("erd", "--path", "MySolution.sln", "--context", "BlogContext");
        });

        return app.Run(args);
    }
}
