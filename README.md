# ProjGraph

**ProjGraph** is a .NET tool designed to help developers visualize project dependencies within solutions. It parses `.sln`, `.slnx`, and `.csproj` files to generate clear, manageable dependency graphs directly in your terminal or via the Model Context Protocol (MCP).

## Key Features

- **CLI Visualization**: Render project dependencies as an ASCII tree or Mermaid.js format in the terminal.
- **Modern .NET Support**: Full support for the modern `.slnx` solution format alongside traditional `.sln` and `.csproj` files.
- **MCP Server**: Built-in MCP server to allow AI agents (like GitHub Copilot or Claude) to programmatically query and understand your solution's architecture.
- **Clean Architecture**: Built with a modular core, ensuring high code quality and testability.

## Project Structure

- `src/ProjGraph.Core`: The engine of the project. Contains the domain models and logic for parsing and graph building.
- `src/ProjGraph.Cli`: Command-line interface for manual visualization and exporting.
- `src/ProjGraph.Mcp`: An implementation of the Model Context Protocol for automated tool access.

## Quickstart

### Installation

Install the CLI tool globally using `dotnet`:

```bash
dotnet tool install -g ProjGraph.Cli
```

### CLI Usage

Visualize a solution or project:

```bash
projgraph visualize ./MySolution.slnx
```

Export to Mermaid format:

```bash
projgraph visualize ./MyProject.csproj --format mermaid
```

### AI Integration (MCP)

Add ProjGraph to your AI assistant's `mcpConfig.json`:

```json
{
  "mcpServers": {
    "projgraph": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/ProjGraph.Mcp/ProjGraph.Mcp.csproj"]
    }
  }
}
```

## Development

### Prerequisites

- .NET 8.0 SDK or later.

### Build & Test

Build the entire solution:

```bash
dotnet build
```

Run all tests:

```bash
dotnet test
```

## License

Distributed under the MIT License. See `LICENSE` for more information.
