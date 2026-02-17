# Feature Specification: Showcase Samples & Reference Documentation

**Feature Branch**: `005-improve-samples`
**Created**: 2026-02-17
**Status**: Draft
**Input**: User description: "Improve the current samples/ The ones I made earlier are kind of messy. I want them to showcase what are all the capacities of the projgraph tool to be in the future use as a documentation reference for exemples. Improve theses samples and their doc to fully showcase the app with impactent exemples. Not like now"

## Clarifications

### Session 2026-02-17

- Q: Should we adopt the current CLI command names and arguments or update them to match the new sample draft? → A: Use current CLI names: `classdiagram`, `visualize`, `erd`. Fix command names and arguments in the samples to match the existing CLI and MCP documentation.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Self-Guided Discovery (Priority: P1)

As a new user, I want to explore the `samples/` directory and immediately understand what ProjGraph can do so that I can decide if it fits my project's needs.

**Why this priority**: Samples are the primary "landing page" for developers looking to integrate the tool. Clear samples reduce onboarding friction.

**Independent Test**: A user can navigate to `samples/`, read the root `README.md`, and successfully generate a diagram for any sample using the documented commands.

**Acceptance Scenarios**:

1. **Given** a fresh clone of the repo, **When** I look at `samples/README.md`, **Then** I see a categorized list of all tool capabilities (Class, ERD, Project Graph).
2. **Given** I am in a sample directory, **When** I run the `projgraph` command provided in the local `README.md`, **Then** a valid Mermaid file is generated that accurately represents that sample.

---

### User Story 2 - Real-World Performance Validation (Priority: P2)

As a technical lead, I want to see "Impactful" (complex) examples so that I can verify the tool doesn't break or produce unreadable spaghetti diagrams on real-world codebases.

**Why this priority**: Users need to know the tool scales beyond "Hello World" before committing to use it on their production systems.

**Independent Test**: The "Complex" samples contain at least 15+ entities with multi-level relationships and successfully render without overlapping labels or logical errors.

**Acceptance Scenarios**:

1. **Given** the `complex-hierarchy` sample, **When** I generate a class diagram, **Then** I see inheritance, interfaces, and associations clearly distinguished.
2. **Given** the `design-patterns` sample, **When** I generate a diagram, **Then** key architectural patterns are visually identifiable.

---

### User Story 3 - CI/CD Integration Reference (Priority: P3)

As a DevOps engineer, I want samples to be "Clean" and "Self-Contained" so that I can use them as a baseline for automated regression testing of the ProjGraph CLI.

**Why this priority**: Ensures the tool remains stable as it evolves.

**Independent Test**: `dotnet build samples/` succeeds for every project and leaves no untracked files in the workspace (properly gitignored).

**Acceptance Scenarios**:

1. **Given** a sample project, **When** I build it, **Then** it produces zero warnings and zero errors.
2. **Given** a built sample project, **When** I check `git status`, **Then** no `bin/` or `obj/` folders are listed as untracked.

---

### Edge Cases

- **Missing Solution Files**: How do samples behave if they only contain `.csproj` without a `.sln/.slnx`? (Should be documented in README).
- **Tool Versioning**: What happens when a sample uses features not yet available in the latest NuGet release but present in the local source? (Should use project references or clearly state requirements).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A root `samples/README.md` MUST be created, providing a "Map of Capabilities" linking to all sub-samples.
- **FR-002**: Every sample MUST have a standardized `README.md` with a "Quick Start" section containing exact CLI commands.
- **FR-003**: Samples MUST include a `snapshots/` folder containing the "Current State" Mermaid code as a reference.
- **FR-004**: The `erd/` samples MUST be expanded to include a `complex-ecommerce` model (10+ tables, various relationship types).
- **FR-005**: All samples MUST be stripped of `bin/` and `obj/` folders and properly excluded in `.gitignore`.
- **FR-006**: An internal "Run All Samples" script (PowerShell/Bash) MUST be provided for developers to quickly regenerate all documentation snapshots.
- **FR-007**: Every sample project MUST target the same .NET version as the main library projects for consistency.
- **FR-008**: All classes, properties, and namespaces in samples MUST use professional, domain-appropriate naming (No "Foo", "Bar", or "TestClass").

### Key Entities *(include if feature involves data)*

- **Sample Catalog**: The root `samples/README.md` containing the structure and metadata defining what each sample demonstrates.
- **Reproducer Commands**: The specific CLI strings required to generate diagrams for each sample.

## Assumptions

- **A-001**: Users have the .NET SDK installed to run the samples.
- **A-002**: The ProjGraph CLI tool will be accessible via the path or a local build for testing samples.
- **A-003**: Mermaid diagrams are the primary output format of choice for the project's documentation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Total time to generate and view a diagram from any sample directory is < 2 minutes for a new user.
- **SC-002**: All sample projects build successfully with `dotnet build` without manual configuration.
- **SC-003**: 100% of samples have a "Visual Reference" (Mermaid code) in their local documentation.
- **SC-004**: 0% of samples contain build artifacts (`bin/`, `obj/`) in the repository.
