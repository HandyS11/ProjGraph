using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProjGraph.Lib;
using ProjGraph.Lib.Interfaces;
using ProjGraph.Lib.Rendering;
using ProjGraph.Lib.Services;
using System.ComponentModel;
using System.Text.Json;

namespace ProjGraph.Mcp;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = "ProjGraph",
                Version = "1.0.0"
            };
        })
        .WithStdioServerTransport()
        .WithTools<ProjGraphTools>();

        builder.Services.AddSingleton<GraphService>();
        builder.Services.AddSingleton<IEfAnalysisService, EfAnalysisService>();
        builder.Services.AddSingleton<ProjGraphTools>();

        var host = builder.Build();
        await host.RunAsync();
    }
}

[McpServerToolType]
public class ProjGraphTools(GraphService graphService, IEfAnalysisService efService)
{
    [McpServerTool]
    [Description("Analyzes a .NET solution or project file and returns the dependency graph.")]
    public string GetProjectGraph(
        [Description("Absolute path to the project or solution file.")] string path,
        [Description("Include NuGet packages?")] bool includePackages = false)
    {
        try
        {
            var graph = graphService.BuildGraph(path);

            var nodes = graph.Projects.Select(p => new
            {
                id = p.Id.ToString("N"),
                name = p.Name,
                type = p.Type.ToString(),
                framework = p.Framework
            }).ToList();

            var edges = graph.Dependencies.Select(d => new
            {
                sourceId = d.SourceId.ToString("N"),
                targetId = d.TargetId.ToString("N"),
                type = d.Type.ToString()
            }).ToList();

            return JsonSerializer.Serialize(new { nodes, edges }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error analyzing project: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description(
        "Generates a Mermaid Entity Relationship Diagram (ERD) based on an Entity Framework Core DbContext found in the specified path.")]
#pragma warning disable IDE1006
    public async Task<string> GetErd(
#pragma warning restore IDE1006
        [Description("Absolute path to the solution (.sln), project (.csproj), or specific DbContext file (.cs).")]
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
