# ProjGraph

<!-- mcp-name: io.github.handys11/projgraph -->

**ProjGraph** is a .NET tool ecosystem for visualizing and analyzing project dependencies within solutions. It provides
both a CLI for manual analysis and an MCP server for AI-assisted exploration of your codebase architecture.

## 🚀 Quick Start

### CLI Tool

Install and use the command-line tool for immediate visualization:

```bash
# Install
dotnet tool install -g ProjGraph.Cli

# Visualize
projgraph visualize ./MySolution.slnx
```

📖 [Full CLI Documentation](./src/ProjGraph.Cli/README.md)

### MCP Server

Enable AI assistants to understand your solution structure:

```bash
# Install
dotnet tool install -g ProjGraph.Mcp

# Configure in mcp.json
{
  "servers": {
    "projgraph": {
      "command": "projgraph-mcp",
      "args": []
    }
  }
}
```

📖 [Full MCP Documentation](./src/ProjGraph.Mcp/README.md)

## ✨ Key Features

- **📊 Multiple Output Formats**: ASCII tree and Mermaid.js diagrams
- **🔄 Circular Dependency Detection**: Automatically identifies problematic cycles
- **📁 Modern .NET Support**: Full support for `.slnx`, `.sln`, and `.csproj` files
- **🤖 AI Integration**: MCP server for GitHub Copilot, Claude, and other AI assistants
- **⚡ Fast & Reliable**: Efficient parsing and graph algorithms
- **🏗️ Clean Architecture**: Modular design with high testability

## 📦 Project Structure

```x
ProjGraph/
├── src/
│   ├── ProjGraph.Core/      # Domain models (Project, Dependency, SolutionGraph)
│   ├── ProjGraph.Lib/       # Business logic (parsers, algorithms, graph service)
│   ├── ProjGraph.Cli/       # Command-line interface tool
│   └── ProjGraph.Mcp/       # Model Context Protocol server
└── tests/
    ├── ProjGraph.Tests.Unit/        # Unit tests
    ├── ProjGraph.Tests.Integration/ # Integration tests
    └── ProjGraph.Tests.Contract/    # Contract tests for MCP
```

### Layer Responsibilities

- **Core**: Domain entities and value objects (no dependencies)
- **Lib**: Parsing logic, graph algorithms, and service orchestration
- **Cli**: User-facing command-line interface using Spectre.Console
- **Mcp**: Model Context Protocol server for AI integration

## 🛠️ Development

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later

### Build

```bash
# Restore dependencies and build all projects
dotnet build

# Build specific configuration
dotnet build --configuration Release
```

### Test

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/ProjGraph.Tests.Unit
```

### Run Locally

```bash
# CLI
dotnet run --project src/ProjGraph.Cli -- visualize ./ProjGraph.sln

# MCP Server
dotnet run --project src/ProjGraph.Mcp
```

## 📝 Usage Examples

### Analyze a Solution

```bash
# Tree format (default)
projgraph visualize ./MySolution.sln

# Mermaid format for documentation
projgraph visualize ./MySolution.slnx --format mermaid > docs/dependencies.mmd
```

### With AI Assistants

Once the MCP server is configured:

```x
You: "Analyze the dependencies in my solution"
AI: [Uses ProjGraph MCP to analyze and explain your architecture]

You: "Are there any circular dependencies?"
AI: [Detects and explains any circular references]
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

### Development Guidelines

1. Follow the existing code style (see `.editorconfig`)
2. Add tests for new features
3. Update documentation as needed
4. Ensure all tests pass before submitting

## 📄 License

Distributed under the MIT License. See [LICENSE](./LICENSE) for more information.

## 🔗 Links

- **CLI Package**: [ProjGraph.Cli on NuGet](https://www.nuget.org/packages/ProjGraph.Cli)
- **MCP Package**: [ProjGraph.Mcp on NuGet](https://www.nuget.org/packages/ProjGraph.Mcp)
- **Issues**: [GitHub Issues](https://github.com/HandyS11/ProjGraph/issues)
- **MCP Specification**: [Model Context Protocol](https://modelcontextprotocol.io/)

## 🙏 Acknowledgments

- Built with [Spectre.Console](https://spectreconsole.net/) for beautiful CLI output
- Uses [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) for AI integration
- Implements Tarjan's algorithm for circular dependency detection
