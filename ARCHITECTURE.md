# Architecture

## Overview

ProjGraph is a .NET tool ecosystem for visualizing project dependencies, database schemas, and class hierarchies. It
exposes two entry points — a CLI and an MCP server — both backed by a shared library layer.

## Solution Structure

```none
ProjGraph.slnx
├── src/
│   ├── ProjGraph.Cli          # Spectre.Console CLI entry point
│   ├── ProjGraph.Mcp          # MCP server entry point (JSON-RPC over stdio)
│   ├── ProjGraph.Lib          # Composition root — wires all sub-libraries via DI
│   ├── ProjGraph.Lib.Core     # Shared abstractions, parsers, infrastructure
│   ├── ProjGraph.Lib.ProjectGraph   # Solution/project dependency graph analysis
│   ├── ProjGraph.Lib.ClassDiagram   # C# class hierarchy analysis (Roslyn)
│   ├── ProjGraph.Lib.EntityFramework # EF Core DbContext/ModelSnapshot ERD analysis
│   └── ProjGraph.Core         # Shared domain models (SolutionGraph, ClassModel, EfModel)
├── tests/
│   ├── ProjGraph.Tests.Unit.*         # Unit tests per library
│   ├── ProjGraph.Tests.Integration.*  # Integration tests for CLI and MCP
│   ├── ProjGraph.Tests.Contract       # MCP contract & DI wiring tests
│   └── ProjGraph.Tests.Shared         # Shared test helpers
└── samples/                           # Sample projects used by integration tests
```

## Dependency Graph

```none
Cli ──┐
      ├──► Lib ──► Lib.Core ──► Core
Mcp ──┘       ├──► Lib.ProjectGraph ──► Lib.Core
              ├──► Lib.ClassDiagram ──► Lib.Core
              └──► Lib.EntityFramework ──► Lib.Core
```

## Key Design Decisions

### Composition Root (`ProjGraph.Lib`)

`ProjGraph.Lib` is a thin DI composition layer. It exposes a single extension method `AddProjGraphLib()` that registers
all sub-library services. Both the CLI and MCP server call this method to wire up the full dependency graph.

### Abstractions in `Lib.Core`

Cross-cutting concerns live in `Lib.Core.Abstractions`:

- **`IFileSystem`** — File I/O abstraction for testability.
- **`IOutputConsole`** — Console output abstraction; the MCP server substitutes a `NullOutputConsole` to prevent ANSI
  markup on the JSON-RPC transport.
- **`ICompilationFactory`** — Roslyn compilation creation.
- **`IDiagramRenderer<T>`** — Format-agnostic rendering (Mermaid, tree, flat). Each renderer exposes a `Format` property
  for keyed resolution.

### Use Case Pattern

Each feature library follows a use-case pattern:

```none
Application/
├── IServiceInterface.cs        # Public service interface
├── ServiceImplementation.cs    # Orchestrates use cases
└── UseCases/
    └── SpecificUseCase.cs      # Single-responsibility operation
```

### Rendering Pipeline

Diagram generation follows: **Parse → Model → Render**.

1. **Parse**: Source files are parsed into domain models (`SolutionGraph`, `ClassModel`, `EfModel`).
2. **Model**: Models are pure data (records/classes) in `ProjGraph.Core`.
3. **Render**: `IDiagramRenderer<T>` implementations convert models to output strings. Multiple renderers can be
   registered for one model type (e.g., tree, flat, and Mermaid for `SolutionGraph`).

### Entity Framework Analysis

EF analysis supports two input types:

- **DbContext files** — Analyzed via Roslyn semantic analysis of `DbSet<>` properties and `OnModelCreating` Fluent API
  configurations.
- **ModelSnapshot files** — Analyzed via Roslyn parsing of the compiled migration model.

The Fluent API parser is split across focused classes: `FluentApiConfigurationParser` (orchestration),
`RelationshipConfigParser`, `PropertyConfigParser`, `DefaultValueResolver`, and `FluentApiParsingUtilities`.

### Class Diagram Analysis

Class analysis uses Roslyn to:

1. Parse the target `.cs` file or **directory** (scanning recursively for `.cs` files) for type declarations.
2. For directories, all discovered files are included in a single `CSharpCompilation` for cross-file relationship
   analysis.
3. Automatically excludes standard directories: `.git`, `bin`, `obj`, `node_modules`.
4. Optionally discover related types across the workspace (inheritance, dependencies) via `IWorkspaceTypeDiscovery`.
5. Control traversal depth via `maxDepth` parameter.
6. Includes a large-set warning (50+ files) to prevent unreadable diagrams.

## Build & Quality

- **Target Framework**: .NET 10.0
- **Central Package Management**: `Directory.Packages.props`
- **Code Quality**: `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`
- **CI**: GitHub Actions on `ubuntu-latest` and `windows-latest`

## Release & Distribution

Releases are triggered by pushing a `v*` Git tag and are fully automated via `.github/workflows/publish.yml`.

### Release Flow

```none
Tag push (v*)
    │
    ├── Update version in Directory.Build.props & server.json
    ├── dotnet build + test
    ├── dotnet pack → ./artifacts/*.nupkg
    ├── dotnet nuget push → NuGet.org          (requires NUGET_API_KEY secret)
    ├── dotnet nuget push → GitHub Packages    (uses GITHUB_TOKEN)
    ├── mcp-publisher publish              (GitHub OIDC auth, no token required)
    │       └── Submits src/ProjGraph.Mcp/.mcp/server.json to the Official MCP Registry
    │           Retried up to 3× via nick-fields/retry@v3
    └── Create GitHub Release with release notes
```

### MCP Registry Ownership Verification

The Official MCP Registry verifies package ownership before accepting a submission by scanning the NuGet package
README for a hidden HTML comment:

```html
<!-- mcp-name: io.github.handys11/projgraph -->
```

This comment must be present at the end of `src/ProjGraph.Mcp/README.md`. The identifier in the comment must exactly
match the `"name"` field in `src/ProjGraph.Mcp/.mcp/server.json`.
