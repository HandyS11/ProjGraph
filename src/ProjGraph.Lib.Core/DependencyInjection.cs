using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;

namespace ProjGraph.Lib.Core;

/// <summary>
/// Provides extension methods for registering Core ProjGraph services.
/// </summary>
public static class CoreServiceRegistration
{
    /// <summary>
    /// Adds core ProjGraph services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddProjGraphCore(this IServiceCollection services)
    {
        // Logging - default to NullLogger (consumers can override with real logging)
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Infrastructure - General
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IOutputConsole, SpectreOutputConsole>();
        services.AddSingleton<ICompilationFactory, CompilationFactory>();

        // Infrastructure - Parsers
        services.AddSingleton<ISlnParser, SlnParser>();
        services.AddSingleton<ISlnxParser, SlnxParser>();
        services.AddSingleton<IProjectParser, ProjectParser>();
        services.AddSingleton<IProjectDiscoveryService, ProjectDiscoveryService>();

        return services;
    }
}
