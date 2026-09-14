using Microsoft.Extensions.AI;
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;
using System.Text.Json.Serialization;

namespace ProjGraph.Mcp;

/// <summary>
/// Source-generated JSON metadata for the types the MCP server serializes itself or exposes through
/// tool parameters and prompt results. Native AOT has no reflection-based serializer, so every such
/// type must be listed here.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AnalysisOptions))]
[JsonSerializable(typeof(SolutionStats))]
[JsonSerializable(typeof(IEnumerable<ChatMessage>))]
internal sealed partial class McpJsonContext : JsonSerializerContext;
