# Data Model: NuGet Package Reference Rendering in `visualize`

## Entities

### `ProjectType` Enrichment

Update `ProjGraph.Core.Models.ProjectType` to include a new value:

- `Package = 4`: Represents an external NuGet package dependency.

### `Project` Record

The `Project` record will be used to represent NuGet packages. When representing a package:

- `Name`: The package ID (e.g., `Newtonsoft.Json`).
- `FullPath`: The package version (e.g., `13.0.1`).
- `RelativePath`: Same as `FullPath` (or empty).
- `Framework`: The target framework version of the project referencing it.
- `Type`: `ProjectType.Package`.

### `SolutionGraph`

The `Projects` collection will now contain both projects and packages.
The `Dependencies` collection will contain `Dependency` records where:

- `SourceId` is the project ID.
- `TargetId` is the package ID.
- `Type` is `DependencyType.PackageReference`.

## Relationships

- **Project -> Project**: `DependencyType.ProjectReference` (Existing)
- **Project -> Package**: `DependencyType.PackageReference` (New)

## Validation Rules

- **Direct Only**: Transitive package dependencies (packages depending on other packages) are NOT included.
- **Deduplication**: If multiple projects reference the same package version, only one `Project` record with `ProjectType.Package` should exist in the `SolutionGraph.Projects` list.

## Lifecycle / State Transitions

1. **Parsing Phase**: `ProjectParser` extracts `<PackageReference>` items.
2. **Building Phase**: `BuildGraphUseCase` collects all unique package/version pairs if `includePackages` is true.
3. **Mapping Phase**: Packages are added as `Project` objects with `ProjectType.Package`.
4. **Rendering Phase**: Renderers use the `Type` property to choose the appropriate visual style.
