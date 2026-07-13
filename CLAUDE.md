# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore dependencies
dotnet restore ProjGraph.slnx

# Build
dotnet build ProjGraph.slnx

# Run all tests
dotnet test ProjGraph.slnx

# Run a specific test project
dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram

# Run a specific test class
dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "ClassAnalysisDepthTests"

# Run CLI locally
dotnet run --project src/ProjGraph.Cli -- visualize ./ProjGraph.slnx
dotnet run --project src/ProjGraph.Cli -- stats ./ProjGraph.slnx

# Run MCP server locally
dotnet run --project src/ProjGraph.Mcp
```

Build enforces `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true`. XML documentation is required on all public APIs.

## Architecture

ProjGraph is a .NET 10 tool ecosystem with two entry points — a **CLI** (`Spectre.Console.Cli`) and an **MCP server** (`ModelContextProtocol.Server` over stdio) — both backed by a shared library layer.

### Dependency graph

```sh
Cli ──┐
      ├──► Lib ──► Lib.Core ──► Core
Mcp ──┘       ├──► Lib.Dependencies ──► Lib.Core
              ├──► Lib.ClassDiagram ──► Lib.Core
              └──► Lib.EntityFramework ──► Lib.Core
```

- **`ProjGraph.Core`** — Pure domain models (`SolutionGraph`, `ClassModel`, `EfModel`, `SolutionStats`) and exceptions. No dependencies.
- **`ProjGraph.Lib.Core`** — Cross-cutting abstractions (`IFileSystem`, `IOutputConsole`, `ICompilationFactory`, `IDiagramRenderer<T>`), solution parsers (`.sln`/`.slnx`), and infrastructure utilities. Also hosts the `TarjanSccAlgorithm` (in `Domain/Algorithms/`) used for cycle detection.
- **`ProjGraph.Lib.Dependencies`** — Builds dependency graphs from solution/project files by parsing them directly via `Microsoft.Build.Construction` (the in-house `SlnParser`/`SlnxParser`/`ProjectParser` in `Lib.Core`); computes stats using `TarjanSccAlgorithm` for cycle detection.
- **`ProjGraph.Lib.ClassDiagram`** — Roslyn-based C# class hierarchy analysis. Accepts a file or directory; optionally discovers related types across the workspace via `IWorkspaceTypeDiscovery`.
- **`ProjGraph.Lib.EntityFramework`** — Roslyn semantic analysis of EF Core `DbContext` files and `ModelSnapshot` files; includes a multi-class Fluent API parser.
- **`ProjGraph.Lib`** — Composition root only. Exposes `AddProjGraphLib()` which wires all sub-library services via DI.

### Key patterns

**Use-case pattern** — Each feature library organises logic as:

```sh
Application/
├── IServiceInterface.cs
├── ServiceImplementation.cs
└── UseCases/
    └── SpecificUseCase.cs
```

**Rendering pipeline** — Parse → Model → Render. Models are pure records in `ProjGraph.Core`. `IDiagramRenderer<T>` implementations are resolved by keyed DI (each renderer exposes a `Format` property). Multiple renderers can exist per model type (tree, flat, Mermaid).

**MCP stdout safety** — The MCP server overrides `IOutputConsole` with `NullOutputConsole` to prevent ANSI markup from leaking onto the JSON-RPC stdio transport.

**New feature checklist:**

1. Add domain models to `ProjGraph.Core` if needed.
2. Implement in the appropriate `Lib.*` project following the use-case pattern.
3. Register services in that library's `DependencyInjection.cs`.
4. Expose via CLI command (`src/ProjGraph.Cli/Commands/`) and/or MCP tool (`src/ProjGraph.Mcp/Program.cs`).
5. Add unit and integration tests.

### Test projects

| Project | Purpose |
| --- | --- |
| `Tests.Unit.*` | Unit tests per library |
| `Tests.Integration.Cli` | CLI end-to-end tests |
| `Tests.Integration.Mcp` | MCP tool integration tests |
| `Tests.Contract` | MCP contract validation & DI wiring |
| `Tests.Shared` | Shared helpers (`TestDirectory`, `TestPathHelper`) |

### Release

Releases are triggered by pushing a `v*` tag. The publish workflow builds, packs, pushes to NuGet.org and GitHub Packages, submits to the MCP Registry via `mcp-publisher`, and creates a GitHub Release. The MCP Registry ownership comment (`<!-- mcp-name: io.github.HandyS11/projgraph -->`) must remain at the end of `src/ProjGraph.Mcp/README.md`.
