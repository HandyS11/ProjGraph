using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Cli.Commands;
using ProjGraph.Cli.Infrastructure;
using ProjGraph.Lib;
using ProjGraph.Tests.Shared.Helpers;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Text;

namespace ProjGraph.Tests.Integration.Cli.Helpers;

public static class CliTestHelpers
{
    public static string GetSamplePath(string relativePath)
    {
        return TestPathHelper.GetSamplePath(relativePath);
    }

    public static string GetRootPath(string relativePath)
    {
        return TestPathHelper.GetRootPath(relativePath);
    }

    public static CommandApp CreateApp()
    {
        var services = new ServiceCollection();
        services.AddProjGraphLib();
        services.AddSingleton<DiagramOutputWriter>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
            // Mirror the production app's parser configuration (Program.cs) so tests exercise the
            // same strict-parsing behaviour users get.
            config.Settings.StrictParsing = true;
            config.AddCommand<VisualizeCommand>("visualize");
            config.AddCommand<ErdCommand>("erd");
            config.AddCommand<ClassDiagramCommand>("classdiagram");
            config.AddCommand<StatsCommand>("stats");
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
                Out = new AnsiConsoleOutput(writer),
                Interactive = InteractionSupport.No
            };

            var console = AnsiConsole.Create(settings);
            console.Profile.Capabilities.Unicode = true;
            console.Profile.Width = 200;
            AnsiConsole.Console = console;

            action();

            // Ensure all output is flushed before we read it
            writer.Flush();

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
