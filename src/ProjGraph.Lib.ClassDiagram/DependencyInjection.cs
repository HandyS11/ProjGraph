using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Lib.ClassDiagram.Rendering;
using ProjGraph.Lib.Core.Abstractions;

namespace ProjGraph.Lib.ClassDiagram;

/// <summary>
/// Provides extension methods for registering Class Diagram ProjGraph services.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Adds Class Diagram ProjGraph services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProjGraphClassDiagram(this IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<IWorkspaceTypeDiscovery, WorkspaceTypeDiscovery>();
        services.AddSingleton<ISymbolResolver, SymbolResolver>();
        services.AddSingleton<ITypeProcessor, TypeProcessor>();
        services.AddSingleton<IDiagramRenderer<ClassModel>, MermaidClassDiagramRenderer>();

        // Use Cases
        services.AddSingleton<DiscoverCsFilesUseCase>();
        services.AddSingleton<AnalyzeFileUseCase>();
        services.AddSingleton<AnalyzeDirectoryUseCase>();

        // Services
        services.AddSingleton<IClassAnalysisService, ClassAnalysisService>();

        return services;
    }
}
