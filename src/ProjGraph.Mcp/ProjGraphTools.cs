using ModelContextProtocol;
using ModelContextProtocol.Server;
using ProjGraph.Core.Exceptions;
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
        var warnings = new List<string>();

        if (fileSystem.DirectoryExists(path))
        {
            var files = discoverCsFilesUseCase.Execute(path);
            if (files.Count > 50)
            {
                warnings.Add($"Scanning {files.Count} files. Large diagrams may be hard to read.");
            }

            progress?.Report(new ProgressNotificationValue
            {
                Progress = 2,
                Total = 3,
                Message = "Analyzing types and members"
            });

            model = await RunAnalysisAsync(() => analysisServices.ClassService.AnalyzeDirectoryAsync(path, options));
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

            model = await RunAnalysisAsync(() => analysisServices.ClassService.AnalyzeFileAsync(path, options));
        }

        progress?.Report(new ProgressNotificationValue
        {
            Progress = 3,
            Total = 3,
            Message = "Rendering class diagram"
        });

        // Appended, not prepended: a comment ahead of the YAML front-matter breaks strict
        // Mermaid parsers (same placement rule as get_project_graph's warnings).
        var result = AppendWarningComments(
            renderers.ClassRenderer.Render(model, new DiagramOptions(showTitle, false)),
            warnings);

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
        var graph = await RunAnalysisAsync(() =>
            analysisServices.GraphService.BuildGraphAsync(path, includePackages, cancellationToken));
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
        var stats = await RunAnalysisAsync(() =>
            analysisServices.StatsService.ComputeStatsAsync(path, topN, cancellationToken));
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
            var snapshots = await RunAnalysisAsync(() => analysisServices.EfService.DiscoverSnapshotsAsync(path));
            var snapshotName = ResolveCandidateName(snapshots, contextName, "ModelSnapshot", path);

            progress?.Report(new ProgressNotificationValue
            {
                Progress = 2,
                Total = 3,
                Message = "Analyzing entities and relationships"
            });

            model = await RunAnalysisAsync(() => analysisServices.EfService.AnalyzeSnapshotAsync(path, snapshotName));
        }
        else
        {
            // Mirror the snapshot branch: with several DbContexts in the file, silently analyzing
            // the first (the old FindContextClass FirstOrDefault behavior) hands an MCP caller
            // plausible-but-wrong output with no signal that the others were never considered.
            var contexts = await RunAnalysisAsync(() => analysisServices.EfService.DiscoverContextsAsync(path));
            var resolvedName = ResolveCandidateName(contexts, contextName, "DbContext", path);

            progress?.Report(new ProgressNotificationValue
            {
                Progress = 2,
                Total = 3,
                Message = "Analyzing entities and relationships"
            });

            model = await RunAnalysisAsync(() => analysisServices.EfService.AnalyzeContextAsync(path, resolvedName));
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

    /// <summary>
    /// Resolves which discovered DbContext/ModelSnapshot class to analyze, failing with an
    /// actionable <see cref="McpException"/> instead of silently picking a wrong or missing one:
    /// a requested name is validated against the discovered candidates (a typo previously fell
    /// through to analysis and surfaced as a stripped generic error), and with several candidates
    /// and no requested name the caller is asked to choose rather than being handed the first.
    /// </summary>
    /// <param name="candidates">The class names discovered in the file.</param>
    /// <param name="requestedName">The caller-supplied class name, if any.</param>
    /// <param name="kind">The kind of class being resolved ("DbContext" or "ModelSnapshot"), for messages.</param>
    /// <param name="path">The analyzed file path, for messages.</param>
    /// <returns>The single resolved class name.</returns>
    /// <exception cref="McpException">Thrown when the requested name is unknown, none exist, or the choice is ambiguous.</exception>
    private static string ResolveCandidateName(
        List<string> candidates, string? requestedName, string kind, string path)
    {
        if (candidates.Count == 0)
        {
            throw new McpException($"No {kind} found in '{path}'.");
        }

        if (!string.IsNullOrEmpty(requestedName))
        {
            return candidates.Contains(requestedName)
                ? requestedName
                : throw new McpException(
                    $"{kind} '{requestedName}' not found in '{path}'. Available: {string.Join(", ", candidates)}.");
        }

        return candidates.Count == 1
            ? candidates[0]
            : throw new McpException(
                $"Multiple {kind}s found in '{path}': {string.Join(", ", candidates)}. Specify one using the contextName parameter.");
    }

    /// <summary>
    /// Runs a library analysis call, converting any <see cref="ProjGraphException"/> (the base of
    /// <c>AnalysisException</c>/<c>ParsingException</c>) into an <see cref="McpException"/> carrying
    /// the same message. The MCP SDK replaces the message of every other exception type with a
    /// generic "An error occurred invoking '…'", so without this wrap the library's actionable
    /// guidance (e.g. "DbContext not found in file") never reaches the client. Unexpected BCL
    /// exceptions still propagate unwrapped: they carry no user guidance worth preserving, and
    /// wrapping them would dress genuine bugs up as clean protocol errors.
    /// </summary>
    /// <typeparam name="T">The analysis result type.</typeparam>
    /// <param name="analysis">The analysis call to run.</param>
    /// <returns>The analysis result.</returns>
    /// <exception cref="McpException">Thrown when the analysis fails with a <see cref="ProjGraphException"/>.</exception>
    private static async Task<T> RunAnalysisAsync<T>(Func<Task<T>> analysis)
    {
        try
        {
            return await analysis();
        }
        catch (ProjGraphException ex)
        {
            throw new McpException(ex.Message);
        }
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
