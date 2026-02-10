using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.ProjectGraph.Application;
using ProjGraph.Lib.ProjectGraph.Application.UseCases;
using ProjGraph.Lib.ProjectGraph.Rendering;

namespace ProjGraph.Lib.ProjectGraph;

/// <summary>
/// Provides extension methods for registering ProjectGraph services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds ProjectGraph specific services to the service collection.
    /// </summary>
    public static IServiceCollection AddProjGraphProjectGraph(this IServiceCollection services)
    {
        services.AddSingleton<BuildGraphUseCase>();
        services.AddSingleton<IGraphService, GraphService>();

        // Renderers
        services.AddSingleton<TreeGraphRenderer>();
        services.AddSingleton<FlatGraphRenderer>();
        services.AddSingleton<MermaidGraphRenderer>();

        // Register as interface for collection injection
        services.AddSingleton<IDiagramRenderer<SolutionGraph>>(sp => sp.GetRequiredService<TreeGraphRenderer>());
        services.AddSingleton<IDiagramRenderer<SolutionGraph>>(sp => sp.GetRequiredService<FlatGraphRenderer>());
        services.AddSingleton<IDiagramRenderer<SolutionGraph>>(sp => sp.GetRequiredService<MermaidGraphRenderer>());

        return services;
    }
}