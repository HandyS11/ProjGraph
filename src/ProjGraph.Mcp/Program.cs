using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ProjGraph.Core.Models;
using ProjGraph.Lib;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.ProjectGraph.Rendering;
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
            .WithTools<ProjGraphTools>()
            .WithPrompts<ProjGraphPrompts>()
            .WithResources<ProjGraphResources>();

        // Register Library services
        builder.Services.AddProjGraphLib();

        // Register MCP integration services
        builder.Services.AddSingleton<DiagramResourceCache>();
        builder.Services.AddSingleton<WorkspaceRootService>();

        // Override IOutputConsole with a no-op to prevent ANSI markup on stdout (JSON-RPC transport)
        builder.Services.AddSingleton<IOutputConsole, NullOutputConsole>();

        builder.Services.AddSingleton<DiagramRenderers>(sp => new DiagramRenderers(
            sp.GetRequiredService<MermaidGraphRenderer>(),
            sp.GetRequiredService<IDiagramRenderer<ClassModel>>(),
            sp.GetRequiredService<IDiagramRenderer<EfModel>>()));
        builder.Services.AddSingleton<ProjGraphTools>();

        var host = builder.Build();
        await host.RunAsync();
    }
}
