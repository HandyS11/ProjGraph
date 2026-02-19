# Implementation Plan: 006-docfx-documentation

**Branch**: `006-docfx-documentation` | **Date**: February 19, 2026 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/006-docfx-documentation/spec.md`

## Summary

Create an exhaustive, user-oriented documentation site for ProjGraph using **DocFX**. The site will feature a comprehensive API reference for public members, aggregate existing READMEs and specialized guides from across the repository, and render Mermaid diagrams client-side. Deployment will be automated via **GitHub Actions** to **GitHub Pages**.

## Technical Context

**Language/Version**: .NET 10.0 (C# 14+) for API metadata extraction
**Primary Dependencies**: DocFX (v2.70+), Mermaid.js (v10+), GitHub Actions
**Storage**: N/A (Static site)
**Testing**: DocFX build validation (link checking), GitHub Action workflow tests
**Target Platform**: GitHub Pages (Static Hosting)
**Project Type**: Documentation Infrastructure
**Performance Goals**: Build & Deploy < 10 minutes; Page load < 2s
**Constraints**: Zero warnings; Public API only; Responsive design
**Scale/Scope**: ~10 projects, 50+ conceptual pages, 100+ API types

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I. Modern .NET 10 Baseline**: Targeting .NET 10.0+ for all metadata generation and build scripts.
- [x] **II. MCP Native Interoperability**: Documentation explicitly covers MCP tool usage and schemas.
- [x] **III. Library-First Core**: API reference highlights the library-first architecture of ProjGraph.
- [x] **IV. Absolute Testing Requirement**: CI/CD includes automated link validation and build success gates.

## Project Structure

### Documentation (this feature)

```text
specs/006-docfx-documentation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (Navigation & TOC structure)
├── quickstart.md        # Phase 1 output (How to build docs locally)
├── contracts/           # Phase 1 output (DocFX config schemas)
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

```text
docfx/                   # DocFX configuration (thin wrapper)
├── docfx.json           # Main config (mappings to /src and /specs)
├── toc.yml              # Global Site Navigation
├── index.md             # Landing Page (aggregated from root README)
└── templates/           # Custom DocFX templates for Mermaid support

src/
├── ProjGraph.Cli/README.md       # PRIMARY source for CLI Guide
├── ProjGraph.Mcp/README.md       # PRIMARY source for MCP Guide
...
```

**Structure Decision**: A minimalist `docfx/` folder serves as the generator heart. It maps content directly from the `src/` projects and `specs/` folder, ensuring documentation is maintained alongside the code components it describes.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
| ----------- | ------------ | ------------------------------------- |
| N/A | No violations | - |
