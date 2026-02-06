using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;

namespace ProjGraph.Lib.Core;

/// <summary>
/// Provides extension methods for registering Core ProjGraph services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds core ProjGraph services to the service collection.
    /// </summary>
    public static IServiceCollection AddProjGraphCore(this IServiceCollection services)
    {
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