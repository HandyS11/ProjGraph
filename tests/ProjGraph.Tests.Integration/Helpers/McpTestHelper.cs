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

namespace ProjGraph.Tests.Integration.Helpers;

public static class McpTestHelper
{
    public static ProjGraphTools CreateTools()
    {
        var fs = new PhysicalFileSystem();
        var slnParser = new SlnParser(fs);
        var slnxParser = new SlnxParser(fs);
        var projectParser = new ProjectParser();
        var graphService = new GraphService(new BuildGraphUseCase(slnParser, slnxParser, projectParser,
            new ProjectDiscoveryService(projectParser, fs), fs));

        var compilationFactory = new CompilationFactory();
        var typeProcessor = new TypeProcessor();

        var analyzer = new EfModelAnalyzer(compilationFactory, fs);
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






