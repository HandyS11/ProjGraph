using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Dependencies.Application;
using ProjGraph.Lib.Dependencies.Application.UseCases;
using ProjGraph.Lib.Dependencies.Rendering;

namespace ProjGraph.Lib.Dependencies;

/// <summary>
/// Provides extension methods for registering ProjectGraph services.
/// </summary>
public static class ProjectGraphServiceRegistration
{
    /// <summary>
    /// Adds ProjectGraph specific services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProjGraphDependencies(this IServiceCollection services)
    {
        services.AddSingleton<BuildGraphUseCase>();
        services.AddSingleton<IGraphService, GraphService>();
        services.AddSingleton<IStatsService, StatsService>();

        // Renderers (all stateless — registered as Singleton)
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
