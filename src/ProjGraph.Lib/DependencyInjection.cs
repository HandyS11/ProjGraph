using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Lib.ClassDiagram;
using ProjGraph.Lib.Core;
using ProjGraph.Lib.EntityFramework;
using ProjGraph.Lib.ProjectGraph;

namespace ProjGraph.Lib;

/// <summary>
/// Provides extension methods for registering services and dependencies
/// required by the ProjGraph library.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Adds the ProjGraph library services and dependencies to the specified
    /// <see cref="IServiceCollection"/> for dependency injection.
    /// </summary>
    /// <param name="services">The service collection to which the dependencies will be added.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> with ProjGraph services registered.</returns>
    public static IServiceCollection AddProjGraphLib(this IServiceCollection services)
    {
        // Core services
        services.AddProjGraphCore();

        // ProjectGraph services
        services.AddProjGraphProjectGraph();

        // EntityFramework services
        services.AddProjGraphEntityFramework();

        // ClassDiagram services
        services.AddProjGraphClassDiagram();

        return services;
    }
}
