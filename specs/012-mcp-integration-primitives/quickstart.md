# Quickstart: MCP Integration Primitives

**For**: LLM clients, MCP host developers, and contributors
**Date**: 2026-03-10

---

## What's New

This release adds three new MCP primitives to the ProjGraph MCP server:

| Primitive | What it enables                                                                                    |
|-----------|----------------------------------------------------------------------------------------------------|
| **Prompts** | Pre-built analysis workflows — select a prompt, provide a path, get a guided multi-step analysis   |
| **Resources** | Cached diagram outputs as addressable URIs — reference generated diagrams without re-running tools |
| **Roots** | Workspace-aware path resolution — provide relative paths instead of absolute paths                 |

Progress notifications (stage-by-stage feedback) are also emitted during tool execution.

---

## Using Prompts

### Listing Available Prompts

```json
// MCP request
{ "method": "prompts/list" }

// Response (excerpt)
{
  "prompts": [
    {
      "name": "architecture_review",
      "description": "Guides an LLM through a comprehensive architecture review of a .NET solution...",
      "arguments": [{ "name": "path", "required": true, "description": "Path to .sln, .slnx, or .csproj" }]
    },
    { "name": "dependency_analysis", ... },
    { "name": "database_schema_review", ... },
    { "name": "class_structure_review", ... }
  ]
}
```

### Using a Prompt

```json
// MCP request
{
  "method": "prompts/get",
  "params": {
    "name": "architecture_review",
    "arguments": { "path": "D:/Projects/MyApp/MyApp.slnx" }
  }
}

// Response: a structured message sequence the LLM follows to perform the analysis
{
  "messages": [
    { "role": "user",      "content": { "type": "text", "text": "Please perform a comprehensive architecture review..." } },
    { "role": "assistant", "content": { "type": "text", "text": "I'll analyze the .NET solution architecture step by step..." } }
  ]
}
```

### Prompt Reference

| Prompt | Required Args              | Optional Args |
|--------|----------------------------|---------------|
| `architecture_review` | `path` (solution/project)  | — |
| `dependency_analysis` | `path` (solution/project)  | `topN` (default: 5) |
| `database_schema_review` | `path` (DbContext/.cs file) | `contextName` |
| `class_structure_review` | `path` (file or directory) | — |

---

## Using Resources

### Listing Resources

```json
// MCP request
{ "method": "resources/list" }

// Response (after one tool call)
{
  "resources": [
    {
      "uri": "projgraph://welcome",
      "name": "projgraph-welcome",
      "description": "ProjGraph capabilities overview",
      "mimeType": "text/plain"
    },
    {
      "uri": "projgraph://diagrams/graph/D%3A%5CProjects%5CMyApp.slnx",
      "name": "graph — MyApp.slnx",
      "description": "Generated 2026-03-10T14:30:00Z from D:\\Projects\\MyApp.slnx",
      "mimeType": "text/plain"
    }
  ]
}
```

### Reading a Resource

```json
// MCP request
{
  "method": "resources/read",
  "params": { "uri": "projgraph://diagrams/graph/D%3A%5CProjects%5CMyApp.slnx" }
}

// Response: full Mermaid diagram
{
  "contents": [{
    "uri": "projgraph://diagrams/graph/D%3A%5CProjects%5CMyApp.slnx",
    "mimeType": "text/plain",
    "text": "graph LR\n  ProjGraph.Core --> ProjGraph.Lib\n  ..."
  }]
}
```

### Welcome Resource

The `projgraph://welcome` resource is always available and describes all capabilities:

```json
{ "method": "resources/read", "params": { "uri": "projgraph://welcome" } }
```

### Resource Templates

```json
{ "method": "resources/templates/list" }
// Returns the diagram template for constructing custom URIs:
// "projgraph://diagrams/{type}/{path}"
// where type = graph | class | erd | stats
// and path = URL-encoded absolute file path
```

### Resource Update Notifications

When a diagram is generated, the server automatically notifies clients:

- **`notifications/resources/list_changed`** — when a new resource is added or evicted (LRU)
- **`notifications/resources/updated`** — when an existing resource's content is refreshed

---

## Using Roots (Relative Paths)

### Setup: Declare Roots in Your MCP Client

Configure your MCP client to declare workspace roots in its `initialize` request:

```json
{
  "capabilities": {
    "roots": { "listChanged": true }
  }
}
```

And respond to `roots/list` requests with your workspace directories:

```json
{
  "roots": [
    { "uri": "file:///D:/Projects/MyApp", "name": "MyApp" }
  ]
}
```

### Calling Tools with Relative Paths

Once roots are configured, you can use relative file names instead of absolute paths:

```json
// Instead of:
{ "path": "D:/Projects/MyApp/MyApp.slnx" }

// You can use:
{ "path": "MyApp.slnx" }
```

The server resolves `MyApp.slnx` → `D:\Projects\MyApp\MyApp.slnx` by searching under each declared root.

### Error Cases

| Situation | Error Message                                                                                      |
|-----------|----------------------------------------------------------------------------------------------------|
| File not found under any root | `"File 'MyApp.slnx' not found under any workspace root"`                                           |
| Matches in multiple roots | `"'MyApp.slnx' matches multiple roots: [D:\Projects\A, D:\Projects\B]. Provide an absolute path."` |
| Client does not support roots | `"Client does not support workspace roots. Please provide an absolute path."`                      |

---

## Progress Notifications

During tool execution, the server emits `notifications/progress` notifications at key stages. These are automatically consumed by MCP clients that support progress tracking (e.g., displayed as loading indicators).

Clients that do not support progress tokens receive no notification — tool execution is unaffected.

### Example Progress Sequence for `get_project_graph`

```sh
Progress 0/3: "Parsing solution file"
Progress 1/3: "Building dependency graph"
Progress 2/3: "Rendering diagram"
[tool result returned]
```

---

## Backward Compatibility

All existing tools (`get_project_graph`, `get_class_diagram`, `get_erd`, `get_project_stats`) continue to work exactly as before:

- Tool signatures are unchanged.
- Absolute paths are still accepted and preferred.
- Clients that don't use prompts, resources, or roots experience no behavior change.
