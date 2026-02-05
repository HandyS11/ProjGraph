using ProjGraph.Lib.Application.Services;
using ProjGraph.Lib.Application.UseCases.ClassAnalysis;
using ProjGraph.Lib.Application.UseCases.EfAnalysis;
using ProjGraph.Lib.Application.UseCases.SolutionGraph;
using ProjGraph.Lib.Infrastructure.Analysis;
using ProjGraph.Lib.Infrastructure.Analysis.ClassAnalysis;
using ProjGraph.Lib.Infrastructure.Analysis.EfAnalysis;
using ProjGraph.Lib.Infrastructure.Parsers;
using ProjGraph.Lib.Infrastructure.Rendering;
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
