using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProjGraph.Lib.Interfaces;
using ProjGraph.Lib.Rendering;
using ProjGraph.Lib.Services;
using ProjGraph.Lib.Services.ClassAnalysis;
using ProjGraph.Lib.Services.EfAnalysis;
using System.ComponentModel;

namespace ProjGraph.Mcp;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = "ProjGraph", Version = "1.0.0" };
            })
            .WithStdioServerTransport()
            .WithTools<ProjGraphTools>();

        builder.Services.AddSingleton<IGraphService, GraphService>();
        builder.Services.AddSingleton<IEfAnalysisService, EfAnalysisService>();
        builder.Services.AddSingleton<IClassAnalysisService, ClassAnalysisService>();
        builder.Services.AddSingleton<ProjGraphTools>();

        var host = builder.Build();
        await host.RunAsync();
    }
}

[McpServerToolType]
public class ProjGraphTools(
    IGraphService graphService,
    IEfAnalysisService efService,
    IClassAnalysisService classService)
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
        int depth = 1)
    {
        try
        {
            var model = await classService.AnalyzeFileAsync(filePath, includeInheritance, includeDependencies, depth);
            return MermaidClassDiagramRenderer.Render(model);
        }
        catch (Exception ex)
        {
            return $"Error generating class diagram: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Analyzes a .NET solution or project file and returns the dependency graph as a Mermaid diagram.")]
    public string GetProjectGraph(
        [Description("Absolute path to the project or solution file.")]
        string path)
    {
        try
        {
            var graph = graphService.BuildGraph(path);
            return MermaidGraphRenderer.Render(graph);
        }
        catch (Exception ex)
        {
            return $"Error analyzing project: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description(
        "Generates a Mermaid Entity Relationship Diagram (ERD) from an Entity Framework Core DbContext file, including entities, properties, relationships, constraints, and inherited properties from base classes.")]
#pragma warning disable IDE1006
    public async Task<string> GetErd(
#pragma warning restore IDE1006
        [Description("Absolute path to the DbContext .cs file.")]
        string path,
        [Description("Specific DbContext class name to use if multiple are present.")]
        string? contextName = null)
    {
        try
        {
            var model = await efService.AnalyzeContextAsync(path, contextName);
            return MermaidErdRenderer.Render(model);
        }
        catch (Exception ex)
        {
            return $"Error generating ERD: {ex.Message}";
        }
    }
}