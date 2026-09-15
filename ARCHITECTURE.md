# Architecture

## Overview

ProjGraph is a .NET tool ecosystem for visualizing project dependencies, database schemas, and class hierarchies. It
exposes two entry points — a CLI and an MCP server — both backed by a shared library layer.

## Solution Structure

```sh
ProjGraph.slnx
├── src/
│   ├── ProjGraph.Cli                   # Spectre.Console CLI entry point
│   ├── ProjGraph.Mcp                   # MCP server entry point (JSON-RPC over stdio)
│   ├── ProjGraph.Lib                   # Composition root — wires all sub-libraries via DI
│   ├── ProjGraph.Lib.Core              # Shared abstractions, parsers, infrastructure
│   ├── ProjGraph.Lib.Dependencies      # Solution/project dependency graph analysis
│   ├── ProjGraph.Lib.ClassDiagram      # C# class hierarchy analysis (Roslyn)
│   ├── ProjGraph.Lib.EntityFramework   # EF Core DbContext/ModelSnapshot ERD analysis
│   └── ProjGraph.Core                  # Shared domain models (SolutionGraph, ClassModel, EfModel)
├── tests/
│   ├── ProjGraph.Tests.Unit.*          # Unit tests per library
│   ├── ProjGraph.Tests.Integration.*   # Integration tests for CLI and MCP
│   ├── ProjGraph.Tests.Contract        # MCP contract & DI wiring tests
│   ├── ProjGraph.Tests.Smoke.Aot       # Native AOT vs JIT parity (CI aot-smoke job)
│   └── ProjGraph.Tests.Shared          # Shared test helpers
└── samples/                            # Sample projects used by integration tests
```

## Dependency Graph

```sh
Cli ──┐
      ├──► Lib ──► Lib.Core ──► Core
Mcp ──┘       ├──► Lib.Dependencies ──► Lib.Core
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
- **CI**: GitHub Actions. `ci.yml` builds and tests on `ubuntu-latest`, `windows-latest`, and `macos-latest`, plus
  the `aot-smoke` job; `pack.yml` adds `ubuntu-24.04-arm` runners and Alpine containers for the native packages

## Release & Distribution

Releases are triggered by pushing a `v*` Git tag and are fully automated via `.github/workflows/publish.yml`.

### Release Flow

```sh
Tag push (v*)
    │
    ├── prepare: dotnet build -p:Version=<tag version> + dotnet test
    ├── pack (.github/workflows/pack.yml)
    │     ├── pack-native ×6, each on a matching runner (linux-musl-* inside Alpine):
    │     │     win-x64 · linux-x64 · linux-arm64 · linux-musl-x64 · linux-musl-arm64 · osx-arm64
    │     │     stamp the server.json version → dotnet pack -r <rid>
    │     │     → dotnet tool install from the packages → Tests.Smoke.Aot
    │     └── pack-portable: stamp the server.json version → libraries, pointer packages,
    │           framework-dependent `any` packages → Tests.Smoke.Aot
    └── publish
          ├── dotnet nuget push libraries + native + any → NuGet.org, GitHub Packages
          ├── wait until NuGet.org lists all 14 tool packages (30 min timeout)
          ├── dotnet nuget push pointer packages → NuGet.org, GitHub Packages
          ├── Create GitHub Release with every package attached
          ├── stamp the server.json version (.github/scripts/set-server-json-version.sh)
          ├── wait 300 s for NuGet.org package validation
          └── mcp-publisher publish (GitHub OIDC auth, no token required)
                └── Submits src/ProjGraph.Mcp/.mcp/server.json to the Official MCP Registry
```

`pack.yml` also runs on pull requests that change a project file under `src/`, the MCP `server.json`,
`ProjGraph.slnx`, the smoke suite, the packaging scripts, `Directory.*.props`, or `global.json`.

### Tool Packages

`ProjGraph.Cli` and `ProjGraph.Mcp` are pointer packages that list one package per runtime identifier.
`dotnet tool install` and `dnx` pick the Native AOT package on win-x64, linux-x64, linux-arm64,
linux-musl-x64, linux-musl-arm64, and osx-arm64, and the framework-dependent `.any` package elsewhere.
Installing a pointer package fails when the package for the installing machine's RID isn't on the feed
(it doesn't fall back to `any`), which is why `publish` pushes it last.

The RID list lives in four places, and they must match:

- `ToolPackageRuntimeIdentifiers` in both tool projects (`src/ProjGraph.Cli/ProjGraph.Cli.csproj` and
  `src/ProjGraph.Mcp/ProjGraph.Mcp.csproj`);
- the `pack-native` matrix in `.github/workflows/pack.yml`;
- the RID sets in `.github/scripts/verify-packages.sh`;
- the verify line in `.github/workflows/publish.yml`.

`publish` derives the NuGet.org wait list from the pointer packages (`.github/scripts/pointer-package-ids.sh`),
and it fails before pushing anything if a pointer lists a package that wasn't built.

If a native package misbehaves after a release, remove its RID from all four places and ship a patch;
that platform then installs the `any` package. Remove a `linux-<arch>` RID together with its
`linux-musl-<arch>` RID, and never a musl RID alone. The RID graph treats musl as compatible with
glibc, so musl systems would install the glibc package, which can't start there.

### MCP Registry Ownership Verification

The Official MCP Registry verifies package ownership before accepting a submission by scanning the NuGet package
README for a hidden HTML comment:

```html
<!-- mcp-name: io.github.HandyS11/projgraph -->
```

This comment must be present at the end of `src/ProjGraph.Mcp/README.md`. The identifier in the comment must exactly
match the `"name"` field in `src/ProjGraph.Mcp/.mcp/server.json`.
