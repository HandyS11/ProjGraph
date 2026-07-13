# ProjGraph MCP Server

[Model Context Protocol](https://modelcontextprotocol.io/) (MCP) server that enables AI assistants to analyze .NET
solution architectures, generate Entity Relationship Diagrams, and visualize class hierarchies.

[![NuGet](https://img.shields.io/nuget/v/ProjGraph.Mcp)](https://www.nuget.org/packages/ProjGraph.Mcp)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Setup

To use ProjGraph with an MCP-compatible client like Claude or Cursor, add the following configuration to your client's
MCP settings file:

> Find the latest version number on [NuGet](https://www.nuget.org/packages/ProjGraph.Mcp)

```json
{
  "servers": {
    "ProjGraph.Mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": ["ProjGraph.Mcp@x.x.x", "--yes"]
    }
  }
}
```

Or run from source:

```json
{
  "mcpServers": {
    "projgraph": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/src/ProjGraph.Mcp/ProjGraph.Mcp.csproj"
      ]
    }
  }
}
```

## Capabilities

### Tools

| Tool                | Description                                             | Primary Parameters                     |
|---------------------|---------------------------------------------------------|----------------------------------------|
| `get_project_graph` | Dependency graph as a Mermaid diagram                   | `path`, `showTitle`, `includePackages` |
| `get_erd`           | Mermaid ERD from EF Core DbContext or ModelSnapshot     | `path`, `contextName`, `showTitle`     |
| `get_class_diagram` | Mermaid class diagram for C# files or directories       | `path`, `options`, `showTitle`         |
| `get_project_stats` | Architectural metrics (counts, depth, hotspots, cycles) | `path`, `topN`                         |

### Prompts

Pre-built analysis workflows that guide the LLM through multi-step investigations:

| Prompt                   | Description                                          | Arguments                        |
|--------------------------|------------------------------------------------------|----------------------------------|
| `architecture_review`    | Comprehensive architecture review of a .NET solution | `path`                           |
| `dependency_analysis`    | Hotspot and cycle detection for a solution           | `path`, `topN` (optional)        |
| `database_schema_review` | EF Core schema design review                         | `path`, `contextName` (optional) |
| `class_structure_review` | Class hierarchy and design pattern review            | `path`                           |

### Resources

- **`projgraph://welcome`** — Always-available overview of all server capabilities
- **`projgraph://diagrams/{type}/{path}`** — Cached diagram outputs from tool calls (up to 50 entries, LRU eviction)

Diagram resources are automatically created when tools generate output. Clients receive
`notifications/resources/list_changed` when resources are added or evicted.

### Roots

The server resolves relative file paths against workspace roots declared by the client. When a client declares roots via
`roots/list`, passing `"MyApp.slnx"` instead of `"D:/Projects/MyApp/MyApp.slnx"` just works.

### Progress Notifications

Every tool emits 3-stage `notifications/progress` reports during execution (e.g., _Parsing → Analyzing → Rendering_).
Clients with progress UI display live feedback; clients without progress support are unaffected.

## Tools Reference

### `get_project_graph`

Analyzes a solution or project file and returns the dependency graph as a Mermaid diagram.

**Parameters:**

- `path` (string): Absolute path to `.sln`, `.slnx`, or `.csproj` file
- `showTitle` (boolean, optional): Include graph title (default: true)
- `includePackages` (boolean, optional): Include NuGet packages in the graph (default: false)

**Example prompts:**

```text
"Analyze the dependencies in ./MySolution.slnx"
"Show me the project structure"
"Are there any circular dependencies?"
```

### `get_erd`

Generates a Mermaid Entity Relationship Diagram from an EF Core `DbContext` or `ModelSnapshot` file.

**Parameters:**

- `path` (string): Absolute path to a `.cs` file containing a `DbContext` or `ModelSnapshot`
- `contextName` (string, optional): Specific class name if multiple exist in the file
- `showTitle` (boolean, optional): Include diagram title (default: true)

**Features:**

- Detects entities, properties (including types), and relationships from source or migration snapshots
- Shows primary keys, foreign keys, and constraints
- Supports inheritance, base classes, data annotations, and Fluent API configurations
- Handles many-to-many with join tables

**Example prompts:**

```text
"Show me the database schema from ./Data/MyDbContext.cs"
"Generate an ERD from the migration snapshot in ./Migrations/MyDbContextModelSnapshot.cs"
"What are the entity relationships in my database?"
```

### `get_class_diagram`

Generates a Mermaid class diagram for the types defined in a specific C# file or directory, with options to discover
inheritance and related types in the workspace.

**Parameters:**

- `path` (string): Absolute path to the C# file or directory to analyze
- `options` (object, optional): Analysis and discovery options:
  - `includeInheritance` (boolean): Search for base classes and interfaces (default: false)
  - `includeDependencies` (boolean): Include classes used as properties or fields (default: false)
  - `includeProperties` (boolean): Display properties and fields (default: true)
  - `includeFunctions` (boolean): Display methods (default: true)
  - `maxDepth` (number): Levels of relationships to follow (default: 1)
- `showTitle` (boolean, optional): Include diagram title (default: true)

**Example prompts:**

```text
"Show me the class diagram for the Models folder"
"Analyze the class hierarchy in ./Services/OrderService.cs"
"Generate a combined diagram for all classes in the Domain project"
```

### `get_project_stats`

Returns key architectural metrics as structured JSON, useful for reasoning about solution health without a diagram.

**Parameters:**

- `path` (string): Absolute path to `.sln`, `.slnx`, or `.csproj` file
- `topN` (number, optional): Top most-referenced projects to include (default: 5)

**Returns:** Project count, type breakdown (library/executable/test), dependency depth stats, cycle detection, and
hotspot ranking.

**Example prompts:**

```text
"How many projects are in my solution and what types are they?"
"Which projects are referenced the most in ./MySolution.slnx?"
"Is there a circular dependency in my solution?"
```

## License

Licensed under the terms specified in the [repository](https://github.com/HandyS11/ProjGraph).

<!-- mcp-name: io.github.HandyS11/projgraph -->
