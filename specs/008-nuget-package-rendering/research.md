# Research finding: NuGet Package Reference Rendering in `visualize`

## Decision 1: `ProjectParser` Extension

- **Decision**: Update `IProjectParser` to return `IEnumerable<PackageReference>` as well as `IEnumerable<string>` (for project refs).
- **Rationale**: `ProjectParser` already uses `Microsoft.Build.Construction.ProjectRootElement`, which has access to both `ProjectReference` and `PackageReference` items.
- **Alternatives considered**: Creating a separate `IPackageParser` was rejected; redundant to re-read project XML.

## Decision 2: `SolutionGraph` Model Adjustment

- **Decision**: Keep `Project` as the primary node type but maybe introduce a custom field or sub-type if needed.
- **Wait**: `Project` struct represents a node. Packages can also be represented as `Project` nodes but with a different `ProjectType` if we want to reuse the model.
- **Wait 2**: The current `Project` has fields like `Framework`, `FullPath`, which might not strictly apply to packages in the same way. However, reusing `Project` with a new `ProjectType.Package` (to be added) simplifies the graph model.
- **Decision 2 Final**: Add `ProjectType.Package` to `ProjGraph.Core.Models.ProjectType`. Packages will be added as `Project` records with this type.
- **Rationale**: Reusing `SolutionGraph`'s existing `Projects` and `Dependencies` lists keeps renderers and path analysis working without major refactoring.

## Decision 3: Flag Propagation

- **Decision**: Update `BuildGraphUseCase` to accept `bool includePackages`. This trickles up to `IGraphService.BuildGraph`, `VisualizeCommand.Settings`, and the MCP tool parameters.
- **Rationale**: Cleanest way to control graph generation at the application layer.

## Decision 4: Rendering Styles

- **Mermaid**: Use `id(label)` for packages (rounded edges).
- **Tree/Flat**: Use `[pkg]` prefix.
- **Rationale**: Direct implementation of user request.

## Technology Choices

- **Microsoft.Build.Construction**: Used for XML manipulation of `.csproj` files. Standard and already used in the project.
- **Spectre.Console**: Used for tree and flat rendering.

## Integration & Patterns

- **MCP**: Add `bool includePackages = false` to `get_project_graph` tool description. This ensures LLMs are aware of the capability.
- **Clean Architecture**: Ensure the CLI simply parses the flag and passes it down to the domain logic.
