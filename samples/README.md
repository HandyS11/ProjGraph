# ProjGraph Showcase Samples

This directory contains a collection of sample projects designed to demonstrate the full capabilities of **ProjGraph**. These samples serve as both high-performance benchmarks for the tool and a documentation reference for users.

## 🗺️ Map of Capabilities

### 🏢 Class Diagrams

Visualize complex C# hierarchies, design patterns, and entity relationships.

- **[Simple Hierarchy](./classdiagram/simple-hierarchy/README.md)**: A clean, entry-level demonstration of inheritance.
- **[Design Patterns](./classdiagram/design-patterns/README.md)**: Advanced usage of Creational, Structural, and Behavioral patterns.
- **[Complex Hierarchy](./classdiagram/complex-hierarchy/README.md)**: Deeply nested types and multi-level generic constraints.

### 🗄️ Entity Relationship Diagrams (ERD)

Generate database schemas directly from EF Core code.

- **[Simple Context](./erd/simple-context/README.md)**: Basic DbContext mapping.
- **[Complex E-commerce](./erd/complex-ecommerce/README.md)**: Large-scale model with 12+ entities, TPH, and recursive FKs.

### 🌐 Project Dependency Graphs

Visualize your solution architecture.

- **[Simple Dependencies](./visualize/simple-dependencies/README.md)**: Basic cross-project references.
- **[Modular Architecture](./visualize/modular-architecture/README.md)**: Modern workspace using `.slnx` and multi-layered projects.

---

## 🚀 Quick Start

1. **Prerequisites**: Ensure you have [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed.
2. **Build Tool**: Build the ProjGraph CLI from the repository root:

   ```bash
   dotnet build src/ProjGraph.Cli/ProjGraph.Cli.csproj
   ```

3. **Run a Sample**: Follow the instructions in any sample subdirectory's `README.md`.

## ⚒️ Build and Maintenance

To ensure all samples remain buildable and their snapshots up-to-date, use the following tools:

### Consolidated Build

Validate the integrity of all samples at once:

```bash
dotnet build ./samples/Samples.slnx
```

### Automatic Snapshot Regeneration

Regenerate all `snapshots/*.mmd` files from the latest source code to ensure documentation stays in sync:

**Windows (PowerShell):**

```powershell
./samples/regenerate-samples.ps1
```

**Linux / macOS (Bash):**

```bash
chmod +x ./samples/regenerate-samples.sh
./samples/regenerate-samples.sh
```

### 💡 Edge Cases & Troubleshooting

- **Missing Solution Files**: ProjGraph CLI automatically discovers project roots, but providing the `.slnx` path directly is always faster and more reliable.
- **Ambiguous Contexts**: If a file contains multiple `DbContext` classes, use the `--context` flag to specify which one to render.
- **Deep Hierarchies**: For multi-level inheritance, use `--depth` (e.g., `--depth 5`) to capture the full branch.
- **Output Redirection**: The CLI tool outputs text to `stdout`. Use shell redirection (`>`) to save diagrams to files.
