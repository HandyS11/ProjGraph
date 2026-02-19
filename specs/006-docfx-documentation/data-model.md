# Data Model: 006-docfx-documentation

This document outlines the hierarchical structure and navigation schema for the ProjGraph documentation site.

## 1. Global Navigation (Table of Contents)

The top-level navigation will consist of the following main sections:

- **Home**: Landing page (`index.md`) with value proposition and high-level architecture.
- **Introduction**: General overview, installation, and first steps.
- **CLI Guide**: Usage documentation for the `ProjGraph.Cli` tool.
- **MCP Guide**: Usage documentation for the Model Context Protocol server.
- **API Reference**: Automatically generated technical details for public libraries.
- **Developer Guide**: Technical specifications, design patterns, and contributor info.

---

## 2. Source-to-Site Mappings

DocFX will perform "zero-duplication" mappings from the project directories:

| Physical Source Path           | DocFX Logical Path | Role                   |
|--------------------------------| ------------------ | ---------------------- |
| `README.md`                    | `index.md`         | Project Homepage       |
| `src/ProjGraph.Cli/README.md`  | `cli-guide.md`     | CLI Usage Reference    |
| `src/ProjGraph.Mcp/README.md`  | `mcp-guide.md`     | MCP Protocol Reference |
| `specs/`                       | `dev/specs/`       | Technical Deep Dive    |

**Strategy**: The implementation task will involve **improving existing project READMEs** to provide a rich experience for both GitHub/NuGet viewers and documentation site users.

---

## 3. API Reference Tiers

The API section will follow the standard .NET namespace hierarchy:

- `ProjGraph.Core`: Shared abstractions and models.
- `ProjGraph.Lib.*`: specialized logic libraries.
- `ProjGraph.Cli.*`: CLI-specific implementation details.
- `ProjGraph.Mcp.*`: MCP-specific implementation details.

---

## 4. Entity: Documented MCP Tool

Each MCP tool documented on the site will include:

- **Name**: The string identifier used to call the tool.
- **Description**: Natural language explanation of its purpose.
- **Parameters**: Structured table of inputs (Type, Required, Description).
- **Example Usage**: A block showing a typical JSON-RPC request and response.
- **Source Link**: Direct link to the implementation in GitHub.
