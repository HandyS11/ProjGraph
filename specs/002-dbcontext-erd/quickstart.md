# Quickstart: DbContext ERD Generation

Generate a Mermaid diagram for your Entity Framework Core model.

## CLI Usage

### Generate ERD for the current directory

Automatically searches for `*DbContext.cs` files in the current folder.

```bash
projgraph erd
```

### Generate ERD for a specific file

Analyzes a specific `DbContext` file Using static analysis.

```bash
projgraph erd ./Data/MyDbContext.cs
```

### Specify a context name

If multiple contexts exist in a file, specify the one you want.

```bash
projgraph erd ./Data/MyDbContext.cs --context MySpecificContext
```

## MCP Usage

### Tool: `get_erd`

Ask your AI assistant:
"Show me the ERD for the DbContext in /path/to/MyDbContext.cs"

The tool returns the Mermaid text which can be rendered in views that support Mermaid.

## Mermaid Output Example

```mermaid
erDiagram
    Blog ||--o{ Post : "Posts"
    Post ||--o{ Comment : "Comments"
```
