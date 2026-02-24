using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
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
    DiscoverCsFilesUseCase discoverCsFilesUseCase,
    MermaidGraphRenderer graphRenderer,
    IDiagramRenderer<ClassModel> classRenderer,
    IDiagramRenderer<EfModel> erdRenderer)
{
    [McpServerTool(Name = "get_class_diagram")]
    [Description(
        "Generates a Mermaid class diagram for the types defined in a specific C# file or directory, with options to discover inheritance and related types in the workspace.")]
    public async Task<string> GetClassDiagramAsync(
        [Description("Absolute path to the .cs file or directory to analyze.")]
        string path,
        [Description("Analysis and discovery options.")]
        AnalysisOptions? options = null,
        [Description("Whether to include the title in the diagram (default: true).")]
        bool showTitle = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException($"Path not found: {path}", path);
        }

        ClassModel model;
        var warningMarkup = string.Empty;

        if (Directory.Exists(path))
        {
            var files = discoverCsFilesUseCase.Execute(path);
            if (files.Count > 50)
            {
                warningMarkup = $"%% WARNING: Scanning {files.Count} files. Large diagrams may be hard to read.\n";
            }

            model = await classService.AnalyzeDirectoryAsync(path, options);
        }
        else
        {
            FilePathGuard.RequireCsFile(path);
            model = await classService.AnalyzeFileAsync(path, options);
        }

        var diagram = classRenderer.Render(model, new DiagramOptions(showTitle));
        return warningMarkup + diagram;
    }

    [McpServerTool(Name = "get_project_graph")]
    [Description("Analyzes a .NET solution or project file and returns the dependency graph as a Mermaid diagram.")]
    public Task<string> GetProjectGraphAsync(
        [Description("Absolute path to the project or solution file.")]
        string path,
        [Description("Whether to include the title in the diagram (default: true).")]
        bool showTitle = true,
        [Description("Whether to include NuGet package dependencies in the graph (default: false).")]
        bool includePackages = false,
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

        var graph = graphService.BuildGraph(path, includePackages);

        return Task.FromResult(graphRenderer.Render(graph,
            new DiagramOptions(showTitle, IncludePackages: includePackages)));
    }

    [McpServerTool(Name = "get_erd")]
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
