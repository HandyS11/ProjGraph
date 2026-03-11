using ModelContextProtocol;
using ModelContextProtocol.Server;
using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.ProjectGraph.Application;
using System.ComponentModel;
using System.Text.Json;

namespace ProjGraph.Mcp;

[McpServerToolType]
internal sealed class ProjGraphTools(
    AnalysisServices analysisServices,
    IDiscoverCsFilesUseCase discoverCsFilesUseCase,
    DiagramRenderers renderers,
    IFileSystem fileSystem,
    DiagramResourceCache cache,
    McpServer server,
    WorkspaceRootService rootService)
{
    [McpServerTool(Name = "get_class_diagram")]
    [Description(
        "Generates a Mermaid class diagram for the types defined in a specific C# file or directory, with options to discover inheritance and related types in the workspace.")]
    public async Task<string> GetClassDiagramAsync(
        [Description("Absolute path to the .cs file or directory to analyze.")]
        string path,
        [Description("Analysis and discovery options.")]
        AnalysisOptions? options = null,
        [Description("Whether to include the title in the diagram (default: true).")]
        bool showTitle = true,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        path = await PreparePathAsync(path, cancellationToken);

        if (!fileSystem.FileExists(path) && !fileSystem.DirectoryExists(path))
            throw new FileNotFoundException($"Path not found: {path}", path);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 1,
            Total = 3,
            Message = "Discovering C# files"
        });

        ClassModel model;
        var warningMarkup = string.Empty;

        if (fileSystem.DirectoryExists(path))
        {
            var files = discoverCsFilesUseCase.Execute(path);
            if (files.Count > 50)
            {
                warningMarkup = $"%% WARNING: Scanning {files.Count} files. Large diagrams may be hard to read.\n";
            }

            progress?.Report(new ProgressNotificationValue
            {
                Progress = 2,
                Total = 3,
                Message = "Analyzing types and members"
            });

            model = await analysisServices.ClassService.AnalyzeDirectoryAsync(path, options);
        }
        else
        {
            FilePathGuard.RequireCsFile(path);

            progress?.Report(new ProgressNotificationValue
            {
                Progress = 2,
                Total = 3,
                Message = "Analyzing types and members"
            });

            model = await analysisServices.ClassService.AnalyzeFileAsync(path, options);
        }

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 3,
            Total = 3,
            Message = "Rendering class diagram"
        });

        var diagram = renderers.ClassRenderer.Render(model, new DiagramOptions(showTitle, false));
        var result = warningMarkup + diagram;

        var filename = Path.GetFileName(path);
        await cache.StoreAsync("class", path, "text/plain", result,
            $"Class diagram for {filename}", server, cancellationToken);


        return result;
    }

    [McpServerTool(Name = "get_project_graph")]
    [Description("Analyzes a .NET solution or project file and returns the dependency graph as a Mermaid diagram.")]
    public async Task<string> GetProjectGraphAsync(
        [Description("Absolute path to the project or solution file.")]
        string path,
        [Description("Whether to include the title in the diagram (default: true).")]
        bool showTitle = true,
        [Description("Whether to include NuGet package dependencies in the graph (default: false).")]
        bool includePackages = false,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        path = await PreparePathAsync(path, cancellationToken);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 1,
            Total = 3,
            Message = "Parsing solution file"
        });

        RequireFileExists(path);
        RequireSolutionExtension(path);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 2,
            Total = 3,
            Message = "Building dependency graph"
        });

        var graph = await analysisServices.GraphService.BuildGraphAsync(path, includePackages, cancellationToken);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 3,
            Total = 3,
            Message = "Rendering diagram"
        });

        var diagram = renderers.GraphRenderer.Render(graph,
            new DiagramOptions(showTitle, false, includePackages));

        var filename = Path.GetFileName(path);
        await cache.StoreAsync("graph", path, "text/plain", diagram,
            $"Project graph for {filename}", server, cancellationToken);


        return diagram;
    }

    [McpServerTool(Name = "get_project_stats")]
    [Description(
        "Analyses a .NET solution or project file and returns key architectural metrics: project count, type breakdown, dependency depth statistics, most-referenced (hotspot) projects, and cycle detection.")]
    public async Task<string> GetProjectStatsAsync(
        [Description("Absolute path to a .NET solution (.sln/.slnx) or project (.csproj) file.")]
        string path,
        [Description("Number of top most-referenced projects to include. Defaults to 5.")]
        int topN = 5,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        path = await PreparePathAsync(path, cancellationToken);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 1,
            Total = 3,
            Message = "Parsing solution"
        });

        RequireFileExists(path);
        RequireSolutionExtension(path);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 2,
            Total = 3,
            Message = "Computing dependency metrics"
        });

        var stats = await analysisServices.StatsService.ComputeStatsAsync(path, topN, cancellationToken);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 3,
            Total = 3,
            Message = "Summarizing results"
        });

        var json = JsonSerializer.Serialize(stats);

        var filename = Path.GetFileName(path);
        await cache.StoreAsync("stats", path, "application/json", json,
            $"Stats for {filename}", server, cancellationToken);


        return json;
    }

    [McpServerTool(Name = "get_erd")]
    [Description(
        "Generates a Mermaid Entity Relationship Diagram (ERD) from an Entity Framework Core DbContext or ModelSnapshot file, including entities, properties, relationships, constraints, and inherited properties from base classes.")]
    public async Task<string> GetErdAsync(
        [Description("Absolute path to a .cs file containing a DbContext or ModelSnapshot.")]
        string path,
        [Description("Specific DbContext or ModelSnapshot class name to use if multiple are present.")]
        string? contextName = null,
        [Description("Whether to include the title in the diagram (default: true).")]
        bool showTitle = true,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        path = await PreparePathAsync(path, cancellationToken);

        RequireFileExists(path);
        FilePathGuard.RequireCsFile(path);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 1,
            Total = 3,
            Message = "Parsing EF Core context"
        });

        EfModel model;

        if (path.EndsWith($"ModelSnapshot{FilePathGuard.CSharpExtension}", StringComparison.OrdinalIgnoreCase))
        {
            var snapshots = await analysisServices.EfService.DiscoverSnapshotsAsync(path);

            var snapshotName = !string.IsNullOrEmpty(contextName)
                ? contextName
                : snapshots.Count switch
                {
                    0 => throw new AnalysisException($"No ModelSnapshot found in '{path}'."),
                    1 => snapshots[0],
                    _ => throw new AnalysisException(
                        $"Multiple ModelSnapshots found in '{path}': {string.Join(", ", snapshots)}. Specify one using the contextName parameter.")
                };

            progress?.Report(new ProgressNotificationValue
            {
                Progress = 2,
                Total = 3,
                Message = "Analyzing entities and relationships"
            });

            model = await analysisServices.EfService.AnalyzeSnapshotAsync(path, snapshotName);
        }
        else
        {
            progress?.Report(new ProgressNotificationValue
            {
                Progress = 2,
                Total = 3,
                Message = "Analyzing entities and relationships"
            });

            model = await analysisServices.EfService.AnalyzeContextAsync(path, contextName);
        }

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 3,
            Total = 3,
            Message = "Rendering entity diagram"
        });

        var diagram = renderers.ErdRenderer.Render(model, new DiagramOptions(showTitle, false));

        var filename = Path.GetFileName(path);
        await cache.StoreAsync("erd", path, "text/plain", diagram,
            $"Entity diagram for {filename}", server, cancellationToken);

        return diagram;
    }

    private async Task<string> PreparePathAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return await rootService.TryResolveAsync(path, server, cancellationToken);
    }

    private void RequireFileExists(string path)
    {
        if (!fileSystem.FileExists(path))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }
    }

    private void RequireSolutionExtension(string path)
    {
        var extension = fileSystem.GetExtension(path).ToLowerInvariant();
        if (extension is not (".sln" or ".slnx" or ".csproj"))
        {
            throw new ArgumentException(
                $"Unsupported file type '{extension}'. Expected .sln, .slnx, or .csproj.", nameof(path));
        }
    }
}

internal sealed record DiagramRenderers(
    IDiagramRenderer<SolutionGraph> GraphRenderer,
    IDiagramRenderer<ClassModel> ClassRenderer,
    IDiagramRenderer<EfModel> ErdRenderer);

internal sealed record AnalysisServices(
    IGraphService GraphService,
    IEfAnalysisService EfService,
    IClassAnalysisService ClassService,
    IStatsService StatsService);
