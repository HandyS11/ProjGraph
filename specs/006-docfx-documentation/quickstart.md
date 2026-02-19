# QuickStart Guide: 006-docfx-documentation

This guide describes how to build and view the ProjGraph documentation locally.

## Prerequisites

- **.NET SDK 10.0+**
- **DocFX .NET Tool**: Install globally via:

  ```powershell
  dotnet tool install -g docfx
  ```

---

## 1. Build and View the Documentation

To build and serve the site locally, run the following command from the root of the repository:

```powershell
docfx docfx/docfx.json --serve
```

Once started, the documentation will be accessible at: `http://localhost:8080`

---

## 2. Generate Metadata Only

If you only want to update the API metadata (from source code) without regenerating the HTML site:

```powershell
docfx metadata docfx/docfx.json
```

This will update the YAML files in the `docfx/api` directory based on the latest XML comments in the source projects.

---

## 3. Verify Local Build

After running the serve command, verify that:

- The **Home** page loads correctly.
- **Top-level navigation** allows you to switch between "CLI Guide", "MCP Guide", and "API Reference".
- **Mermaid diagrams** in the "Samples" section render as charts, not raw code blocks.
- **Search bar** successfully finds keywords from both conceptual and API sections.

---

## 4. GitHub Actions Deployment

The documentation starts building automatically on every PR to `main` and deploys on merge. To view the deployment status, check the **Actions** tab in the GitHub repository and look for the `docs-publish` workflow.
