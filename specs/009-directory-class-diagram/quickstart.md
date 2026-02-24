# Quickstart: Folder/Directory Scanning for classdiagram

The `classdiagram` command now supports scanning entire directories to generate combined class diagrams.

## CLI Usage

Generate a diagram for all classes in a specific folder:

```powershell
projgraph classdiagram ./Models/
```

Generate a diagram for a single file:

```powershell
projgraph classdiagram ./Models/User.cs
```

Options like `--inheritance` (`-i`) and `--dependencies` (`-d`) work across the entire set of files discovered in the folder.

## MCP Usage

Use the `get_class_diagram` tool with a directory path:

```json
{
  "name": "get_class_diagram",
  "arguments": {
    "path": "C:/Projects/MyApp/src/Domain/Models",
    "options": {
      "includeInheritance": true
    }
  }
}
```

## Behavior & Performance

- **Recursive**: Subfolders are scanned by default.
- **Exclusions**: `bin`, `obj`, `.git`, and `node_modules` are automatically ignored to keep diagrams clean and analysis fast.
- **Warning Threshold**: If more than 50 `.cs` files are found, a warning will be logged to the console, suggesting a more specific folder path for better readability.
- **Efficiency**: All files are parsed into a single Roslyn compilation, allowing fast and accurate resolution of cross-file relationships.
