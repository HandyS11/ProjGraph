using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Lib.EntityFramework.Rendering;

namespace ProjGraph.Lib.EntityFramework;

/// <summary>
/// Provides extension methods for registering Entity Framework ProjGraph services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Entity Framework ProjGraph services to the service collection.
    /// </summary>
    public static IServiceCollection AddProjGraphEntityFramework(this IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<IEfModelAnalyzer, EfModelAnalyzer>();
        services.AddSingleton<IDiagramRenderer<EfModel>, MermaidErdRenderer>();

        // Use Cases
        services.AddSingleton<AnalyzeContextUseCase>();
        services.AddSingleton<DiscoverContextsUseCase>();
        services.AddSingleton<AnalyzeSnapshotUseCase>();
        services.AddSingleton<DiscoverSnapshotsUseCase>();

        // Services
        services.AddSingleton<IEfAnalysisService, EfAnalysisService>();

        return services;
    }
}
