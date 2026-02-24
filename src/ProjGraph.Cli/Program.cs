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
        });

        return app.Run(args);
    }
}
