# Feature Specification: MCP Integration Primitives

**Feature Branch**: `012-mcp-integration-primitives`
**Created**: 2026-03-10
**Status**: Draft
**Input**: User description: "Improve MCP server with better integration primitives like resources and prompts, and optionally roots and notifications"

## Context & Motivation

ProjGraph's MCP server currently exposes **only Tools** (4 tools: `get_project_graph`, `get_class_diagram`, `get_erd`, `get_project_stats`). The MCP specification defines additional primitives — **Resources**, **Prompts**, **Roots**, and **Notifications** — that would significantly improve the LLM integration experience.

**Current limitations:**

- LLMs must re-generate diagrams every time they need to reference them (no caching or addressable outputs)
- Users must manually craft analysis requests; there are no guided workflows or reusable prompt templates
- Every tool call requires an absolute file path — there is no way to auto-discover the user's workspace
- Long-running analyses provide no progress feedback to the user

**Opportunity:** By adopting additional MCP primitives, ProjGraph can offer richer, more contextual interactions — pre-built analysis workflows, addressable cached outputs, workspace-aware path resolution, and real-time progress feedback.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pre-Built Analysis Prompts (Priority: P1)

An AI assistant user wants to perform common architectural analyses without needing to know the exact tool parameters or craft complex requests. They select a pre-built prompt template (e.g., "Architecture Review" or "Database Schema Review") and the assistant generates a well-structured analysis using the appropriate ProjGraph tools behind the scenes.

**Why this priority**: Prompts are the highest-impact primitive because they directly improve the end-user experience. Users today must know tool names and parameters; prompts provide guided, discoverable workflows that lower the barrier to entry and produce consistently high-quality analyses.

**Independent Test**: Can be fully tested by listing available prompts, selecting one, providing the required arguments (e.g., a solution path), and verifying that the returned prompt messages contain properly structured analysis instructions that an LLM can follow.

**Acceptance Scenarios**:

1. **Given** the MCP server is running, **When** a client lists available prompts, **Then** at least four prompt templates are returned (architecture review, dependency analysis, database schema review, class structure review) each with a name, description, and declared arguments.
2. **Given** the user selects the "Architecture Review" prompt and provides a solution file path, **When** the prompt is resolved, **Then** the response contains a sequence of messages that instruct the LLM to call `get_project_graph` and `get_project_stats`, and then analyze the results.
3. **Given** the user selects the "Database Schema Review" prompt and provides a DbContext file path, **When** the prompt is resolved, **Then** the response contains messages instructing the LLM to call `get_erd` and analyze entities, relationships, and potential issues.
4. **Given** the user selects the "Dependency Analysis" prompt, **When** the prompt is resolved, **Then** the messages guide the LLM to generate the project graph, compute stats, identify hotspot projects, and check for circular dependencies.

---

### User Story 2 - Addressable Diagram Resources (Priority: P2)

An AI assistant user generates a diagram (project graph, class diagram, or ERD) during a conversation. Later in the same session, they want to reference that diagram again — for example, to compare it with another diagram, or to ask follow-up questions about specific elements. Instead of re-running the analysis tool, the previously generated output is available as an addressable resource the LLM can read directly.

**Why this priority**: Resources provide addressable, cacheable outputs. This reduces redundant tool calls, saves computation time (especially for large solutions), and enables richer multi-step analysis workflows where the LLM builds on previous results.

**Independent Test**: Can be tested by calling a tool to generate a diagram, then listing resources, reading the resource by URI, and verifying the content matches the tool output.

**Acceptance Scenarios**:

1. **Given** the MCP server is running with no prior analyses, **When** a client lists resources, **Then** a static "welcome" resource is returned explaining ProjGraph's capabilities and available tools.
2. **Given** a user has called `get_project_graph` for a solution, **When** the client lists resources, **Then** a new resource appears with a URI identifying the generated diagram, its type, and the analyzed path.
3. **Given** a diagram resource exists, **When** the client reads that resource by URI, **Then** the full Mermaid diagram content is returned.
4. **Given** the user re-runs an analysis on the same path with the same parameters, **When** the tool completes, **Then** the existing resource is updated (not duplicated) and a resource-updated notification is sent to the client.
5. **Given** the server is restarted, **When** the client lists resources, **Then** only the static welcome resource is present (cached diagrams are session-scoped, not persisted).

---

### User Story 3 - Workspace-Aware Root Discovery (Priority: P3)

An AI assistant user opens a .NET project in their IDE and starts the MCP server. Instead of needing to provide absolute paths for every analysis request, the server discovers the workspace roots from the client and can resolve relative paths or auto-discover solution files within those roots.

**Why this priority**: Root discovery improves usability by eliminating the need for absolute paths in every request. It enables the server to find solution files, project files, and C# source files relative to the user's workspace — a natural expectation for IDE-integrated tools.

**Independent Test**: Can be tested by configuring a client with declared roots, connecting to the server, and verifying the server can request and use those roots to resolve relative file paths.

**Acceptance Scenarios**:

1. **Given** a client that supports the Roots capability connects to the server, **When** a tool is called with a relative path (e.g., `"MySolution.slnx"`), **Then** the server resolves it against the client's declared roots and proceeds with the analysis.
2. **Given** a client with multiple declared roots, **When** a relative path matches files in multiple roots, **Then** the server returns a clear error listing the ambiguous matches and asks the user to specify.
3. **Given** the client sends a `roots/list_changed` notification, **When** the server receives it, **Then** it updates its cached root list for subsequent path resolutions.
4. **Given** a client that does NOT support the Roots capability, **When** a tool is called with a relative path, **Then** the server returns a clear error explaining that an absolute path is required because the client does not support root discovery.

---

### User Story 4 - Progress Notifications for Long Analyses (Priority: P4)

A user requests an analysis of a large solution (many projects, large class hierarchies, or complex EF models). Instead of waiting with no feedback, the user sees progress updates in their AI assistant as the analysis proceeds through its stages (parsing, analyzing, rendering).

**Why this priority**: Progress feedback improves perceived performance and user confidence. While not required for functionality, it meaningfully improves the experience for large codebases where analysis may take several seconds.

**Independent Test**: Can be tested by triggering a tool call and observing that progress notification tokens are emitted at key stages of the analysis pipeline.

**Acceptance Scenarios**:

1. **Given** a user calls `get_project_graph` on a large solution, **When** the analysis proceeds, **Then** the server sends progress notifications at stages: "Parsing solution file", "Building dependency graph", "Rendering diagram".
2. **Given** a user calls `get_class_diagram` on a directory with many files, **When** discovery and analysis proceed, **Then** the server sends progress notifications including "Discovering files (N found)", "Analyzing types", "Rendering diagram".
3. **Given** a user calls any tool, **When** the operation completes quickly (under 1 second), **Then** progress notifications are still sent but may arrive close together; the user experience is not degraded.

---

### Edge Cases

- What happens when a prompt is requested with an invalid or non-existent file path argument? The prompt should still resolve successfully (prompts provide *instructions*, not *results*); the LLM will encounter the error when it follows the instructions and calls the tool.
- What happens when the client does not support the Roots capability? The server gracefully falls back to requiring absolute paths; no errors are thrown at startup.
- What happens when a cached resource's underlying file is modified on disk? The resource is not automatically invalidated — it represents the output at the time of generation. The user must re-run the tool to get updated content.
- What happens when the server accumulates many cached diagram resources? A reasonable upper bound (e.g., 50 resources) is enforced; oldest resources are evicted when the limit is reached.
- What happens when progress notifications are sent but the client does not support them? The notifications are silently dropped per the MCP specification; the tool call still succeeds.

## Requirements *(mandatory)*

### Functional Requirements

**Prompts (P1)**

- **FR-001**: Server MUST expose at least four prompt templates: Architecture Review, Dependency Analysis, Database Schema Review, and Class Structure Review.
- **FR-002**: Each prompt MUST declare its required and optional arguments with descriptions, so clients can present them to users.
- **FR-003**: Each prompt MUST return a sequence of structured messages that guide an LLM through a multi-step analysis workflow using existing ProjGraph tools.
- **FR-004**: Prompts MUST be discoverable via the standard MCP `prompts/list` method.
- **FR-005**: The Architecture Review prompt MUST accept a solution path argument and produce instructions to call `get_project_graph` and `get_project_stats`.
- **FR-006**: The Dependency Analysis prompt MUST accept a solution path and produce instructions to analyze dependency depth, hotspot projects, and circular dependencies.
- **FR-007**: The Database Schema Review prompt MUST accept a DbContext/ModelSnapshot path and optionally a context name, producing instructions to call `get_erd` and analyze the schema.
- **FR-008**: The Class Structure Review prompt MUST accept a file or directory path, producing instructions to call `get_class_diagram` with appropriate analysis options.

**Resources (P2)**

- **FR-009**: Server MUST expose a static welcome resource that describes ProjGraph's capabilities.
- **FR-010**: Server MUST expose generated diagram outputs as addressable resources with unique URIs.
- **FR-011**: Each diagram resource MUST include metadata: the analysis type (graph, class, erd, stats), the analyzed path, and generation timestamp.
- **FR-012**: When a tool generates a diagram for a path+parameters combination that already has a cached resource, the existing resource MUST be updated rather than creating a duplicate.
- **FR-013**: Server MUST send a `resources/list_changed` notification when a new resource is created.
- **FR-014**: Server MUST send a `resources/updated` notification when an existing resource is updated.
- **FR-015**: Cached diagram resources MUST be session-scoped (not persisted to disk).
- **FR-016**: Server MUST enforce a maximum number of cached resources (upper bound) and evict the oldest when the limit is reached.

**Roots (P3)**

- **FR-017**: Server MUST request roots from the client when the client declares the Roots capability.
- **FR-018**: Server MUST listen for `roots/list_changed` notifications and update its cached root list accordingly.
- **FR-019**: When a tool receives a relative path, the server MUST attempt to resolve it against known roots.
- **FR-020**: When a relative path is ambiguous (matches in multiple roots), the server MUST return a descriptive error listing the matches.
- **FR-021**: When the client does not support Roots and a relative path is provided, the server MUST return a clear error explaining that an absolute path is required.

**Progress Notifications (P4)**

- **FR-022**: Each tool MUST emit progress notifications at key analysis stages (parsing, analyzing/building, rendering).
- **FR-023**: Progress notifications MUST include a human-readable description of the current stage.
- **FR-024**: Progress notifications MUST NOT block or delay the tool execution.

### Key Entities

- **Prompt Template**: A named, parameterized analysis workflow. Has a name, description, declared arguments, and produces a sequence of messages. Stateless — does not depend on prior analyses.
- **Diagram Resource**: A cached output from a tool invocation. Has a unique URI, content (Mermaid diagram or JSON stats), metadata (analysis type, source path, timestamp), and a session-scoped lifecycle.
- **Root**: A workspace directory declared by the MCP client. Has a URI and optional name. Used for relative path resolution.
- **Resource Cache**: An in-memory collection of diagram resources with a bounded capacity and LRU eviction policy.

### MCP Primitive Interfaces

**Prompts:**

- **Prompt Name**: `architecture_review`
- **Description**: Guides an LLM through a comprehensive architecture review of a .NET solution, including dependency analysis and project metrics.
- **Arguments**:
  - `path`: (string, required) Path to the solution file (.sln, .slnx, or .csproj)

- **Prompt Name**: `dependency_analysis`
- **Description**: Guides an LLM to analyze dependency depth, identify hotspot projects, and detect circular dependencies.
- **Arguments**:
  - `path`: (string, required) Path to the solution file
  - `topN`: (number, optional) Number of top hotspot projects to highlight (default: 5)

- **Prompt Name**: `database_schema_review`
- **Description**: Guides an LLM through reviewing an Entity Framework Core database schema for design issues and improvements.
- **Arguments**:
  - `path`: (string, required) Path to a DbContext or ModelSnapshot .cs file
  - `contextName`: (string, optional) Specific DbContext or ModelSnapshot class name

- **Prompt Name**: `class_structure_review`
- **Description**: Guides an LLM through reviewing class hierarchies, inheritance patterns, and design structure in C# code.
- **Arguments**:
  - `path`: (string, required) Path to a .cs file or directory to analyze

**Resources:**

- **Resource URI**: `projgraph://welcome`
- **Description**: Static resource describing ProjGraph capabilities and available tools.
- **MIME Type**: text/plain

- **Resource URI Template**: `projgraph://diagrams/{analysisType}/{encodedPath}`
- **Description**: Cached diagram output from a previous tool invocation.
- **MIME Type**: text/plain (Mermaid) or application/json (stats)

## Assumptions

- The MCP C# SDK (v1.0.0) supports all required primitives: `[McpServerPromptType]`, `[McpServerResourceType]`, `WithPrompts<>()`, `WithResources<>()`, `McpServer.RequestRootsAsync()`, and `IProgress<ProgressNotificationValue>`.
- Clients that do not support optional capabilities (Roots, progress) will gracefully degrade — no server-side errors.
- Diagram resources are purely session-scoped (in-memory); persistence across server restarts is out of scope.
- The existing 4 tools (`get_project_graph`, `get_class_diagram`, `get_erd`, `get_project_stats`) remain unchanged in their external signatures; resource caching and progress notifications are additive behaviors.
- Prompt templates produce instructional messages for the LLM — they do not execute tools directly.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Clients can discover and list all prompt templates via the standard MCP `prompts/list` method and receive at least 4 prompts with complete metadata.
- **SC-002**: Each prompt template, when resolved with valid arguments, returns a well-structured message sequence that references the correct ProjGraph tools by name.
- **SC-003**: After a tool generates a diagram, a corresponding resource is immediately available via `resources/list` and readable via `resources/read`.
- **SC-004**: Diagram resources are de-duplicated — repeated analyses of the same path and parameters produce exactly one resource entry (updated, not duplicated).
- **SC-005**: When a client supports Roots, relative paths provided to tools are resolved successfully against declared roots without user intervention.
- **SC-006**: Progress notifications are emitted during tool execution, providing at least 2 stage updates per tool call.
- **SC-007**: All new primitives are covered by contract tests (verifying MCP attributes and signatures) and integration tests (verifying behavior).
- **SC-008**: Existing tool behavior is fully preserved — no regressions in current functionality.
