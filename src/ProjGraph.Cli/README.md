# ProjGraph CLI

Command-line tool for visualizing .NET project dependencies, generating Entity Relationship Diagrams, and visualizing
class hierarchies.

## Installation

```bash
dotnet tool install -g ProjGraph.Cli
```

## Commands

### `visualize` - Project Dependencies

Visualize solution/project dependencies as ASCII tree or Mermaid diagram.

```bash
# ASCII tree (default)
projgraph visualize ./MySolution.sln

# Mermaid diagram
projgraph visualize ./MySolution.slnx --format mermaid > graph.mmd

# Mermaid diagram without title header
projgraph visualize ./MySolution.slnx --format mermaid --show-title false
```

**Settings**:

- `[path]`: Path to `.sln`, `.slnx`, or `.csproj` file.
- `-f|--format`: Output format (`flat`, `tree`, `mermaid`). Default: `mermaid`.
- `--show-title <true|false>`: Include diagram title. Default: `true`.

**Supports**: `.sln`, `.slnx`, `.csproj`

**Example output**:

```mermaid
graph TD
    MyApp.Web --> MyApp.Core
    MyApp.Infrastructure --> MyApp.Core
```

### `erd` - Entity Relationship Diagrams

Generate Mermaid ERD from EF Core `DbContext` or `ModelSnapshot` files.

```bash
# Generate ERD from DbContext
projgraph erd ./Data/MyDbContext.cs

# Generate ERD from ModelSnapshot (useful if migrations already exist)
projgraph erd ./Migrations/MyDbContextModelSnapshot.cs

# Save to file
projgraph erd ./Data/MyDbContext.cs > database-schema.md

# Generate without title header
projgraph erd ./Data/MyDbContext.cs --show-title false
```

**Settings**:

- `[path]`: Optional path to `.cs` file. Searches current directory if not specified.
- `-c|--context <NAME>`: Optional context/snapshot name.
- `--show-title <true|false>`: Include diagram title. Default: `true`.

**Features**:

- Detects entities, properties, and relationships from source or snapshots
- Shows primary keys, foreign keys, and constraints
- Supports inheritance and base classes
- Extracts `MaxLength`, `Required`, and other data annotations
- Detects Fluent API configurations
- Handles many-to-many relationships with join tables

**Example output**:

```mermaid
erDiagram
    Publisher {
        int Id PK
        string Name "required, max:200"
        string Country "max:100"
    }
    Book {
        int Id PK
        string Title "required, max:300"
        int PublisherId FK "required"
    }

    Publisher ||--o{ Book : "Books"
```

### `classdiagram` - Class Hierarchies

Generate Mermaid Class Diagram for a specific class and its hierarchy.

```bash
# Analyze a specific class and discover its base types and dependencies
projgraph classdiagram ./Models/Admin.cs

# Specify depth of discovery (default: 1)
projgraph classdiagram ./Models/Admin.cs --depth 5

# Generate without title header
projgraph classdiagram ./Models/Admin.cs --show-title false

# Hide properties or functions
projgraph classdiagram ./Models/Admin.cs --properties false --functions false
```

**Settings**:

- `[path]`: Path to the `.cs` file.
- `-i|--inheritance`: Include base classes/interfaces. Default: `false`.
- `-d|--dependencies`: Include dependent types. Default: `false`.
- `--properties <true|false>`: Show properties and fields in the diagram. Default: `true`.
- `--functions <true|false>`: Show functions and methods in the diagram. Default: `true`.
- `--depth <INT>`: Max discovery depth. Default: `1`.
- `--show-title <true|false>`: Include diagram title. Default: `true`.

**Features**:

- Detects properties, fields, and inheritance (`<|--`)
- Discovers dependencies via property types (`*--` or `--`)
- Simple heuristic workspace-wide discovery of missing types (scans for `.sln`, `.slnx`, or `.csproj`)
- Support for generic types (sanitized for Mermaid as `~T~`)

**Example output**:

```mermaid
classDiagram
    class User {
        +string Name
        +Address PrimaryAddress
    }
    class Admin {
        +Permissions Rights
    }
    User <|-- Admin
    User *-- Address
```

## Requirements

- .NET 10.0 or later

## License

Licensed under the terms specified in the [repository](https://github.com/HandyS11/ProjGraph).
