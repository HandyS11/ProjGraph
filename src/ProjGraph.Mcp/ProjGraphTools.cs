using ModelContextProtocol;
using ModelContextProtocol.Server;
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.ClassDiagram.Application.UseCases;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Dependencies.Application;
using ProjGraph.Lib.EntityFramework.Application;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProjGraph.Mcp;

[McpServerToolType]
[method: SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters",
    Justification =
        "This is the MCP tool host: each parameter is an independent DI-injected collaborator that a " +
        "distinct tool method needs. Bundling them behind a wrapper type would obscure the dependency " +
        "graph without reducing real coupling.")]
internal sealed class ProjGraphTools(
    AnalysisServices analysisServices,
    IDiscoverCsFilesUseCase discoverCsFilesUseCase,
    DiagramRenderers renderers,
    IFileSystem fileSystem,
    DiagramResourceCache cache,
    McpServer server,
    WorkspaceRootService rootService,
    CollectingOutputConsole outputConsole)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
        if (options is { MaxDepth: < 0 })
        {
            // 0 is valid (render only the requested types); only a negative depth is invalid.
            throw new McpException($"maxDepth must not be negative; got {options.MaxDepth}.");
        }

        path = await PreparePathAsync(path, cancellationToken);

        if (!fileSystem.FileExists(path) && !fileSystem.DirectoryExists(path))
            throw new McpException($"Path not found: {path}");

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
            RequireCsFile(path);

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

        outputConsole.ClearWarnings();
        var graph = await analysisServices.GraphService.BuildGraphAsync(path, includePackages, cancellationToken);
        var warnings = outputConsole.DrainWarnings();

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 3,
            Total = 3,
            Message = "Rendering diagram"
        });

        var diagram = AppendWarningComments(
            renderers.GraphRenderer.Render(graph, new DiagramOptions(showTitle, false, includePackages)),
            warnings);

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
        if (topN < 1)
        {
            throw new McpException($"topN must be at least 1; got {topN}.");
        }

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

        outputConsole.ClearWarnings();
        var stats = await analysisServices.StatsService.ComputeStatsAsync(path, topN, cancellationToken);
        var warnings = outputConsole.DrainWarnings();

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 3,
            Total = 3,
            Message = "Summarizing results"
        });

        var json = SerializeStatsWithWarnings(stats, warnings);

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
        [Description("How EF Core owned types are shown: 'mirror' (default) inlines table-split owned types onto the owner as EF names them; 'classic' gives every owned type its own entity")]
        string ownedMode = "mirror",
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!ErdOwnedModeParser.TryParse(ownedMode, out var mode))
        {
            // McpException so the actionable message reaches the client; the SDK strips the message from
            // any other exception type. Unlike the CLI, an MCP caller is usually an LLM — silently
            // defaulting an unrecognized value to mirror would return plausible-but-wrong output with no
            // signal the requested mode was never applied. Validated up front, before any analysis work,
            // mirroring how maxDepth is rejected in GetClassDiagramAsync before touching the file system.
            throw new McpException($"Invalid ownedMode '{ownedMode}'. Expected 'mirror' or 'classic'.");
        }

        path = await PreparePathAsync(path, cancellationToken);

        RequireFileExists(path);
        RequireCsFile(path);

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
                    0 => throw new McpException($"No ModelSnapshot found in '{path}'."),
                    1 => snapshots[0],
                    _ => throw new McpException(
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

        var diagram = renderers.ErdRenderer.Render(model, new DiagramOptions(showTitle, false, false, mode));

        var filename = Path.GetFileName(path);
        await cache.StoreAsync("erd", path, "text/plain", diagram,
            $"Entity diagram for {filename}", server, cancellationToken);

        return diagram;
    }

    /// <summary>
    /// Appends any collected analysis warnings to a Mermaid diagram as trailing <c>%% WARNING</c>
    /// comment lines so partial/skipped analysis is visible to the client. Comments are appended
    /// after the diagram body to avoid disturbing the leading YAML front-matter.
    /// </summary>
    /// <param name="diagram">The rendered Mermaid diagram.</param>
    /// <param name="warnings">The collected warnings to append.</param>
    /// <returns>The diagram with trailing warning comments, or the original diagram when there are none.</returns>
    private static string AppendWarningComments(string diagram, IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return diagram;
        }

        var builder = new StringBuilder(diagram.TrimEnd('\r', '\n'));
        foreach (var warning in warnings)
        {
            builder.Append(CultureInfo.InvariantCulture, $"\n%% WARNING: {warning.ReplaceLineEndings(" ")}");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Serializes the stats object, attaching a top-level <c>warnings</c> array when any analysis
    /// warnings were collected. The stats fields stay at the root, so existing consumers that
    /// deserialize the stats object are unaffected.
    /// </summary>
    /// <param name="stats">The computed solution statistics.</param>
    /// <param name="warnings">The collected warnings to attach.</param>
    /// <returns>The stats JSON, with a <c>warnings</c> array when any were collected.</returns>
    private static string SerializeStatsWithWarnings(SolutionStats stats, IReadOnlyList<string> warnings)
    {
        var node = JsonSerializer.SerializeToNode(stats, JsonSerializerOptions)?.AsObject();
        if (node is null)
        {
            return JsonSerializer.Serialize(stats, JsonSerializerOptions);
        }

        if (warnings.Count > 0)
        {
            var array = new JsonArray();
            foreach (var warning in warnings)
            {
                array.Add(warning);
            }

            node["warnings"] = array;
        }

        return node.ToJsonString(JsonSerializerOptions);
    }

    private async Task<string> PreparePathAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return await rootService.TryResolveAsync(path, server, cancellationToken);
    }

    private static void RequireCsFile(string path)
    {
        try
        {
            FilePathGuard.RequireCsFile(path);
        }
        catch (ArgumentException ex)
        {
            // McpException so the actionable message reaches the client; the SDK strips the
            // message from any other exception type.
            throw new McpException(ex.Message);
        }
    }

    private void RequireFileExists(string path)
    {
        if (!fileSystem.FileExists(path))
        {
            // McpException so the actionable message reaches the client; the SDK strips the
            // message from any other exception type.
            throw new McpException($"File not found: {path}");
        }
    }

    private void RequireSolutionExtension(string path)
    {
        var extension = fileSystem.GetExtension(path).ToLowerInvariant();
        if (extension is not (".sln" or ".slnx" or ".csproj"))
        {
            throw new McpException(
                $"Unsupported file type '{extension}'. Expected .sln, .slnx, or .csproj.");
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
