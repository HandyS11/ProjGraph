# Data Model: MCP Registry Submission

## Entities

### MCP Registry Listing

- **Identifier**: `io.github.handys11/projgraph` (Unique across registry)
- **Metadata**: Defined in `server.json`
- **Ownership**: Verified via `mcp-name` HTML comment in NuGet README.

### README Verification Tag

- **Format**: `<!-- mcp-name: io.github.handys11/projgraph -->`
- **Location**: `src/ProjGraph.Mcp/README.md` (End of file)
- **Validation**: Regex match `<!--\s*mcp-name:\s*io\.github\.handys11/projgraph\s*-->`

## State Transitions

| Source      | Action      | Destination  | Validation                     |
|-------------|-------------|--------------|--------------------------------|
| Local Code  | GitHub Push | NuGet.org    | README contains tag            |
| NuGet.org   | MCP Sync    | MCP Registry | Tag matches `server.json` name |
| server.json | Publish CLI | MCP Registry | Valid schema & valid token     |
