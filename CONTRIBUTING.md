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

## Documentation

The project documentation is built with [DocFX](https://dotnet.github.io/docfx/).

To build and serve the documentation locally:

```powershell
./doc-serve.ps1
```

Or on Linux/macOS:

```bash
./doc-serve.sh
```

Navigation and API configuration is located in the `docfx/` directory. For more details on documentation structure,
see [specs/006-docfx-documentation/spec.md](specs/006-docfx-documentation/spec.md).

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

| Project                 | Purpose                                            |
|-------------------------|----------------------------------------------------|
| `Tests.Unit.*`          | Unit tests per library                             |
| `Tests.Integration.Cli` | CLI end-to-end tests                               |
| `Tests.Integration.Mcp` | MCP tool integration tests                         |
| `Tests.Contract`        | MCP contract validation & DI wiring                |
| `Tests.Shared`          | Shared helpers (`TestDirectory`, `TestPathHelper`) |

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

We use [DocFX](https://dotnet.github.io/docfx/) for our documentation site.

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

1. Restore tools.
2. Build the DocFX site.
3. Serve it at `http://localhost:8080`.

### Requirements

- .NET 10.0 SDK
- `docfx` dotnet tool (`dotnet tool install -g docfx`)

All PRs must pass the documentation build with **zero warnings** (`--warningsAsErrors`).
