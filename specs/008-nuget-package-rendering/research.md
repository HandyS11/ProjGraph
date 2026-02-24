# Research finding: NuGet Package Reference Rendering in `visualize`

## Decision 1: `ProjectParser` Extension

- **Decision**: Update `IProjectParser` to return a 3-element tuple `(Project, IEnumerable<string> ProjectRefs, IEnumerable<PackageReference> PackageRefs)`.
- **Rationale**: `ProjectParser` already uses `Microsoft.Build.Construction.ProjectRootElement`, which can efficiently extract both list types from a single XML parse.
- **Alternatives considered**: Separate parser rejected for performance; returning a complex DTO was considered but C# 10+ tuples are idiomatic for small local results.

## Decision 2: `SolutionGraph` Model Adjustment

- **Decision 2 Final**: Add `ProjectType.Package` to `ProjGraph.Core.Models.ProjectType`. Packages are added as standard `Project` records with SHA256-hashed deterministic Guids.
- **Rationale**: Reusing `SolutionGraph`'s existing node and edge lists keeps path analysis and generic graph logic working without structural changes. Node versioning is stored in `FullPath` to avoid adding package-specific fields to the base `Project` record.

## Decision 3: Flag Propagation

- **Decision**: Update `BuildGraphUseCase` and `IGraphService` to accept `bool includePackages`. Parameters added to CLI `VisualizeCommand.Settings` and the MCP `get_project_graph` tool.
- **Rationale**: Simplifies the API and keeps it consistent across the CLI and MCP interfaces.

## Decision 4: Rendering Styles

- **Mermaid**: Hexagon `id{{label}}` for package nodes, dotted arrows `-.->` for edges.
- **Tree/Flat**: `📦` emoji icon, yellow/italic styling for package name/arrows, and version in parentheses.
- **Spectre.Console**: Escaping `[pkg]` (if used) as `[[pkg]]` to avoid markup parsing errors, although final choice shifted to 📦 icon + color for better visual impact.

## Technology Choices

- **Microsoft.Build.Construction**: Lightweight XML parsing of `.csproj`.
- **Spectre.Console**: Used for high-fidelity tree and flat lists with color support.
- **SHA256**: Used for deterministic ID generation for package nodes.

## Integration & Patterns

- **MCP**: Add `bool includePackages = false` to `get_project_graph` tool description. This ensures LLMs are aware of the capability.
- **Clean Architecture**: Ensure the CLI simply parses the flag and passes it down to the domain logic.
