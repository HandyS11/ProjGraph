# Feature Specification: 006-docfx-documentation

**Feature Branch**: `006-docfx-documentation`
**Created**: February 19, 2026
**Status**: Draft
**Input**: User description: "I want to create the documentation of the app using the docfx utilitary. This doc will be publish on github page. I want this doc to me exhaustive and reuse existing readme"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Maintainer Automated Publishing (Priority: P1)

As a repository maintainer, I want the documentation to be automatically rebuilt and published to GitHub Pages whenever changes are merged into the `main` branch, ensuring the online docs are always current.

**Why this priority**: Continuous delivery of documentation is critical for keeping users and developers informed without manual effort.

**Independent Test**: Push a change to a README or an XML comment in code, and verify that the GitHub Pages site updates within a few minutes.

**Acceptance Scenarios**:

1. **Given** a merge to the `main` branch, **When** the GitHub Action completes successfully, **Then** the updated documentation is visible at the GitHub Pages URL.
2. **Given** a failed build (e.g., broken links), **When** the GitHub Action runs, **Then** the process fails and the previous version of the site remains live.

---

### User Story 2 - Developer Local Preview (Priority: P1)

As a developer contributing to the project, I want to build and view the documentation locally before pushing my changes, so I can check for formatting errors or missing API details.

**Why this priority**: Fast feedback loop for documentation changes prevents broken or poorly formatted docs from reaching production.

**Independent Test**: Run a local script `doc-serve.ps1` (or `doc-serve.sh`) that triggers DocFX build and serves it locally, then verify the site opens in a browser.

**Acceptance Scenarios**:

1. **Given** a local development environment, **When** I run the `doc-serve` utility, **Then** a local web server starts and I can browse the site.
2. **Given** a modification in a project's README, **When** I trigger a local rebuild, **Then** the browser shows the updated content immediately (or upon refresh).

---

### User Story 3 - Consumer API Reference (Priority: P2)

As a developer using the ProjGraph libraries, I want an exhaustive API reference that details all public classes, methods, and properties, including those from the Core and specialized Lib projects.

**Why this priority**: High-quality API documentation is essential for library adoption and correct usage.

**Independent Test**: Navigate to the API section of the site and verify that internal-sounding projects like `ProjGraph.Lib.ClassDiagram` are fully indexed.

**Acceptance Scenarios**:

1. **Given** the published site, **When** I navigate to the "API" section, **Then** I see a hierarchical list of namespaces and types.
2. **Given** a specific class, **When** I view its page, **Then** I see descriptions pulled from its `<summary>` tags in the source code.

---

### Edge Cases

- **Broken Internal Links**: How does the build process handle links between Markdown files that have moved or been renamed? (Build should fail or log a critical warning).
- **Missing XML Documentation**: How should the API reference present classes or methods that lack `<summary>` tags? (Display a placeholder noting the lack of documentation rather than leaving it blank).
- **Diagram Rendering Failures**: If a Mermaid diagram has syntax errors, how should the documentation site display it? (Display the raw Mermaid code block with an error message instead of an empty box).
- **Concurrent Builds**: How should the GitHub Actions workflow handle multiple pushes to `main` in rapid succession? (Cancel outdated runs to save resources and ensure the final state is correct).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST use **DocFX** as the core engine for generating the documentation site.
- **FR-002**: System MUST include **API documentation** for all public projects in the `src/` directory, generated from XML documentation comments, focusing on the **Public API**.
- **FR-003**: System MUST identify and aggregate **README.md** files from the root, `src/`, and `samples/` directories into a structured conceptual section.
- **FR-004**: System MUST support rendering **Mermaid diagrams** embedded in Markdown files using **client-side rendering (Mermaid.js)**.
- **FR-005**: System MUST prioritize **user-oriented documentation** (CLI and MCP usage). Existing technical specifications from the `specs/` directory will be included as secondary material with high-level summaries for developers.
- **FR-006**: System MUST automate the build and deployment process via **GitHub Actions**, targeting **GitHub Pages**.
- **FR-007**: System MUST provide a search functionality that covers both API and conceptual content.
- **FR-008**: System MUST provide a local script to build and serve the documentation for development purposes.

### Assumptions & Constraints

- **DocFX Version**: The implementation will use the latest stable version of DocFX.
- **GitHub Pages Configuration**: The site will be hosted at the repository's default GitHub Pages URL.
- **Markdown Flavors**: Standard DocFX (DocFX-flavored Markdown) will be used, with the addition of Mermaid support.
- **Developer Content**: While the focus is on usage, a "Developer Guide" section will be established to house existing architecture notes and specs without overwhelming the end-user documentation.

### Key Entities *(include if feature involves data)*

- **Documentation Site**: The final static website hosted on GitHub Pages.
- **API Reference**: The portion of the site containing technical details extracted from C# code.
- **Conceptual Guides**: The portion of the site derived from Markdown files (READMEs, Specs, User Guides).
- **docfx.json**: The configuration file defining how the site is structured and built.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The documentation site is reachable at the official GitHub Pages URL for the repository.
- **SC-002**: 100% of public namespaces in `src/` have corresponding entries in the API reference.
- **SC-003**: All diagrams defined in Mermaid syntax are rendered as visual charts on the site.
- **SC-004**: Navigation menu allows reaching any README file within a maximum of 3 clicks from the landing page.
- **SC-005**: The total build and deployment time in GitHub Actions is under 10 minutes.
