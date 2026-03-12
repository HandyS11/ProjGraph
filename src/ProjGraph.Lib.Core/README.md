# ProjGraph.Lib.Core

Core abstractions, parsers, and infrastructure for analyzing .NET solutions, projects, and source files.

[![NuGet](https://img.shields.io/nuget/v/ProjGraph.Lib.Core)](https://www.nuget.org/packages/ProjGraph.Lib.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Installation

```bash
dotnet add package ProjGraph.Lib.Core
```

## Overview

`ProjGraph.Lib.Core` is the shared infrastructure layer for the ProjGraph library ecosystem. It provides:

- **Solution & project parsers** for `.sln`, `.slnx`, and `.csproj` files via MSBuild
- **Dependency injection** extension methods for service registration
- **Abstractions** (`IProjectParser`, `ISolutionParser`, `IDiagramRenderer`, `IFileSystem`, …)
- **Infrastructure** services (`CompilationFactory`, `ProjectDiscoveryService`, `PhysicalFileSystem`, …)
- **Graph algorithms** (Tarjan's SCC for cycle detection)

This package is the foundation for all other `ProjGraph.Lib.*` packages. It is not usually referenced
on its own — use a purpose-specific package instead:

- [`ProjGraph.Lib.Dependencies`](https://www.nuget.org/packages/ProjGraph.Lib.Dependencies)
- [`ProjGraph.Lib.ClassDiagram`](https://www.nuget.org/packages/ProjGraph.Lib.ClassDiagram)
- [`ProjGraph.Lib.EntityFramework`](https://www.nuget.org/packages/ProjGraph.Lib.EntityFramework)
- [`ProjGraph.Lib`](https://www.nuget.org/packages/ProjGraph.Lib) — meta-package (all of the above)

## Usage

Register all core services:

```csharp
using ProjGraph.Lib.Core;

services.AddProjGraphCore();
```

### Parse a solution

```csharp
using Microsoft.Extensions.DependencyInjection;
using ProjGraph.Lib.Core;
using ProjGraph.Core.Models;

var services = new ServiceCollection();
services.AddProjGraphCore();
var provider = services.BuildServiceProvider();

var parser = provider.GetRequiredService<ISolutionParser>();
IReadOnlyList<Project> projects = await parser.ParseAsync("MySolution.slnx");
```

## Key Abstractions

| Interface                  | Description                                                         |
|----------------------------|---------------------------------------------------------------------|
| `ISolutionParser`          | Parses `.sln` / `.slnx` into a list of `Project` models             |
| `IProjectParser`           | Parses a single `.csproj` file                                      |
| `IProjectDiscoveryService` | Discovers all projects under a directory                            |
| `ICompilationFactory`      | Creates Roslyn `Compilation` objects from project files             |
| `IDiagramRenderer<T>`      | Generic renderer producing a string diagram from a model            |
| `IFileSystem`              | Abstraction over `System.IO` for testability                        |
| `IOutputConsole`           | Abstraction over console output (Spectre.Console-backed by default) |

## Links

- [GitHub Repository](https://github.com/HandyS11/ProjGraph)
- [Documentation](https://handys11.github.io/ProjGraph)
