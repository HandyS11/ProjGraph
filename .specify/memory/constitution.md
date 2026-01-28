<!--
Sync Impact Report:
- Version change: N/A -> 1.0.0
- List of modified principles:
  - Added: I. Modern .NET 10 Baseline
  - Added: II. MCP Native Interoperability
  - Added: III. Library-First Core
  - Added: IV. Absolute Testing Requirement
  - Added: V. Traceability & Semantic Stability
- Added sections: Technical Constraints, Development Workflow
- Removed sections: None
- Templates requiring updates:
  - .specify/templates/spec-template.md (⚠ pending - MCP Tool Definitions)
  - .specify/templates/plan-template.md (⚠ pending - Library-First Alignment)
  - .specify/templates/tasks-template.md (⚠ pending - Observability & MCP tasks)
- Follow-up TODOs: None
-->

# ProjGraph Constitution

## Core Principles

### I. Modern .NET 10 Baseline

All project code MUST target .NET 10.0 or higher. We leverage the latest framework features, C# innovations, and high-performance APIs. Code quality is non-negotiable: zero warnings, strict style enforcement (EditorConfig), and optimal memory management for tool execution.

### II. MCP Native Interoperability

The system is built with Model Context Protocol (MCP) as a first-class citizen. Every "Tool" functionality exposed by the system MUST be accessible via an MCP server interface. Protocol compliance and schema-perfect tool descriptions are required to ensure seamless LLM interaction.

### III. Library-First Core

Core business logic and domain models MUST reside in standalone, SDK-style libraries. The .NET CLI tool and MCP server are thin consumers of these libraries. This ensures logic is reusable, independently testable, and decoupled from the delivery mechanism.

### IV. Absolute Testing Requirement (NON-NEGOTIABLE)

No code is merged without comprehensive test coverage. Unit tests for logic, integration tests for tool workflows, and contract tests for MCP endpoints are mandatory. We follow a "test-first" mentality where specifications drive test cases before implementation begins.

### V. Semantic Stability

We adhere strictly to Semantic Versioning (SemVer) 2.0.0. Diagnostic logs must provide enough context to debug issues without access to the source.

## Technical Constraints

- **Runtime**: Windows, Linux, and macOS supported via .NET cross-platform capabilities.
- **Tooling**: Built as a .NET Global Tool for easy installation.
- **Protocol**: MCP version 1.0 specification compliance.
- **Security**: No secrets in source code; use environment variables or secure secret stores for sensitive configuration.

## Development Workflow

- **Feature branching**: All development occurs on feature branches branched from `main`.
- **Pull Requests**: MUST pass all CI build and test gates. Code reviews focus on principle compliance and architectural integrity.
- **Documentation**: All public APIs and MCP tools MUST be documented using XML comments for automated documentation generation and LLM context.

## Governance

- Amendments to this Constitution require a MAJOR version bump.
- Periodic compliance reviews are conducted after every major milestone.
- The Constitution supersedes all other documentation and development practices. Use `.specify/memory/constitution.md` as the source of truth.

**Version**: 1.0.0 | **Ratified**: 2026-01-13 | **Last Amended**: 2026-01-13
