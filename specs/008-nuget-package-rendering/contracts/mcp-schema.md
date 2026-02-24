# API Contract: NuGet Package Reference Rendering in `visualize`

## MCP Tool Definition: `get_project_graph`

### Request Schema

```json
{
  "name": "get_project_graph",
  "arguments": {
    "path": {
      "type": "string",
      "description": "Absolute path to the project or solution file."
    },
    "showTitle": {
      "type": "boolean",
      "description": "Whether to include the title in the diagram (default: true).",
      "default": true
    },
    "includePackages": {
      "type": "boolean",
      "description": "Whether to include NuGet package dependencies in the graph (default: false).",
      "default": false
    }
  }
}
```

### Response Schema

The response format remains `string`. When `includePackages` is `true`, the resulting Mermaid code will include NuGet package nodes.

#### Updated Mermaid Syntax Rules

- **Projects**: `id["Name (Type)"]` (Rectangle)
- **Packages**: `id("Name (Version)")` (Rounded Corners)
- **Relationships**: `Project --> Package`

## Command-Line Interface (CLI)

### Command: `visualize`

#### New Arguments/Options

`--include-packages`: Boolean flag to enable NuGet package rendering in the output.

#### Syntax Example

```bash
projgraph visualize ./MySolution.sln --format mermaid --include-packages
```

## Library Contracts (Internal APIs)

### `IGraphService`

```csharp
SolutionGraph BuildGraph(string path, bool includePackages = false);
```

### `BuildGraphUseCase`

```csharp
SolutionGraph Execute(string path, bool includePackages = false);
```

### `IProjectParser`

```csharp
(Project Project, IEnumerable<string> ProjectReferences, IEnumerable<PackageReference> PackageReferences) Parse(string projectPath);
```

Where `PackageReference` is a new record:

```csharp
public record PackageReference(string Name, string Version);
```

### `DiagramOptions` (Modified)

```csharp
public record DiagramOptions(bool ShowTitle = true, bool WrapInMarkdownFence = false, bool IncludePackages = false);
```

*(This allows renderers to know if they should look for package nodes, although they can also just rely on `ProjectType` in the model).*
