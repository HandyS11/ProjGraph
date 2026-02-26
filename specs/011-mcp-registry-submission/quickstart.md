# Quickstart: MCP Registry Submission

## For Maintainers

### 1. Verification Tag

Ensure the following comment is at the bottom of `src/ProjGraph.Mcp/README.md`:

```html
<!-- mcp-name: io.github.handys11/projgraph -->
```

### 2. Registry Credentials

Add a repository secret named `MCP_REGISTRY_TOKEN` with your Official MCP Registry API token.

### 3. Publishing

The submission happens automatically during the release workflow when a new tag `v*` is pushed.
To manually trigger a preview:

```bash
export MCP_REGISTRY_TOKEN=...
npx mcp-publisher publish src/ProjGraph.Mcp/.mcp/server.json
```

## Troubleshooting

- **Verification Failed**: Ensure the README in the `.nupkg` actually contains the tag. `dotnet nuget push` must complete before the registry can scan it.
- **Validation Failed**: Validate `server.json` against the official schema.
