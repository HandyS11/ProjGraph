using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.ProjectGraph.Application;
using ProjGraph.Lib.ProjectGraph.Rendering;
using System.ComponentModel;
using System.Reflection;

namespace ProjGraph.Mcp;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var version = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddMcpServer(options =>
                options.ServerInfo = new Implementation
                {
                    Name = "ProjGraph",
                    Version = version
                })
            .WithStdioServerTransport()
            .WithTools<ProjGraphTools>();

        // Register Library services
        builder.Services.AddProjGraphLib();

        // Override IOutputConsole with a no-op to prevent ANSI markup on stdout (JSON-RPC transport)
        builder.Services.AddSingleton<IOutputConsole, NullOutputConsole>();

        builder.Services.AddSingleton<ProjGraphTools>();

        var host = builder.Build();
        await host.RunAsync();
    }
}

[McpServerToolType]
internal sealed class ProjGraphTools(
    IGraphService graphService,
    IEfAnalysisService efService,
    IClassAnalysisService classService,
    MermaidGraphRenderer graphRenderer,
    IDiagramRenderer<ClassModel> classRenderer,
    IDiagramRenderer<EfModel> erdRenderer)
{
    [McpServerTool(Name = "GetClassDiagram")]
    [Description(
        "Generates a Mermaid class diagram for the types defined in a specific C# file, with options to discover inheritance and related types in the workspace.")]
    public async Task<string> GetClassDiagramAsync(
        [Description("Absolute path to the .cs file to analyze.")]
        string path,
        [Description("Whether to search the workspace for base classes and interfaces.")]
        bool includeInheritance = false,
        [Description("Whether to search for and include other classes used as properties or fields.")]
        bool includeDependencies = false,
        [Description("Whether to display properties and fields in the class diagram (default: true).")]
        bool includeProperties = true,
        [Description("Whether to display functions/methods in the class diagram (default: true).")]
        bool includeFunctions = true,
        [Description("How many levels of relationships to follow (default: 1).")]
        int depth = 1,
        [Description("Whether to include the title in the diagram (default: true).")]
        bool showTitle = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }

        FilePathGuard.RequireCsFile(path);

        var model = await classService.AnalyzeFileAsync(
            path,
            includeInheritance,
            includeDependencies,
            includeProperties,
            includeFunctions,
            depth);

        return classRenderer.Render(model, new DiagramOptions(showTitle));
    }

    [McpServerTool(Name = "GetProjectGraph")]
    [Description("Analyzes a .NET solution or project file and returns the dependency graph as a Mermaid diagram.")]
    public Task<string> GetProjectGraphAsync(
        [Description("Absolute path to the project or solution file.")]
        string path,
        [Description("Whether to include the title in the diagram (default: true).")]
        bool showTitle = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".sln" or ".slnx" or ".csproj"))
        {
            throw new ArgumentException(
                $"Unsupported file type '{extension}'. Expected .sln, .slnx, or .csproj.", nameof(path));
        }

        var graph = graphService.BuildGraph(path);

        return Task.FromResult(graphRenderer.Render(graph, new DiagramOptions(showTitle)));
    }

    [McpServerTool(Name = "GetErd")]
    [Description(
        "Generates a Mermaid Entity Relationship Diagram (ERD) from an Entity Framework Core DbContext or ModelSnapshot file, including entities, properties, relationships, constraints, and inherited properties from base classes.")]
    public async Task<string> GetErdAsync(
        [Description("Absolute path to a .cs file containing a DbContext or ModelSnapshot.")]
        string path,
        [Description("Specific DbContext or ModelSnapshot class name to use if multiple are present.")]
        string? contextName = null,
        [Description("Whether to include the title in the diagram (default: true).")]
        bool showTitle = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }

        FilePathGuard.RequireCsFile(path);

        EfModel model;

        if (path.EndsWith($"ModelSnapshot{FilePathGuard.CSharpExtension}", StringComparison.OrdinalIgnoreCase))
        {
            var snapshots = await efService.DiscoverSnapshotsAsync(path);

            var snapshotName = !string.IsNullOrEmpty(contextName)
                ? contextName
                : snapshots.Count switch
                {
                    0 => throw new AnalysisException($"No ModelSnapshot found in '{path}'."),
                    1 => snapshots[0],
                    _ => throw new AnalysisException(
                        $"Multiple ModelSnapshots found in '{path}': {string.Join(", ", snapshots)}. Specify one using the contextName parameter.")
                };

            model = await efService.AnalyzeSnapshotAsync(path, snapshotName);
        }
        else
        {
            model = await efService.AnalyzeContextAsync(path, contextName);
        }

        return erdRenderer.Render(model, new DiagramOptions(showTitle));
    }
}
