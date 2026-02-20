# Implementation Tasks: 006-docfx-documentation

**Feature Name**: 006-docfx-documentation
**Implementation Strategy**: MVP first, focusing on local build and API extraction, followed by GitHub Actions automation.

## Phase 1: Setup

- [x] T001 Initialize documentation directory structure in `docfx/`
- [x] T002 Create initial configuration file in `docfx/docfx.json`
- [x] T003 Create landing page by mapping root README in `docfx/index.md`

## Phase 2: Foundational

- [x] T004 [P] Implement custom Mermaid.js template in `docfx/templates/custom/layout/_master.tmpl`
- [x] T005 Configure global navigation hierarchy in `docfx/toc.yml`

## Phase 3: User Story 2 - Developer Local Preview (Priority: P1)

**Goal**: Enable developers to build and view documentation locally using the `doc-serve` utility.
**Independent Test**: Run `doc-serve.ps1` and browse the site at `localhost:8080`.

- [x] T006 [US2] Create PowerShell build and serve script `doc-serve.ps1`
- [x] T007 [P] [US2] Create Shell build and serve script `doc-serve.sh`
- [x] T008 [US2] Add documentation build prerequisites to `CONTRIBUTING.md`

## Phase 4: User Story 3 - Consumer API Reference (Priority: P2)

**Goal**: Generate full API reference from source code XML comments.
**Independent Test**: Navigate to the "API" section on the local site and see detailed member descriptions.

- [x] T009 [P] [US3] Enable XML documentation generation for all project files in `src/**/*.csproj`
- [x] T010 [US3] Configure API metadata extraction in `docfx/docfx.json` for all `src/` projects
- [x] T011 [US3] Add XML documentation comments to core interfaces in `src/ProjGraph.Core/` (Focus: IGraph, INode, IEdge)
- [x] T012 [US3] Add XML documentation comments to specialized libraries in `src/ProjGraph.Lib.*/` (Focus: Extension methods and Public Converters)
- [x] T013 [US3] Add XML documentation comments to delivery projects in `src/ProjGraph.Mcp/` and `src/ProjGraph.Cli/` (Focus: Command classes and MCP Tool descriptors)

## Phase 5: User Story 1 - Maintainer Automated Publishing (Priority: P1)

**Goal**: Automate rebuilding and publishing to GitHub Pages via CI/CD.
**Independent Test**: Merge a PR to `main` and verify the live GitHub Pages site updates.

- [x] T014 [US1] Create GitHub Actions deployment workflow in `.github/workflows/docs-publish.yml`
- [x] T015 [US1] Configure DocFX build validation in CI pipeline (MUST fail on warnings/broken links per Constitution I)

## Phase 6: Content Integration & Polish

- [x] T016 [P] Enhance `src/ProjGraph.Cli/README.md` (Add: Installation, CLI Command Examples, Troubleshooting)
- [x] T017 [P] Enhance `src/ProjGraph.Mcp/README.md` (Add: MCP Setup, Tool Definitions Table, Schema Examples)
- [x] T018 Map technical specifications directory to site in `docfx/docfx.json`
- [x] T019 Finalize site-wide search configuration in `docfx/docfx.json`

## Dependencies

- Phase 2 depends on Phase 1 completion.
- Phase 3/4/5 are relatively independent but Phase 3 is best for local testing of Phase 4/5 outputs.

## Parallel Execution Examples

- US2 (T006, T007) and US3 (T009) can be worked on simultaneously.
- Content enhancements (T016, T017) can be done in parallel with infrastructure tasks.
