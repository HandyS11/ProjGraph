using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProjGraph.Core.Models;
using ProjGraph.Lib;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.ProjectGraph.Application;
using System.ComponentModel;
using System.Reflection;

namespace ProjGraph.Mcp;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var version = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = "ProjGraph", Version = version };
            })
            .WithStdioServerTransport()
            .WithTools<ProjGraphTools>();

        // Register Library services
        builder.Services.AddProjGraphLib();

        builder.Services.AddSingleton<ProjGraphTools>();

        var host = builder.Build();
        await host.RunAsync();
    }
}

[McpServerToolType]
public class ProjGraphTools(
    IGraphService graphService,
    IEfAnalysisService efService,
    IClassAnalysisService classService,
    IDiagramRenderer<SolutionGraph> graphRenderer,
    IDiagramRenderer<ClassModel> classRenderer,
    IDiagramRenderer<EfModel> erdRenderer)
{
    [McpServerTool]
    [Description(
        "Generates a Mermaid class diagram for the types defined in a specific C# file, with options to discover inheritance and related types in the workspace.")]
#pragma warning disable IDE1006
    public async Task<string> GetClassDiagram(
#pragma warning restore IDE1006
        [Description("Absolute path to the .cs file to analyze.")]
        string filePath,
        [Description("Whether to search the workspace for base classes and interfaces.")]
        bool includeInheritance = false,
        [Description("Whether to search for and include other classes used as properties or fields.")]
        bool includeDependencies = false,
        [Description("How many levels of relationships to follow (default: 1).")]
        int depth = 1,
        [Description("Whether to include the title in the diagram (default: true).")]
        bool showTitle = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}", filePath);
        }

        if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Only .cs files are supported. Got: {filePath}", nameof(filePath));
        }

        var model = await classService.AnalyzeFileAsync(filePath, includeInheritance, includeDependencies, depth);

        return classRenderer.Render(model, new DiagramOptions(showTitle));
    }

    [McpServerTool]
    [Description("Analyzes a .NET solution or project file and returns the dependency graph as a Mermaid diagram.")]
    public string GetProjectGraph(
        [Description("Absolute path to the project or solution file.")]
        string path,
        [Description("Whether to include the title in the diagram (default: true).")]
        bool showTitle = true)
    {
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

        return graphRenderer.Render(graph, new DiagramOptions(showTitle));
    }

    [McpServerTool]
    [Description(
        "Generates a Mermaid Entity Relationship Diagram (ERD) from an Entity Framework Core DbContext or ModelSnapshot file, including entities, properties, relationships, constraints, and inherited properties from base classes.")]
#pragma warning disable IDE1006
    public async Task<string> GetErd(
#pragma warning restore IDE1006
        [Description("Absolute path to a .cs file containing a DbContext or ModelSnapshot.")]
        string path,
        [Description("Specific DbContext or ModelSnapshot class name to use if multiple are present.")]
        string? contextName = null,
        [Description("Whether to include the title in the diagram (default: true).")]
        bool showTitle = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }

        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Only .cs files are supported. Got: {path}", nameof(path));
        }

        EfModel model;

        if (path.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
        {
            var snapshots = await efService.DiscoverSnapshotsAsync(path);

            var snapshotName = !string.IsNullOrEmpty(contextName)
                ? contextName
                : snapshots.Count switch
                {
                    0 => throw new InvalidOperationException($"No ModelSnapshot found in '{path}'."),
                    1 => snapshots[0],
                    _ => throw new InvalidOperationException(
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
