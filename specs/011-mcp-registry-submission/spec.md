# Feature Specification: MCP Registry Submission

**Feature Branch**: `011-mcp-registry-submission`
**Created**: 2026-02-26
**Status**: Draft
**Input**: User description: "The two things that were missing (and are now documented): <!-- mcp-name: io.github.handys11/projgraph --> — an HTML comment that must exist in the NuGet package README. The Official MCP Registry uses this to verify package ownership before accepting a submission. Without it, the registry will reject the publish command. mcp-publisher publish server.json — a dedicated CLI tool from the Official MCP Registry that must be run explicitly to submit the server. VS Code's marketplace sources its listing from this registry, not from NuGet.org directly. This step must be repeated on every version release."

## Clarifications

### Session 2026-02-26

- Q: How is the `mcp-publisher` tool installed in the CI environment? → A: Pre-built binary downloaded from GitHub releases via `curl`
- Q: What authentication mechanism does the `mcp-publisher` command use? → A: GitHub OIDC (no token required; workflow needs `id-token: write` permission) or GitHub PAT (`MCP_GITHUB_TOKEN` secret with `read:org` and `read:user` scopes)
- Q: Where should the `server.json` file be located in the repository? → A: `src/ProjGraph.Mcp`

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ownership Verification (Priority: P1)

As a ProjGraph maintainer, I want to include a standard ownership verification marker in my package documentation so that registration with the Official MCP Registry is successful.

**Why this priority**: Without this marker, the registry will reject all submission attempts, blocking listing in the VS Code marketplace.

**Independent Test**: Can be tested by verifying the presence of the exact HTML comment `<!-- mcp-name: io.github.handys11/projgraph -->` in the README file included in the NuGet package artifacts.

**Acceptance Scenarios**:

1. **Given** a new version of ProjGraph is being prepared, **When** the NuGet package README is generated, **Then** it MUST contain the hidden HTML comment `<!-- mcp-name: io.github.handys11/projgraph -->`.
2. **Given** the NuGet package is uploaded to NuGet.org, **When** the MCP Registry scans the package, **Then** it SHOULD successfully verify ownership based on this tag.

---

### User Story 2 - Registry Submission (Priority: P1)

As a ProjGraph maintainer, I want to explicitly submit my server definition to the MCP Registry using the official publishing tool so that it is listed in the marketplace.

**Why this priority**: The VS Code marketplace specifically looks at the MCP Registry for its listings. Listing on NuGet.org alone is insufficient.

**Independent Test**: Can be tested by executing the publishing command and verifying a successful response from the registry API/CLI.

**Acceptance Scenarios**:

1. **Given** a valid `server.json` file, **When** the command `mcp-publisher publish server.json` is executed, **Then** the server metadata SHOULD be transmitted to the Official MCP Registry.
2. **Given** a successful submission, **When** searching the Official MCP Registry, **Then** the updated ProjGraph server entry SHOULD be visible.

---

### User Story 3 - Release Process Integration (Priority: P1)

As a maintainer, I want the registry submission to be a standard part of every release so that users always have access to the latest version in the marketplace.

**Why this priority**: Required to meet SC-004 (Zero manual intervention) and ensure the marketplace remains in sync with NuGet automatically.

**Independent Test**: Can be tested by running the full release pipeline and checking if the registry reflects the new version number without manual intervention.

**Acceptance Scenarios**:

1. **Given** a new version release is triggered, **When** the build/publish pipeline completes, **Then** both NuGet.org and the MCP Registry SHOULD reflect the new version.

---

### Edge Cases

- **Missing `server.json`**: How does the system handle a publish attempt when the configuration file is missing or contains invalid metadata?
- **Network Failure**: What is the retry policy if the MCP Registry is temporarily unreachable during the publish step?
- **Mismatched IDs**: What happens if the `mcp-name` in the README does not match the ID in `server.json`?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The project MUST maintain a README file for the NuGet package that includes the ownership verification comment `<!-- mcp-name: io.github.handys11/projgraph -->`.
- **FR-002**: The project MUST maintain a `server.json` file in `src/ProjGraph.Mcp` containing the necessary MCP server metadata for the registry.
- **FR-003**: The project Release Workflow MUST include a step that installs the `mcp-publisher` binary, authenticates via GitHub OIDC (`mcp-publisher login github-oidc`), and runs `mcp-publisher publish src/ProjGraph.Mcp/.mcp/server.json`.
- **FR-004**: The release process MUST fail if the `mcp-publisher` command returns a non-zero exit code.
- **FR-005**: The `mcp-publisher` command MUST be executed after the NuGet package is successfully pushed to ensure ownership can be verified by the registry.
- **FR-006**: The system MUST ensure the identifier in `server.json` matches the `mcp-name` in the README to prevent verification failures.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Verification status: MCP Registry successfully acknowledges ownership of the `io.github.handys11/projgraph` name.
- **SC-002**: Listing visibility: ProjGraph is searchable and listed with correct metadata in the Official MCP Registry.
- **SC-003**: Version synchronization: The version of ProjGraph in the MCP Registry matches the latest version available on NuGet.org within 10 minutes of release.
- **SC-004**: Zero manual intervention: The entire verification and publishing flow completes automatically during the GitHub Actions release workflow.

## Assumptions

- **A-001**: The `mcp-publisher` tool is a pre-built binary available from GitHub releases and installed in CI via `curl` from `https://github.com/modelcontextprotocol/registry/releases/latest`.
- **A-002**: The `server.json` schema is stable and documented by the MCP Registry.
- **A-003**: For `io.github.handys11/` namespaces, the MCP Registry uses GitHub OIDC authentication in CI (no token required; `id-token: write` permission grants access automatically). Alternatively, a GitHub PAT with `read:org` and `read:user` scopes can be stored as `MCP_GITHUB_TOKEN`.

## Dependencies

- **Official MCP Registry**: Availability of the registry and its publishing API.
- **mcp-publisher CLI**: Availability and compatibility of the tools within the build environment.
- **NuGet.org**: The package must be publicly available on NuGet for the registry to verify the ownership tag.
