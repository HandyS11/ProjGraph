# Data Model Summary: File Output (`--output` flag)

## Entity Update: Command Settings

Adding a new field to CLI command settings to handle the output file path.

### Settings Entity (Used by `VisualizeCommand`, `ErdCommand`, `ClassDiagramCommand`)

- **Field**: `OutputPath`
- **Type**: `string?`
- **CLI Flag**: `-o|--output`
- **Description**: The path to save the diagram output to disk.

### DiagramOptions (No changes needed, existing fields)

- `bool ShowTitle`
- `bool WrapInMarkdownFence`
