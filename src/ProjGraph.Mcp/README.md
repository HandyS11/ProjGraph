# ProjGraph MCP Server

**ProjGraph.Mcp** is a [Model Context Protocol](https://modelcontextprotocol.io/) (MCP) server that enables AI
assistants like GitHub Copilot and Claude to programmatically analyze and understand .NET solution architectures.

## Requirements

- .NET 10.0 or later runtime (included in self-contained deployment)

## Available Tools

### `get_project_graph`

Analyzes a solution or project file and returns the complete dependency graph.

**Parameters:**

- `solutionPath` (string): Path to the `.sln`, `.slnx`, or `.csproj` file

**Returns:**

- Project nodes with metadata (name, path, target framework)
- Dependency edges between projects
- Circular dependency detection results

**Example Usage (via AI Assistant):**

```x
"Analyze the dependencies in ./MySolution.slnx"
"Show me the project structure of MyProject.csproj"
"Are there any circular dependencies in this solution?"
```

## License

Licensed under the terms specified in the [repository](https://github.com/HandyS11/ProjGraph).
