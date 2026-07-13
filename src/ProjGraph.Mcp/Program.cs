using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ProjGraph.Core.Models;
using ProjGraph.Lib;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Dependencies.Application;
using ProjGraph.Lib.Dependencies.Rendering;
using ProjGraph.Lib.EntityFramework.Application;
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

        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddConsole(options =>
            options.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.Services.AddMcpServer(options =>
                options.ServerInfo = new Implementation
                {
                    Name = "ProjGraph",
                    Version = version
                })
            .WithStdioServerTransport()
            .WithTools<ProjGraphTools>()
            .WithPrompts<ProjGraphPrompts>()
            .WithResources<ProjGraphResources>();

        // Register Library services
        builder.Services.AddProjGraphLib();

        // Register MCP integration services
        builder.Services.AddSingleton<DiagramResourceCache>();
        builder.Services.AddSingleton<WorkspaceRootService>();

        // Override IOutputConsole with a warning-collecting console: it never writes to stdout
        // (reserved for the JSON-RPC transport) but captures skip/partial-analysis warnings so the
        // tools can surface them in their results instead of silently discarding them.
        builder.Services.AddSingleton<CollectingOutputConsole>();
        builder.Services.AddSingleton<IOutputConsole>(sp => sp.GetRequiredService<CollectingOutputConsole>());

        builder.Services.AddSingleton<DiagramRenderers>(sp => new DiagramRenderers(
            sp.GetRequiredService<MermaidGraphRenderer>(),
            sp.GetRequiredService<IDiagramRenderer<ClassModel>>(),
            sp.GetRequiredService<IDiagramRenderer<EfModel>>()));
        builder.Services.AddSingleton<AnalysisServices>(sp => new AnalysisServices(
            sp.GetRequiredService<IGraphService>(),
            sp.GetRequiredService<IEfAnalysisService>(),
            sp.GetRequiredService<IClassAnalysisService>(),
            sp.GetRequiredService<IStatsService>()));
        builder.Services.AddSingleton<ProjGraphTools>();

        var host = builder.Build();
        await host.RunAsync();
    }
}
