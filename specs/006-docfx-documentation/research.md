# Research: 006-docfx-documentation

This document outlines the technical decisions and research findings for the DocFX documentation implementation.

## 1. DocFX Mermaid.js Integration

### Decision

Use client-side rendering with a customized DocFX template by including the Mermaid.js library and a small initialization script in the post-processing phase.

### Rationale

- **Simplicity**: No need for complex build-time headless browser plugins (like puppeteer).
- **Interactivity**: Client-side rendering allows for zooming, panning, and dynamic theme switching if the DocFX skin supports it.
- **Maintainability**: Directly uses the latest Mermaid.js version from a CDN.

### Alternatives considered

- **DocFX.Plugins.Mermaid**: Existing build-time plugin. Rejected because it's often outdated and adds complexity to the CI/CD environment (requiring Node.js and Chromium).

---

## 2. README-First Documentation Strategy

### Decision

Adopt a "Single Source of Truth" approach where project-specific documentation lives strictly as enhanced `README.md` files within the respective `src/` subdirectories. DocFX will map these files directly into the site structure.

### Rationale

- **Zero Duplication**: Improvements made for the NuGet packages (which use these READMEs) automatically reflect on the documentation site.
- **Improved Discoverability**: Developers see documentation right next to the code they are working on.
- **Contextual Accuracy**: Readmes like `src/ProjGraph.Mcp/README.md` serve both as a local guide and a high-level overview on the site.

### Alternatives considered

- **Dedicated Articles Folder**: Creating new `.md` files in a centralized `docfx/articles/` folder. Rejected to avoid content fragmentation and maintenance overhead.

---

## 3. DocFX Search Configuration

### Decision

Enable the built-in `docfx.search.local` plugin and configure a unified index across the `api/` and `articles/` sections.

### Rationale

- The local search plugin is highly performant for a site of this scale.
- Provides an integrated search bar in the navbar out of the box.

---

## 4. GitHub Actions Deployment Workflow

### Decision

Use the official `actions/upload-pages-artifact` and `actions/deploy-pages` actions. The build will run DocFX via the `.NET tool` command.

### Rationale

- Standard, secure method for deploying to GitHub Pages.
- Supports fine-grained access control and deployment environments.

### Research Tasks (Internal)

- [ ] Verify if `.NET 10` is supported by the `docfx` global tool.
- [ ] Confirm the URL structure for GitHub Pages (typically `https://OWNER.github.io/REPO/`).
