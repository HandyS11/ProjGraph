using ProjGraph.Cli.Commands;
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
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.AddCommand<VisualizeCommand>("visualize");
            config.AddCommand<ErdCommand>("erd");
            config.AddCommand<ClassDiagramCommand>("classdiagram");
        });
        return app;
    }

    public static async Task<(int ExitCode, string Output)> RunCommandAsync(params string[] args)
    {
        var app = CreateApp();
        var output = new StringBuilder();
        await using var writer = new StringWriter(output);

        var originalOut = Console.Out;
        Console.SetOut(writer);

        try
        {
            var exitCode = await app.RunAsync(args);
            return (exitCode, output.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
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