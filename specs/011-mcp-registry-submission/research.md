# Research: MCP Registry Submission

## Decision: `mcp-publisher` CLI usage

- **Chosen**: Download the pre-built binary from GitHub releases via `curl` and run `./mcp-publisher publish <path-to-server.json>`.
- **Rationale**: `mcp-publisher` is not an npm package. It is a Go binary distributed via GitHub releases at `https://github.com/modelcontextprotocol/registry/releases/latest`.
- **Alternatives considered**: `npx mcp-publisher`. Rejected — the tool is not on npm.

## Decision: Authentication for `mcp-publisher`

- **Chosen**: GitHub OIDC (`mcp-publisher login github-oidc`). Requires `id-token: write` permission in the workflow job. No secrets required.
- **Rationale**: The registry validates namespace ownership. For `io.github.handys11/projgraph`, the workflow running on the `HandyS11` repository authenticates automatically via OIDC — the cleanest and most secure approach.
- **Alternatives considered**: GitHub PAT (`MCP_GITHUB_TOKEN` secret with `read:org` and `read:user` scopes). Valid fallback but requires managing a long-lived secret.

## Decision: README Ownership Verification Comment

- **Chosen**: Addition of `<!-- mcp-name: io.github.handys11/projgraph -->` at the very end of the NuGet README (`src/ProjGraph.Mcp/README.md`).
- **Rationale**: Placing it at the end ensures it doesn't interfere with the human-readable description while remaining visible to automated scanners.
- **Alternatives considered**: Placing at the top. Rejected to keep the primary header clean for users.

## Decision: CI Retry Policy

- **Chosen**: `nick-fields/retry@v3` for the publish step in `publish.yml`.
- **Rationale**: Registry API endpoints can be flaky; a simple 3-attempt retry with backoff minimizes false pipeline failures.
- **Alternatives considered**: Manual retry logic in bash/powershell. Rejected as it adds complexity compared to a well-tested action.
