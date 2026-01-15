# Quickstart: DbContext ERD Generation

Generate a Mermaid diagram for your Entity Framework Core model.

## CLI Usage

### Generate ERD for a solution

Automatically scans all projects and lists found `DbContexts`.

```bash
projgraph erd --path ./MySolution.sln
```

### Generate ERD for a specific file

Analyzes a single `DbContext` file using static analysis.

```bash
projgraph erd --file ./Data/MyDbContext.cs
```

### Specify a context name

If multiple contexts exist, specify the one you want.

```bash
projgraph erd --path ./MySolution.sln --context "AppDbContext"
```

## MCP Usage

### Tool: `get_erd`

Ask your AI assistant:
"Show me the ERD for the project in /path/to/project"

The tool returns the Mermaid text which can be rendered in views that support Mermaid.

## Mermaid Output Example

```mermaid
erDiagram
    Blog ||--o{ Post : "Posts"
    Post ||--o{ Comment : "Comments"
```
