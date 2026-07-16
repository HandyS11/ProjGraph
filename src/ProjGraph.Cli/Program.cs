using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Cli.Commands;
using ProjGraph.Cli.Infrastructure;
using ProjGraph.Lib;
using Spectre.Console.Cli;

namespace ProjGraph.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        const string packageDiagramCommandName = "visualize";
        const string erdCommandName = "erd";
        const string classDiagramCommandName = "classdiagram";

        var services = new ServiceCollection();

        // Register Library services
        services.AddProjGraphLib();

        // Register CLI-specific services
        services.AddSingleton<DiagramOutputWriter>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("projgraph");

            // Spectre's default parser silently ignores unrecognized long options: a typo like
            // `--owned-mod classic` (missing 'e') exits 0 and renders with defaults, giving the user
            // no signal their option was dropped. Strict parsing turns any unknown option into a
            // parse error with a non-zero exit code instead.
            config.Settings.StrictParsing = true;

            config.AddCommand<VisualizeCommand>(packageDiagramCommandName)
                .WithDescription("Visualize the dependency graph of a solution or project")
                .WithExample(packageDiagramCommandName, "MySolution.sln")
                .WithExample(packageDiagramCommandName, "MySolution.sln", "--format", "tree")
                .WithExample(packageDiagramCommandName, "MySolution.slnx", "--output", "graph.mmd");

            config.AddCommand<ErdCommand>(erdCommandName)
                .WithDescription("Generate a Mermaid ERD for an Entity Framework Core DbContext")
                .WithExample(erdCommandName, "Data/AppDbContext.cs")
                .WithExample(erdCommandName, "Data/AppDbContext.cs", "--context", "BlogContext")
                .WithExample(erdCommandName, "Data/AppDbContext.cs", "--output", "docs/erd.md");

            config.AddCommand<ClassDiagramCommand>(classDiagramCommandName)
                .WithDescription("Generate a Mermaid Class Diagram for a C# file")
                .WithExample(classDiagramCommandName, "Services/UserService.cs")
                .WithExample(classDiagramCommandName, "Models/User.cs", "--inheritance", "--dependencies", "--depth",
                    "10")
                .WithExample(classDiagramCommandName, "Models/User.cs", "--output", "user-hierarchy.mmd");

            config.AddCommand<StatsCommand>("stats")
                .WithDescription("Analyse a solution and print key architectural metrics")
                .WithExample("stats", "MySolution.slnx")
                .WithExample("stats", "MySolution.slnx", "--top", "10");
        });

        return app.Run(args);
    }
}
