using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Application.Interfaces;
using ProjGraph.Lib.Application.Services;
using ProjGraph.Lib.Application.UseCases.ClassAnalysis;
using ProjGraph.Lib.Application.UseCases.EfAnalysis;
using ProjGraph.Lib.Application.UseCases.SolutionGraph;
using ProjGraph.Lib.Infrastructure.Analysis;
using ProjGraph.Lib.Infrastructure.Analysis.ClassAnalysis;
using ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis;
using ProjGraph.Lib.Infrastructure.Parsers;
using ProjGraph.Lib.Infrastructure.Rendering;

namespace ProjGraph.Lib;

/// <summary>
/// Provides extension methods for registering services and dependencies
/// required by the ProjGraph library.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the ProjGraph library services and dependencies to the specified
    /// <see cref="IServiceCollection"/> for dependency injection.
    /// </summary>
    /// <param name="services">The service collection to which the dependencies will be added.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> with ProjGraph services registered.</returns>
    public static IServiceCollection AddProjGraphLib(this IServiceCollection services)
    {
        // Infrastructure - General
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IOutputConsole, SpectreOutputConsole>();

        // Infrastructure - Parsers
        services.AddSingleton<ISlnParser, SlnParser>();
        services.AddSingleton<ISlnxParser, SlnxParser>();
        services.AddSingleton<IProjectParser, ProjectParser>();

        // Infrastructure - Technical Details
        services.AddSingleton<ICompilationFactory, CompilationFactory>();
        services.AddSingleton<ITypeProcessor, TypeProcessor>();
        services.AddSingleton<IProjectDiscoveryService, ProjectDiscoveryService>();
        services.AddSingleton<IEfModelAnalyzer, EfModelAnalyzer>();

        // Infrastructure - Renderers
        services.AddSingleton<IDiagramRenderer<SolutionGraph>, MermaidGraphRenderer>();
        services.AddSingleton<IDiagramRenderer<ClassModel>, MermaidClassDiagramRenderer>();
        services.AddSingleton<IDiagramRenderer<EfModel>, MermaidErdRenderer>();

        // Application - Use Cases
        services.AddSingleton<BuildGraphUseCase>();
        services.AddSingleton<AnalyzeFileUseCase>();
        services.AddSingleton<AnalyzeContextUseCase>();
        services.AddSingleton<DiscoverContextsUseCase>();
        services.AddSingleton<AnalyzeSnapshotUseCase>();
        services.AddSingleton<DiscoverSnapshotsUseCase>();

        // Application - Services (Public API)
        services.AddSingleton<IGraphService, GraphService>();
        services.AddSingleton<IEfAnalysisService, EfAnalysisService>();
        services.AddSingleton<IClassAnalysisService, ClassAnalysisService>();

        return services;
    }
}