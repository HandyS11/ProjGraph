# Quickstart: Showcase Samples

This document provides a guide for users and developers to interact with the ProjGraph showcase samples.

## 1. Prerequisites

- **.NET 10 SDK**: [Download from official source](https://dotnet.microsoft.com/download/dotnet/10.0).
- **ProjGraph CLI**: Build the tool locally or install it via the provided NuGet package.

    ```bash
    dotnet build src/ProjGraph.Cli/ProjGraph.Cli.csproj
    ```

## 2. Exploring Samples

Navigate to the `samples/` directory to see the available categories:

- `classdiagram/`: Advanced OOP and design pattern hierarchies.
- `erd/`: Entity Framework Core database models (including the new complex e-commerce model).
- `visualize/`: Project dependency graphs across multi-project solutions.

## 3. Running a Sample

Each sample includes its own `README.md` with specific commands. Here's a general example for the **Design Patterns** sample:

```bash
# 1. Build the sample project to ensure it's valid
dotnet build samples/classdiagram/design-patterns/DesignPatterns.csproj

# 2. Generate the Mermaid diagram
projgraph classdiagram ./samples/classdiagram/design-patterns/Domain/Order.cs --inheritance --dependencies --depth 2 > ./samples/classdiagram/design-patterns/snapshots/design-patterns.mmd
```

## 4. Visual Verification

Once generated, you can view the `.mmd` files:

- **On GitHub**: Markdown files with Mermaid code blocks render automatically.
- **In VS Code**: Use the "Mermaid Editor" or "Mermaid Preview" extension.

## 5. (Internal) Regenerating All Samples

Developers can update all diagrams following a major tool update by running the internal script:

```powershell
./scripts/regenerate-samples.ps1
```

This script iterates through all standardized samples, runs the tool, and overwrites the snapshots and README sections to ensure they remain in sync with the codebase.
