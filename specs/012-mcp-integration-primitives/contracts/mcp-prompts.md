# MCP Prompts Contract

**Date**: 2026-03-10
**Branch**: `012-mcp-integration-primitives`
**Registration**: `builder.Services.AddMcpServer().WithPrompts<ProjGraphPrompts>()`
**SDK type**: `[McpServerPromptType]` / `[McpServerPrompt]`

---

## Prompt: `architecture_review`

**Description**: Guides an LLM through a comprehensive architecture review of a .NET solution, including dependency visualization and project metrics.

### Arguments

| Name | Type | Required | Description                                                                                   |
|------|------|----------|-----------------------------------------------------------------------------------------------|
| `path` | string | yes | Absolute (or root-relative) path to the solution file (.sln, .slnx) or project file (.csproj) |

### Returned Messages

**Message 1 — User role**:

```none
Please perform a comprehensive architecture review of the .NET solution at: {path}

Use the following steps in order:

1. Call `get_project_graph` with path="{path}" to generate the dependency graph.
2. Call `get_project_stats` with path="{path}" to retrieve architectural metrics.
3. Analyze the results and provide:
   - A summary of the overall architecture and structure
   - Projects with the most dependencies (hotspots)
   - Any suspicious dependency patterns (deep chains, violations of layering)
   - A health assessment: what is well-structured and what could be improved
   - Specific actionable recommendations
```

**Message 2 — Assistant role (pre-fill)**:

```none
I'll analyze the .NET solution architecture step by step. Starting with the dependency graph and metrics.
```

---

## Prompt: `dependency_analysis`

**Description**: Guides an LLM to analyze dependency depth, identify hotspot projects, and detect potential circular dependencies in a .NET solution.

### Arguments

| Name | Type | Required | Default | Description |
|------|------|----------|--------|-------------|
| `path` | string | yes | —      | Absolute path to the solution or project file |
| `topN` | string | no | `"5"`  | Number of top hotspot projects to highlight |

### Returned Messages

**Message 1 — User role**:

```none
Analyze the dependency structure of the .NET solution at: {path}

Steps:
1. Call `get_project_graph` with path="{path}" to visualize the dependency graph.
2. Call `get_project_stats` with path="{path}" and topN={topN} to retrieve depth statistics and top {topN} most-referenced projects.
3. Report:
   - Dependency depth distribution (min, max, average)
   - Top {topN} most-referenced (hotspot) projects and why they matter
   - Any detected circular dependencies — list them explicitly
   - Recommendations to reduce coupling or break cycles
```

**Message 2 — Assistant role**:

```none
I'll analyze the dependency structure and identify hotspots and cycles.
```

---

## Prompt: `database_schema_review`

**Description**: Guides an LLM through reviewing an Entity Framework Core database schema for design quality, relationships, and potential issues.

### Arguments

| Name | Type | Required | Description                                                                        |
|------|------|----------|------------------------------------------------------------------------------------|
| `path` | string | yes | Absolute path to a .cs file containing a `DbContext` or `ModelSnapshot`            |
| `contextName` | string | no | Specific DbContext or ModelSnapshot class name to use if multiple exist in the file |

### Returned Messages

**Message 1 — User role**:

```none
Please review the Entity Framework Core database schema defined in: {path}{contextName_clause}

Steps:
1. Call `get_erd` with path="{path}"{context_arg} to generate the Entity Relationship Diagram.
2. Analyze the ERD and provide:
   - An overview of the entity model (count, main entities, key relationships)
   - Assessment of relationship cardinality (one-to-many, many-to-many, etc.)
   - Any potential design issues: missing indexes, over-normalized or under-normalized tables, overly wide entities
   - Naming consistency review
   - Recommendations for improving the schema design
```

Where `{contextName_clause}` = `" (using context: {contextName})"` if contextName is provided, else empty.
Where `{context_arg}` = `, contextName="{contextName}"` if contextName is provided, else empty.

**Message 2 — Assistant role**:

```none
I'll review the Entity Framework Core database schema and identify design issues and improvements.
```

---

## Prompt: `class_structure_review`

**Description**: Guides an LLM through reviewing C# class hierarchies, inheritance patterns, and overall design structure.

### Arguments

| Name | Type | Required | Description                                         |
|------|------|----------|-----------------------------------------------------|
| `path` | string | yes | Absolute path to a .cs file or directory to analyze |

### Returned Messages

**Message 1 — User role**:

```none
Please review the class structure and design of the C# code at: {path}

Steps:
1. Call `get_class_diagram` with path="{path}" and options including includeInheritance=true and includeDependencies=true to generate the class diagram.
2. Analyze the diagram and provide:
   - Overview of the class hierarchy (depth, breadth, key types)
   - Assessment of inheritance vs. composition usage
   - Identification of potential design issues: god classes, deep inheritance chains, circular dependencies between classes
   - Interface segregation: are interfaces focused and cohesive?
   - Recommendations to improve the design (e.g., extract interfaces, favor composition)
```

**Message 2 — Assistant role**:

```none
I'll review the class structure and design patterns in the provided C# code.
```

---

## Implementation Notes

- Class: `ProjGraphPrompts` in `src/ProjGraph.Mcp/ProjGraphPrompts.cs`
- Attribute: `[McpServerPromptType]` on class, `[McpServerPrompt]` on each method
- Return type: `IEnumerable<ChatMessage>` (from `Microsoft.Extensions.AI`)
- All methods are `static` (no DI dependencies — prompts are pure functions of their arguments)
- Parameter binding: `string path`, `string? contextName = null`, `string topN = "5"` (MCP prompt args are always strings)
- Registration: `.WithPrompts<ProjGraphPrompts>()` in `Program.cs`
