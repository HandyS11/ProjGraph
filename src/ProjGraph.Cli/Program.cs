using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Cli.Commands;
using ProjGraph.Cli.Infrastructure;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Application.Interfaces;
using ProjGraph.Lib.Application.Services;
using ProjGraph.Lib.Infrastructure.Analysis.ClassAnalysis;
using ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis;
using ProjGraph.Lib.Infrastructure.Parsers;
using ProjGraph.Lib.Infrastructure.Rendering;
using Spectre.Console.Cli;

namespace ProjGraph.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        var services = new ServiceCollection();

        // Infrastructure - Parsers
        services.AddSingleton<ISlnParser, SlnParser>();
        services.AddSingleton<ISlnxParser, SlnxParser>();
        services.AddSingleton<IProjectParser, ProjectParser>();

        // Infrastructure - Analysis
        services.AddSingleton<ICompilationFactory, CompilationFactory>();
        services.AddSingleton<ITypeProcessor, TypeProcessor>();

        // Infrastructure - Renderers
        services.AddSingleton<IDiagramRenderer<SolutionGraph>, MermaidGraphRenderer>();
        services.AddSingleton<IDiagramRenderer<ClassModel>, MermaidClassDiagramRenderer>();
        services.AddSingleton<IDiagramRenderer<EfModel>, MermaidErdRenderer>();

        // Application Services
        services.AddSingleton<IGraphService, GraphService>();
        services.AddSingleton<IEfAnalysisService, EfAnalysisService>();
        services.AddSingleton<IClassAnalysisService, ClassAnalysisService>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("projgraph");

            config.AddCommand<VisualizeCommand>("visualize")
                .WithDescription("Visualize the dependency graph of a solution or project")
                .WithExample("visualize", "MySolution.sln")
                .WithExample("visualize", "MySolution.sln", "--format", "mermaid");

            config.AddCommand<ErdCommand>("erd")
                .WithDescription("Generate a Mermaid ERD for an Entity Framework Core DbContext")
                .WithExample("erd", "Data/AppDbContext.cs")
                .WithExample("erd", "Data/AppDbContext.cs", "--context", "BlogContext");

            config.AddCommand<ClassDiagramCommand>("classdiagram")
                .WithDescription("Generate a Mermaid Class Diagram for a C# file")
                .WithExample("classdiagram", "Services/UserService.cs")
                .WithExample("classdiagram", "Models/User.cs", "--inheritance", "--dependencies");
        });

        return app.Run(args);
    }
}