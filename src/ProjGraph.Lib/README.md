# ProjGraph.Lib

Meta-package that bundles all ProjGraph libraries with a single dependency and a single DI registration call.

[![NuGet](https://img.shields.io/nuget/v/ProjGraph.Lib)](https://www.nuget.org/packages/ProjGraph.Lib)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Installation

```bash
dotnet add package ProjGraph.Lib
```

## Overview

`ProjGraph.Lib` is the recommended entry point for embedding ProjGraph capabilities in your own application.
It aggregates all three analysis libraries under a single package reference and exposes a single extension method
for dependency injection setup.

### Included packages

| Package                                                                                         | Description                                                             |
|-------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------|
| [`ProjGraph.Lib.Core`](https://www.nuget.org/packages/ProjGraph.Lib.Core)                       | Solution/project parsers, Roslyn compilation factory, core abstractions |
| [`ProjGraph.Lib.Dependencies`](https://www.nuget.org/packages/ProjGraph.Lib.Dependencies)       | Dependency graph analysis and Mermaid/tree rendering                    |
| [`ProjGraph.Lib.ClassDiagram`](https://www.nuget.org/packages/ProjGraph.Lib.ClassDiagram)       | Roslyn class hierarchy analysis and Mermaid class diagrams              |
| [`ProjGraph.Lib.EntityFramework`](https://www.nuget.org/packages/ProjGraph.Lib.EntityFramework) | EF Core DbContext/snapshot analysis and Mermaid ERDs                    |

## Usage

### Register all services

```csharp
using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Lib;

var services = new ServiceCollection();
services.AddProjGraphLib();
var provider = services.BuildServiceProvider();
```

### Project dependency graph

```csharp
using ProjGraph.Lib.Dependencies.Application;
using ProjGraph.Lib.Dependencies.Rendering;

var graphService = provider.GetRequiredService<IGraphService>();
var graph = await graphService.BuildGraphAsync("MySolution.slnx", includePackages: false);

var renderer = provider.GetRequiredService<MermaidGraphRenderer>();
Console.WriteLine(renderer.Render(graph));
```

### Class diagram

```csharp
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;
using ProjGraph.Lib.Core.Abstractions;

var classAnalysis = provider.GetRequiredService<IClassAnalysisService>();
var result = await classAnalysis.AnalyzeDirectoryAsync("src/MyProject");

var renderer = provider.GetRequiredService<IDiagramRenderer<ClassModel>>();
Console.WriteLine(renderer.Render(result));
```

### Entity Relationship Diagram

```csharp
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Application;

var efAnalysis = provider.GetRequiredService<IEfAnalysisService>();
var model = await efAnalysis.AnalyzeContextAsync("src/Data/AppDbContext.cs");

var renderer = provider.GetRequiredService<IDiagramRenderer<EfModel>>();
Console.WriteLine(renderer.Render(model));
```

## Related packages

- [`ProjGraph.Cli`](https://www.nuget.org/packages/ProjGraph.Cli) — `dotnet tool` CLI wrapping this library
- [`ProjGraph.Mcp`](https://www.nuget.org/packages/ProjGraph.Mcp) — MCP server for AI assistant integration

## Links

- [GitHub Repository](https://github.com/HandyS11/ProjGraph)
- [Documentation](https://handys11.github.io/ProjGraph)
