using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjGraph.Core.Models;
using ProjGraph.Lib;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.ProjectGraph.Application;

namespace ProjGraph.Tests.Contract;

/// <summary>
/// Verifies that the DI container wired by <see cref="ServiceRegistration.AddProjGraphLib"/>
/// can resolve all critical service interfaces.
/// </summary>
public class DiWiringTests
{
    private readonly ServiceProvider _provider;

    public DiWiringTests()
    {
        var services = new ServiceCollection();
        services.AddProjGraphLib();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    [Theory]
    [InlineData(typeof(IFileSystem))]
    [InlineData(typeof(IOutputConsole))]
    [InlineData(typeof(ICompilationFactory))]
    [InlineData(typeof(IGraphService))]
    [InlineData(typeof(IEfAnalysisService))]
    [InlineData(typeof(IClassAnalysisService))]
    [InlineData(typeof(IDiagramRenderer<SolutionGraph>))]
    [InlineData(typeof(IDiagramRenderer<EfModel>))]
    [InlineData(typeof(IDiagramRenderer<ClassModel>))]
    [InlineData(typeof(ISlnParser))]
    [InlineData(typeof(ISlnxParser))]
    [InlineData(typeof(IProjectParser))]
    [InlineData(typeof(IProjectDiscoveryService))]
    [InlineData(typeof(ILogger<DiWiringTests>))]
    public void AddProjGraphLib_ShouldResolve_CriticalService(Type serviceType)
    {
        var service = _provider.GetService(serviceType);
        service.Should().NotBeNull($"{serviceType.Name} should be resolvable from the container");
    }

    [Fact]
    public void AddProjGraphLib_ShouldRegister_MultipleGraphRenderers()
    {
        var renderers = _provider.GetServices<IDiagramRenderer<SolutionGraph>>().ToList();
        renderers.Should().HaveCountGreaterThanOrEqualTo(3,
            "TreeGraphRenderer, FlatGraphRenderer, and MermaidGraphRenderer should all be registered");
    }
}
