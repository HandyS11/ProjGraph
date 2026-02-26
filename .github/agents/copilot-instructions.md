# ProjGraph Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-01-13

## Active Technologies

- .NET 10.0 (C# 14+) + `mcp-publisher` (npm), GitHub Actions (011-mcp-registry-submission)

- .NET 10.0 (C# 14+) + `Spectre.Console.Cli` (CLI formatting), `ModelContextProtocol.Server` (MCP tool),
  `System.Text.Json` (MCP response serialisation), `Microsoft.Extensions.DependencyInjection` (DI wiring) (
  010-project-stats-mcp)
- N/A — pure in-memory computation over `SolutionGraph` (010-project-stats-mcp)

- N/A (Memory analysis) (009-directory-class-diagram)

- .NET 10.0 (C# 14+) + Microsoft.Build.Construction (for project parsing), Spectre.Console (for rendering) (
  008-nuget-package-rendering)
- N/A (In-memory graph processing) (008-nuget-package-rendering)

- .NET 10.0 (C# 14+) for API metadata extraction + DocFX (v2.70+), Mermaid.js (v10+), GitHub Actions (
  006-docfx-documentation)
- N/A (Static site) (006-docfx-documentation)

- .NET 10.0 (C# 14+) + ProjGraph.Cli (local build), .NET SDK 10.0 (005-improve-samples)
- Local file system (Markdown, C# Source, .slnx Solution Explorers) (005-improve-samples)

- .NET 10.0 (C# 14+) + Microsoft.CodeAnalysis.CSharp (Roslyn), ProjGraph.Lib.Core (004-config-class-members)

- .NET 10.0 (C# 14+) + `Microsoft.CodeAnalysis.CSharp`, `Spectre.Console` (003-mermaid-class-diagram)

- .NET 10.0 (C# 14+) + `Microsoft.CodeAnalysis.CSharp`, `Microsoft.CodeAnalysis.Workspaces.MSBuild`,
  `Microsoft.EntityFrameworkCore` (for symbol analysis) (002-dbcontext-erd)

- .NET 10.0 (C# 14+) + Buildalyzer (Parsing), Spectre.Console (CLI), ModelContextProtocol (MCP) (001-cli-graph-rendering)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for .NET 10.0 (C# 14+)

## Code Style

.NET 10.0 (C# 14+): Follow standard conventions

## Recent Changes

- 011-mcp-registry-submission: Added .NET 10.0 (C# 14+) + `mcp-publisher` (npm), GitHub Actions

- 010-project-stats-mcp: Added .NET 10.0 (C# 14+) + `Spectre.Console.Cli` (CLI formatting),
  `ModelContextProtocol.Server` (MCP tool), `System.Text.Json` (MCP response serialisation),
  `Microsoft.Extensions.DependencyInjection` (DI wiring)

- 009-directory-class-diagram: Added .NET 10.0 (C# 14+) + Microsoft.CodeAnalysis.CSharp (Roslyn)

  Spectre.Console (for rendering)

  GitHub Actions




  `Microsoft.CodeAnalysis.Workspaces.MSBuild`, `Microsoft.EntityFrameworkCore` (for symbol analysis)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
