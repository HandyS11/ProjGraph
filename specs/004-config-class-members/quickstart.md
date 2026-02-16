# Quickstart: Configuring Class Member Visibility

## Objective

Generate a class diagram that focuses only on high-level relationships by hiding detailed properties and methods.

## Usage with MCP

To generate a diagram with only classes and their relationships (no members):

```json
{
  "name": "GetClassDiagram",
  "arguments": {
    "filePath": "C:/path/to/YourController.cs",
    "includeProperties": false,
    "includeFunctions": false
  }
}
```

To see only the behavioral API (methods):

```json
{
  "name": "GetClassDiagram",
  "arguments": {
    "filePath": "C:/path/to/YourService.cs",
    "includeProperties": false,
    "includeFunctions": true
  }
}
```

## Default Behavior

If omitted, `includeProperties` and `includeFunctions` default to `true`, providing the same detailed output as before.
