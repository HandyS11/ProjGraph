# ProjGraph MCP Server

[Model Context Protocol](https://modelcontextprotocol.io/) (MCP) server that enables AI assistants to analyze .NET
solution architectures, generate Entity Relationship Diagrams, and visualize class hierarchies.

## Requirements

- .NET 10.0 or later runtime

## Available Tools

### `get_project_graph`

Analyzes a solution or project file and returns the dependency graph as a Mermaid diagram.

**Parameters:**

- `path` (string): Absolute path to `.sln`, `.slnx`, or `.csproj` file

**Returns:** Mermaid graph diagram code

**Example prompts:**

```text
"Analyze the dependencies in ./MySolution.slnx"
"Show me the project structure"
"Are there any circular dependencies?"
```

### `get_erd`

Generates a Mermaid Entity Relationship Diagram from an EF Core DbContext file.

**Parameters:**

- `path` (string): Absolute path to DbContext `.cs` file
- `contextName` (string, optional): Specific DbContext class name if multiple exist

**Returns:** Mermaid ERD diagram code

**Features:**

- Detects entities, properties, and relationships
- Shows primary keys, foreign keys, and constraints
- Supports inheritance and base classes
- Extracts MaxLength, Required, and other data annotations
- Detects Fluent API shadow relationships
- Handles many-to-many with join tables

**Example prompts:**

```text
"Show me the database schema from ./Data/MyDbContext.cs"
"Generate an ERD for my DbContext"
"What are the entity relationships in my database?"
```

### `get_class_diagram`

Generates a Mermaid class diagram for the types defined in a specific C# file, with options to discover inheritance and
related types in the workspace.

**Parameters:**

- `filePath` (string): Absolute path to the C# file to analyze
- `includeInheritance` (boolean, optional): Whether to search the workspace for base classes and interfaces (default:
  false)
- `includeDependencies` (boolean, optional): Whether to search for and include other classes used as properties or
  fields (default: false)
- `depth` (number, optional): How many levels of relationships to follow (default: 1)

**Returns:** Mermaid class diagram code

**Example prompts:**

```text
"Show me the class hierarchy for ./Models/Admin.cs"
"Visualize the dependencies of the GuestUser class in Guest.cs"
"Draw a class diagram for my domain model starting at ./Domain/Entity.cs with inheritance"
"Generate a class diagram with all dependencies at depth 2"
```

## License

Licensed under the terms specified in the [repository](https://github.com/HandyS11/ProjGraph).
