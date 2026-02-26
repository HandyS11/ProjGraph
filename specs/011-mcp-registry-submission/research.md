# Research: MCP Registry Submission

## Decision: `mcp-publisher` CLI usage

- **Chosen**: `npx mcp-publisher publish <path-to-server.json>`
- **Rationale**: Using `npx` ensures the tool is always at the latest version without requiring global installation in the CI environment.
- **Alternatives considered**: `npm install -g mcp-publisher`. Rejected because global installation can be slower and more prone to version conflicts in shared CI environments.

## Decision: Authentication for `mcp-publisher`

- **Chosen**: Pass token via `MCP_REGISTRY_TOKEN` environment variable.
- **Rationale**: Standard practice for GitHub Actions and most CLI tools.
- **Alternatives considered**: Passing via `--token` flag. Rejected as it might expose secrets in logs if not handled carefully (though `WIK-001` suggests environment variables are safer).

## Decision: README Ownership Verification Comment

- **Chosen**: Addition of `<!-- mcp-name: io.github.handys11/projgraph -->` at the very end of the NuGet README (`src/ProjGraph.Mcp/README.md`).
- **Rationale**: Placing it at the end ensures it doesn't interfere with the human-readable description while remaining visible to automated scanners.
- **Alternatives considered**: Placing at the top. Rejected to keep the primary header clean for users.

## Decision: CI Retry Policy

- **Chosen**: `nick-fields/retry@v3` for the publish step in `publish.yml`.
- **Rationale**: Registry API endpoints can be flaky; a simple 3-attempt retry with backoff minimizes false pipeline failures.
- **Alternatives considered**: Manual retry logic in bash/powershell. Rejected as it adds complexity compared to a well-tested action.
