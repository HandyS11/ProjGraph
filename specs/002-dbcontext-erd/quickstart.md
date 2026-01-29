# Quickstart: ERD Generation (DbContext & ModelSnapshot)

Generate a Mermaid diagram for your Entity Framework Core model from source code or migration snapshots.

## CLI Usage

### Generate ERD for the current directory

Automatically searches for `*DbContext.cs` and `*ModelSnapshot.cs` files in the current folder.

```bash
projgraph erd
```

### Generate ERD for a specific file

Analyzes a specific `DbContext` or `ModelSnapshot` file.

```bash
# Using source context
projgraph erd ./Data/MyDbContext.cs

# Using migration snapshot (highly accurate for current DB state)
projgraph erd ./Migrations/MyDbContextModelSnapshot.cs
```

### Specify a context or snapshot name

If multiple relevant classes exist in a file, specify the one you want.

```bash
projgraph erd ./Data/MyDbContext.cs --context MySpecificContext
```

## MCP Usage

### Tool: `get_erd`

Ask your AI assistant:
"Show me the ERD for the DbContext or ModelSnapshot in /path/to/MyFile.cs"

The tool returns the Mermaid text which can be rendered in views that support Mermaid.

## Mermaid Output Example

```mermaid
erDiagram
    Blog ||--o{ Post : "Posts"
    Post ||--o{ Comment : "Comments"
```
