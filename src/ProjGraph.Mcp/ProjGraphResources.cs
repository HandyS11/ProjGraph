using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ProjGraph.Mcp;

[McpServerResourceType]
internal sealed class ProjGraphResources(DiagramResourceCache cache)
{
    private const string WelcomeContent =
        """
        Welcome to ProjGraph MCP Server

        ProjGraph analyzes .NET solution architectures and generates Mermaid diagrams and metrics.

        Available Tools:
        ─────────────────────────────────────────────────────────────────────────────
        • get_project_graph  — Dependency graph for a .sln, .slnx, or .csproj file
        • get_class_diagram  — Class diagram for a .cs file or directory
        • get_erd            — Entity Relationship Diagram from an EF Core DbContext or ModelSnapshot
        • get_project_stats  — Architectural metrics (dependency depth, hotspots, cycles)

        Available Prompts (guided workflows):
        ─────────────────────────────────────────────────────────────────────────────
        • architecture_review    — Comprehensive architecture review of a .NET solution
        • dependency_analysis    — Hotspot and cycle analysis for a solution
        • database_schema_review — EF Core schema design review
        • class_structure_review — Class hierarchy and design pattern review

        Generated Diagrams (session cache):
        ─────────────────────────────────────────────────────────────────────────────
        Diagrams generated during this session are cached as resources under
        projgraph://diagrams/{type}/{encodedPath} and appear in listResources.
        """;

    [McpServerResource(Name = "projgraph-welcome", MimeType = "text/plain",
        UriTemplate = "projgraph://welcome")]
    public static string GetWelcome()
    {
        return WelcomeContent;
    }

    [McpServerResource(Name = "projgraph-diagram", MimeType = "text/plain",
        UriTemplate = "projgraph://diagrams/{type}/{path}")]
    public ReadResourceResult ReadDiagram(string type, string path)
    {
        var decodedPath = Uri.UnescapeDataString(path);
        var uri = $"projgraph://diagrams/{type}/{Uri.EscapeDataString(decodedPath)}";

        var content = cache.TryRead(uri)
                      ?? throw new McpException($"Resource not found: {uri}");

        var resource = cache.ListResources().FirstOrDefault(r => r.Uri == uri);
        var mimeType = resource?.MimeType ?? "text/plain";

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = uri,
                    MimeType = mimeType,
                    Text = content
                }
            ]
        };
    }
}
