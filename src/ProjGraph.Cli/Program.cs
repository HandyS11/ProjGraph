using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Cli.Commands;
using ProjGraph.Cli.Infrastructure;
using ProjGraph.Lib;
using Spectre.Console.Cli;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ProjGraph.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        var services = new ServiceCollection();

        // Register Library services
        services.AddProjGraphLib();

        // Register CLI-specific services
        services.AddSingleton<DiagramOutputWriter>();

        var app = CreateApp(new TypeRegistrar(services));

        app.Configure(Configure);

        return app.Run(args);
    }

    /// <summary>
    /// Applies the application settings and registers the commands. Tests call this directly so they
    /// can run the real configuration against their own console.
    /// </summary>
    /// <param name="config">The configurator of the command app.</param>
    internal static void Configure(IConfigurator config)
    {
        const string packageDiagramCommandName = "visualize";
        const string erdCommandName = "erd";
        const string classDiagramCommandName = "classdiagram";

        config.SetApplicationName("projgraph");
        config.SetApplicationVersion(GetPackageVersion(typeof(Program).Assembly));

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
    }

    /// <summary>
    /// Gets the package version an assembly was built with. Spectre's
    /// <c>UseAssemblyInformationalVersion</c> reads the entry assembly, which is the test host under tests,
    /// and keeps the <c>+commit</c> build metadata the SDK appends, so <see cref="Configure"/> passes the
    /// CLI assembly here and the metadata is trimmed.
    /// </summary>
    /// <param name="assembly">The assembly to read the version from.</param>
    /// <returns>The package version, e.g. <c>1.2.0</c> or <c>1.2.0-beta.1</c>.</returns>
    internal static string GetPackageVersion(Assembly assembly)
    {
        // The SDK always generates the attribute, but Configure runs for every command, so a build
        // without it falls back to the assembly version instead of failing them all.
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString(3)
                      ?? "0.0.0";
        var metadataStart = version.IndexOf('+', StringComparison.Ordinal);
        return metadataStart < 0 ? version : version[..metadataStart];
    }

    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Spectre.Console.Cli reflects over the command and settings types. ProjGraph.Cli and " +
                        "Spectre.Console.Cli are trimmer root assemblies, so that metadata is kept, and " +
                        "tests/ProjGraph.Tests.Smoke.Aot runs every command natively.")]
    private static CommandApp CreateApp(TypeRegistrar registrar)
    {
        return new CommandApp(registrar);
    }
}
