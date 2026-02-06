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
        services.AddSingleton<IDiagramRenderer<SolutionGraph>, MermaidGraphRenderer>();

        return services;
    }
}