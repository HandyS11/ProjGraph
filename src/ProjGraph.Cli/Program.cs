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
        var services = new ServiceCollection();

        // Register Library services
        services.AddProjGraphLib();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("projgraph");

            config.AddCommand<VisualizeCommand>("visualize")
                .WithDescription("Visualize the dependency graph of a solution or project")
                .WithExample("visualize", "MySolution.sln")
                .WithExample("visualize", "MySolution.sln", "--format", "tree")
                .WithExample("visualize", "MySolution.slnx", "--output", "graph.mmd");

            config.AddCommand<ErdCommand>("erd")
                .WithDescription("Generate a Mermaid ERD for an Entity Framework Core DbContext")
                .WithExample("erd", "Data/AppDbContext.cs")
                .WithExample("erd", "Data/AppDbContext.cs", "--context", "BlogContext")
                .WithExample("erd", "Data/AppDbContext.cs", "--output", "docs/erd.md");

            config.AddCommand<ClassDiagramCommand>("classdiagram")
                .WithDescription("Generate a Mermaid Class Diagram for a C# file")
                .WithExample("classdiagram", "Services/UserService.cs")
                .WithExample("classdiagram", "Models/User.cs", "--inheritance", "--dependencies", "--depth", "10")
                .WithExample("classdiagram", "Models/User.cs", "--output", "user-hierarchy.mmd");
        });

        return app.Run(args);
    }
}
