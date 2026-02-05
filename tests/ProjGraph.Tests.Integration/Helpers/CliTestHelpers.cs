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
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text;

namespace ProjGraph.Tests.Integration.Helpers;

public static class CliTestHelpers
{
    public static string GetSamplePath(string relativePath)
    {
        // Split path by both forward and backward slashes to support cross-platform
        var parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var pathParts = new[] { Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "samples" }
            .Concat(parts)
            .ToArray();
        var path = Path.Combine(pathParts);
        return Path.GetFullPath(path);
    }

    public static string GetRootPath(string relativePath)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", relativePath);
        return Path.GetFullPath(path);
    }

    public static CommandApp CreateApp()
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
            config.PropagateExceptions();
            config.AddCommand<VisualizeCommand>("visualize");
            config.AddCommand<ErdCommand>("erd");
            config.AddCommand<ClassDiagramCommand>("classdiagram");
        });
        return app;
    }

    public static string CaptureConsoleOutput(Action action)
    {
        var output = new StringBuilder();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var writer = new StringWriter(output);
        try
        {
            Console.SetOut(writer);
            Console.SetError(writer);

            // Capture AnsiConsole output as well
            var settings = new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No, // Disable ANSI codes for cleaner test output
                ColorSystem = ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(writer)
            };

            var console = AnsiConsole.Create(settings);
            AnsiConsole.Console = console;

            action();

            // Ensure all output is flushed before we read it
            writer.Flush();

            // Give async operations a moment to complete
            Thread.Sleep(100);

            return output.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings());
        }
    }
}