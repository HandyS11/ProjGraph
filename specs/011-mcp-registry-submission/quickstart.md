# Quickstart: MCP Registry Submission

## For Maintainers

### 1. Verification Tag

Ensure the following comment is at the bottom of `src/ProjGraph.Mcp/README.md`:

```html
<!-- mcp-name: io.github.handys11/projgraph -->
```

### 2. Registry Credentials

No token secret is required. Authentication is handled automatically via GitHub OIDC.
The release workflow job must have the `id-token: write` permission — this is already set in `.github/workflows/publish.yml`.

**Alternative**: If OIDC is unavailable, add a `MCP_GITHUB_TOKEN` repository secret with a GitHub PAT that has `read:org` and `read:user` scopes, then change the login step to:

```bash
./mcp-publisher login github --token $MCP_GITHUB_TOKEN
```

### 3. Publishing

The submission happens automatically during the release workflow when a new tag `v*` is pushed.
To manually publish from a local machine, first authenticate interactively:

```bash
# Download the binary
curl -L "https://github.com/modelcontextprotocol/registry/releases/latest/download/mcp-publisher_$(uname -s | tr '[:upper:]' '[:lower:]')_$(uname -m | sed 's/x86_64/amd64/;s/aarch64/arm64/').tar.gz" | tar xzf - mcp-publisher

# Authenticate via GitHub browser flow
./mcp-publisher login github

# Publish
./mcp-publisher publish src/ProjGraph.Mcp/.mcp/server.json
```

## Troubleshooting

- **Verification Failed**: Ensure the README in the `.nupkg` actually contains the tag. `dotnet nuget push` must complete before the registry can scan it. If in doubt, run `dotnet pack` and inspect the archive with a zip tool.
- **Validation Failed**: Validate `server.json` against the official schema at the URL in the `$schema` field.
- **Transient Network Failure / NuGet Not Yet Indexed**: The CI workflow waits 5 minutes after the NuGet push and then uses `nick-fields/retry@v3` (5 attempts, 2 minutes apart). If all 5 fail, check registry uptime at the official MCP Registry status page and re-run the workflow once it recovers.
- **JWT Token Expired (`token is expired`)**: The OIDC JWT issued by `mcp-publisher login github-oidc` is short-lived. The login command is intentionally placed **inside** the retry loop in CI so a fresh token is obtained on every attempt.
- **Mismatched ID Rejected**: Ensure `"name"` in `src/ProjGraph.Mcp/.mcp/server.json` exactly equals the value after `mcp-name:` in the README comment. Both must be `io.github.handys11/projgraph`.
