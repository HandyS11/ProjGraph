# ProjGraph

![CI](https://github.com/HandyS11/ProjGraph/actions/workflows/ci.yml/badge.svg)
![CD](https://github.com/HandyS11/ProjGraph/actions/workflows/publish.yml/badge.svg)

[![ProjGraph.Cli NuGet](https://img.shields.io/nuget/v/ProjGraph.Cli?label=CLI&logo=nuget)](https://www.nuget.org/packages/ProjGraph.Cli)
[![ProjGraph.Cli Downloads](https://img.shields.io/nuget/dt/ProjGraph.Cli?label=CLI%20downloads&logo=nuget)](https://www.nuget.org/packages/ProjGraph.Cli)
[![ProjGraph.Mcp NuGet](https://img.shields.io/nuget/v/ProjGraph.Mcp?label=MCP&logo=nuget)](https://www.nuget.org/packages/ProjGraph.Mcp)
[![ProjGraph.Mcp Downloads](https://img.shields.io/nuget/dt/ProjGraph.Mcp?label=MCP%20downloads&logo=nuget)](https://www.nuget.org/packages/ProjGraph.Mcp)

**ProjGraph** is a .NET tool ecosystem for visualizing and analyzing project dependencies within solutions. It provides
both a CLI for manual analysis and an MCP server for AI-assisted exploration of your codebase architecture.

## 🚀 Quick Start

### CLI Tool

Install and use the command-line tool for immediate visualization:

```bash
# Install
dotnet tool install -g ProjGraph.Cli

# Visualize project dependencies
projgraph visualize ./MySolution.slnx

# Generate Entity Relationship Diagram
projgraph erd ./Data/MyDbContext.cs
```

📖 [Full CLI Documentation](./src/ProjGraph.Cli/README.md)

### MCP Server

Configure your MCP client (e.g., GitHub Copilot, Claude) with the following settings:

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

📖 [Full MCP Documentation](./src/ProjGraph.Mcp/README.md)

## ✨ Key Features

- **📊 Multiple Output Formats**: ASCII tree and Mermaid.js diagrams
- **🗄️ Entity Relationship Diagrams**: Generate ERDs from EF Core DbContext files
- **🔄 Circular Dependency Detection**: Automatically identifies problematic cycles
- **📁 Modern .NET Support**: Full support for `.slnx`, `.sln`, and `.csproj` files
- **🤖 AI Integration**: MCP server for GitHub Copilot, Claude, and other AI assistants
- **⚡ Fast & Reliable**: Efficient parsing and graph algorithms

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

### Run Locally

```bash
# CLI
dotnet run --project src/ProjGraph.Cli -- visualize ./ProjGraph.sln

# MCP Server
dotnet run --project src/ProjGraph.Mcp
```

## 📝 Usage Examples

### Analyze Project Dependencies

```bash
# Tree format (default)
projgraph visualize ./MySolution.sln

# Mermaid format for documentation
projgraph visualize ./MySolution.slnx --format mermaid > docs/dependencies.mmd
```

### Generate Database Diagrams

```bash
# Generate ERD from DbContext
projgraph erd ./Data/MyDbContext.cs

# Save to documentation
projgraph erd ./Data/MyDbContext.cs > docs/database-schema.md
```

### With AI Assistants

Once the MCP server is configured:

```x
You: "Analyze the dependencies in my solution"
AI: [Uses ProjGraph MCP to analyze and explain your architecture]

You: "Show me the entity relationships in my DbContext"
AI: [Generates and explains the database schema]
```

## 🔗 Links

- **CLI Package**: [ProjGraph.Cli on NuGet](https://www.nuget.org/packages/ProjGraph.Cli)
- **MCP Package**: [ProjGraph.Mcp on NuGet](https://www.nuget.org/packages/ProjGraph.Mcp)
