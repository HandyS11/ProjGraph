# MCP Tool Contract: get_class_diagram

## Description

Generates a Mermaid class diagram for the types defined in a specific C# file or directory, with options to discover inheritance and related types in the workspace.

## Parameters

- `path`: (string) **Required**. Absolute path to the .cs file OR directory to analyze.
- `options`: (AnalysisOptions) **Optional**. Analysis and discovery options.
  - `depth`: (number) **Default 1**. Max depth for relationship discovery.
  - `includeInheritance`: (boolean) **Default false**. Discover base classes and interfaces in the workspace.
  - `includeDependencies`: (boolean) **Default false**. Discover dependent types in the workspace.
  - `includeProperties`: (boolean) **Default true**. Show properties and fields in the diagram.
  - `includeFunctions`: (boolean) **Default true**. Show functions and methods in the diagram.
- `showTitle`: (boolean) **Default true**. Whether to include the title in the diagram.

## Output

A string containing the Mermaid class diagram text.
