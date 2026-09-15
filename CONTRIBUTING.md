# Contributing to ProjGraph

Thank you for your interest in contributing to ProjGraph! This guide will help you get started.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- [DocFX](https://dotnet.github.io/docfx/) (for documentation): `dotnet tool install -g docfx`
- A code editor (VS Code or Rider recommended)

## Getting Started

```bash
# Clone the repository
git clone https://github.com/HandyS11/ProjGraph.git
cd ProjGraph

# Restore dependencies
dotnet restore ProjGraph.slnx

# Build
dotnet build ProjGraph.slnx

# Run tests
dotnet test ProjGraph.slnx
```

## Project Structure

See [ARCHITECTURE.md](ARCHITECTURE.md) for a detailed overview of the solution structure and design decisions.

## Development Workflow

1. **Create a branch** from `develop` for your change.
2. **Make your changes** with clear, focused commits.
3. **Add or update tests** — all new features and bug fixes should have test coverage.
4. **Ensure the build passes** — the project uses `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true`.
5. **Open a pull request** against `develop`.

## Code Style

- The project enforces code style at build time via `EnforceCodeStyleInBuild=true`.
- XML documentation is required on all public APIs (enforced via `TreatWarningsAsErrors`).
- Follow existing patterns — use-case classes for new features, `IDiagramRenderer<T>` for new output formats.

## Running Tests

```bash
# All tests
dotnet test ProjGraph.slnx

# Specific test project
dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram

# Specific test class
dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram --filter "ClassAnalysisDepthTests"
```

### Test Organisation

| Project                 | Purpose                                                                                                                                                |
|-------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Tests.Unit.*`          | Unit tests per library                                                                                                                                 |
| `Tests.Integration.Cli` | CLI end-to-end tests                                                                                                                                   |
| `Tests.Integration.Mcp` | MCP tool integration tests                                                                                                                             |
| `Tests.Contract`        | MCP contract validation & DI wiring                                                                                                                    |
| `Tests.Smoke.Aot`       | Native AOT vs JIT parity for the CLI and MCP tools (skipped unless `PROJGRAPH_SMOKE_*` is set; CI runs it through `.github/actions/native-tool-smoke`) |
| `Tests.Shared`          | Shared helpers (`TestDirectory`, `TestPathHelper`)                                                                                                     |

### Native AOT smoke tests

`Tests.Smoke.Aot` compares Native AOT builds of the CLI and MCP server with the JIT build of the
same commit. It is skipped unless the `PROJGRAPH_SMOKE_*` variables are set. CI runs
`.github/scripts/native-tool-smoke.sh` through the `.github/actions/native-tool-smoke` action: it
packs the native tool packages, installs them with `dotnet tool install`, and tests the installed
tools. The `aot-smoke` job does this for linux-x64 on every PR, and `pack.yml` does it for every
release platform. Native AOT on Linux needs `clang` (or `gcc`) and `zlib1g-dev`.

Before a local native pack or publish, delete `src/ProjGraph.Cli/bin/Release/net10.0/<rid>` and
`src/ProjGraph.Mcp/bin/Release/net10.0/<rid>`: a pack reuses that folder's `publish/` output and ships
whatever is left there.

To run the same check locally on Linux:

```bash
bash .github/scripts/native-tool-smoke.sh linux-x64 0.0.0-local.1
```

For a quicker loop, test published binaries instead (run one publish at a time; each takes several GB
of memory):

```bash
dotnet build ProjGraph.slnx -c Release
dotnet publish src/ProjGraph.Cli -c Release -r linux-x64 -o artifacts/native/cli
dotnet publish src/ProjGraph.Mcp -c Release -r linux-x64 -o artifacts/native/mcp
PROJGRAPH_SMOKE_CLI_NATIVE=artifacts/native/cli/ProjGraph.Cli \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE=artifacts/native/mcp/ProjGraph.Mcp \
PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.dll \
dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build
```

## Adding a New Feature

1. Add domain models to `ProjGraph.Core` if needed.
2. Implement the feature in the appropriate `Lib.*` project following the use-case pattern.
3. Register services in the library's `DependencyInjection.cs`.
4. Expose via CLI command and/or MCP tool.
5. Add tests at the unit and integration levels.

## Reporting Issues

Open an issue on GitHub with:

- A clear description of the problem or feature request.
- Steps to reproduce (for bugs).
- Expected vs actual behavior.

## Documentation

The project documentation is built with [DocFX](https://dotnet.github.io/docfx/). Navigation and API configuration is located in the `docfx/` directory.

### Local Preview

To build and preview the documentation locally, use the provided scripts:

**PowerShell (Windows):**

```powershell
./docfx/doc-serve.ps1
```

**Bash (Linux/macOS):**

```bash
./docfx/doc-serve.sh
```

These scripts will:

1. Verify that DocFX is installed.
2. Build the DocFX site.
3. Serve it at `http://localhost:8080`.

### Requirements

- .NET 10.0 SDK
- `docfx` dotnet tool (`dotnet tool install -g docfx`)

All PRs must pass the documentation build with **zero warnings** (`--warningsAsErrors`).
