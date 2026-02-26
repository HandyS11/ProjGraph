# ProjGraph MCP Server

[Model Context Protocol](https://modelcontextprotocol.io/) (MCP) server that enables AI assistants to analyze .NET
solution architectures, generate Entity Relationship Diagrams, and visualize class hierarchies.

## Setup

To use ProjGraph with an MCP-compatible client like Claude or Cursor, add the following configuration to your client's
MCP settings file:

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

## Available Tools

| Tool Name           | Description                                             | Primary Parameters |
|---------------------|---------------------------------------------------------|--------------------|
| `get_project_graph` | Analyzes .NET solution/project dependency graph.        | `path`             |
| `get_erd`           | Generates Mermaid ERD from EF Core source or snapshots. | `path`             |
| `get_class_diagram` | Generates Mermaid class diagram for C# files.           | `path`             |
| `get_project_stats` | Returns key architectural metrics for a solution.       | `path`             |

### `get_project_graph`

Analyzes a solution or project file and returns the dependency graph as a Mermaid diagram.

**Parameters:**

- `path` (string): Absolute path to `.sln`, `.slnx`, or `.csproj` file
- `show_title` (boolean, optional): Whether to include the title in the diagram (default: true)

**Returns:** Mermaid graph diagram code

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
- `context_name` (string, optional): Specific DbContext or ModelSnapshot class name if multiple exist in the file
- `show_title` (boolean, optional): Whether to include the title in the diagram (default: true)

**Returns:** Mermaid ERD diagram code

**Features:**

- Detects entities, properties (including types), and relationships from source or migration snapshots
- Shows primary keys, foreign keys, and constraints
- Supports inheritance and base classes
- Extracts `MaxLength`, `Required`, and other data annotations
- Detects Fluent API configurations and shadow relationships
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
- `include_inheritance` (boolean, optional): Whether to search the workspace for base classes and interfaces (default:
  false)
- `include_dependencies` (boolean, optional): Whether to search for and include other classes used as properties or
  fields (default: false)
- `include_properties` (boolean, optional): Whether to display properties and fields in the class diagram (default:
  true)
- `include_functions` (boolean, optional): Whether to display functions/methods in the class diagram (default: true)
- `depth` (number, optional): How many levels of relationships to follow (default: 1)
- `show_title` (boolean, optional): Whether to include the title in the diagram (default: true)

**Returns:** Mermaid class diagram code

**Example prompts:**

```text
"Show me the class diagram for the Models folder"
"Analyze the class hierarchy in ./Services/OrderService.cs"
"Generate a combined diagram for all classes in the Domain project"
```

**Example prompts:**

```text
"Show me the class hierarchy for ./Models/Admin.cs"
"Visualize the dependencies of the GuestUser class in Guest.cs"
"Draw a class diagram for my domain model starting at ./Domain/Entity.cs with inheritance"
"Generate a class diagram with all dependencies at depth 2"
```

### `get_project_stats`

Analyses a .NET solution or project file and returns key architectural metrics as structured data, useful for
reasoning about solution health and architecture without generating a diagram.

**Parameters:**

- `path` (string): Absolute path to `.sln`, `.slnx`, or `.csproj` file
- `top_n` (number, optional): Number of top most-referenced (hotspot) projects to include (default: 5)

**Returns:** Structured data containing:

- Total project count
- Breakdown by output type (library, executable, test, other)
- Dependency depth statistics (average, min, max)
- Whether circular dependencies exist
- Ranked list of most-referenced (hotspot) projects with their in-degree counts

**Example prompts:**

```text
"How many projects are in my solution and what types are they?"
"Which projects are referenced the most in ./MySolution.slnx?"
"Is there a circular dependency in my solution?"
"Give me a health summary of ./src/MyApp/MyApp.sln"
```

## License

Licensed under the terms specified in the [repository](https://github.com/HandyS11/ProjGraph).

<!-- mcp-name: io.github.HandyS11/projgraph -->
