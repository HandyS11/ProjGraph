using Microsoft.Extensions.Logging.Abstractions;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Lib.ClassDiagram.Rendering;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Lib.EntityFramework.Rendering;
using ProjGraph.Lib.ProjectGraph.Application;
using ProjGraph.Lib.ProjectGraph.Application.UseCases;
using ProjGraph.Lib.ProjectGraph.Rendering;
using ProjGraph.Mcp;
using NullOutputConsole = ProjGraph.Tests.Shared.Helpers.NullOutputConsole;

namespace ProjGraph.Tests.Integration.Mcp.Helpers;

internal static class McpTestHelper
{
    public static ProjGraphTools CreateTools()
    {
        var fs = new PhysicalFileSystem();
        var console = new NullOutputConsole();
        var slnParser = new SlnParser(fs);
        var slnxParser = new SlnxParser(fs);
        var projectParser = new ProjectParser(fs);
        var graphService = new GraphService(new BuildGraphUseCase(slnParser, slnxParser, projectParser,
            new ProjectDiscoveryService(projectParser, fs, console,
                NullLogger<ProjectDiscoveryService>.Instance), fs, console,
            NullLogger<BuildGraphUseCase>.Instance));

        var compilationFactory = new CompilationFactory();
        var workspaceTypeDiscovery = new WorkspaceTypeDiscovery();
        var symbolResolver = new SymbolResolver(workspaceTypeDiscovery);
        var typeProcessor = new TypeProcessor(symbolResolver);

        var entityFileDiscovery = new EntityFileDiscovery();
        var analyzer = new EfModelAnalyzer(compilationFactory, fs, entityFileDiscovery);
        var efService = new EfAnalysisService(new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));
        var classService = new ClassAnalysisService(new AnalyzeFileUseCase(compilationFactory, typeProcessor, fs));

        return new ProjGraphTools(
            graphService,
            efService,
            classService,
            new MermaidGraphRenderer(),
            new MermaidClassDiagramRenderer(),
            new MermaidErdRenderer());
    }
}
