using Microsoft.Extensions.Logging.Abstractions;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Lib.ClassDiagram.Rendering;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;
using ProjGraph.Lib.Dependencies.Application;
using ProjGraph.Lib.Dependencies.Application.UseCases;
using ProjGraph.Lib.Dependencies.Rendering;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Lib.EntityFramework.Rendering;
using ProjGraph.Mcp;

namespace ProjGraph.Tests.Integration.Mcp.Helpers;

internal static class McpTestHelper
{
    public static ProjGraphTools CreateTools()
    {
        return CreateTools(new CollectingOutputConsole());
    }

    public static ProjGraphTools CreateTools(CollectingOutputConsole console, DiagramResourceCache? cache = null)
    {
        var fs = new PhysicalFileSystem();
        var slnParser = new SlnParser(fs);
        var slnxParser = new SlnxParser(fs);
        var projectParser = new ProjectParser(fs);
        var graphService = new GraphService(new BuildGraphUseCase(slnParser, slnxParser, projectParser,
            new ProjectDiscoveryService(projectParser, fs, console,
                NullLogger<ProjectDiscoveryService>.Instance), fs, console,
            NullLogger<BuildGraphUseCase>.Instance));

        var compilationFactory = new CompilationFactory();
        var workspaceTypeDiscovery = new WorkspaceTypeDiscovery(fs);
        var symbolResolver = new SymbolResolver(workspaceTypeDiscovery, fs);
        var typeProcessor = new TypeProcessor(symbolResolver);

        var entityFileDiscovery = new EntityFileDiscovery(fs);
        var analyzer = new EfModelAnalyzer(compilationFactory, fs, entityFileDiscovery);
        var efService = new EfAnalysisService(new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));

        var discoverCsFilesUseCase = new DiscoverCsFilesUseCase(fs);
        var classService = new ClassAnalysisService(
            new AnalyzeFileUseCase(compilationFactory, typeProcessor, fs),
            new AnalyzeDirectoryUseCase(discoverCsFilesUseCase, compilationFactory, typeProcessor, fs));

        return new ProjGraphTools(
            new AnalysisServices(graphService, efService, classService, new StatsService(graphService)),
            discoverCsFilesUseCase,
            new DiagramRenderers(new MermaidGraphRenderer(), new MermaidClassDiagramRenderer(),
                new MermaidErdRenderer()),
            fs,
            cache ?? new DiagramResourceCache(),
            new WorkspaceRootService(fs),
            console);
    }
}
