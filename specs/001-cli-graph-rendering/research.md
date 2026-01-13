# Research: CLI Graph Rendering & MCP Hub

This document summarizes the research and technical decisions for Phase 0 of the ProjGraph project.

## 1. Project Parsing (.sln, .slnx, .csproj)

- **Decision**: `Buildalyzer` for project analysis combined with `Microsoft.Build.Construction` for solution files.
- **Rationale**:
  - `Buildalyzer` provides a robust, high-level wrapper around MSBuild that handles project loading, property evaluation, and dependency resolution without requiring a full MSBuild installation on the target environment.
  - `Microsoft.Build.Construction.SolutionFile` remains the standard for parsing legacy `.sln` files.
  - For the modern `.slnx` format, Microsoft is integrating support into the `Microsoft.Build.Solution` namespace. By using the official MSBuild construction libraries, we ensure long-term compatibility as `.slnx` matures.
- **Alternatives considered**:
  - **Microsoft.Build (Raw)**: Too low-level; requires significant boilerplate to handle environment variables and global properties.
  - **Microsoft.Build.Prediction**: Highly performant but uses static analysis that can miss dynamic dependencies or conditional references.
  - **Manual XML Parsing**: Rejected as it fails to account for MSBuild imports, properties, and complex logic (e.g., `<ProjectReference Include="..." Condition="..." />`).

## 2. CLI Graph Rendering

- **Decision**: `Spectre.Console`.
- **Rationale**:
  - It is the most mature and widely adopted library for building rich console applications in .NET.
  - Specifically, its `Tree` and `Table` widgets are perfect for representing hierarchical and relational dependency data.
  - Supports ANSI colors, emojis, and responsive layouts, which aligns with the goal of providing a "clear visual representation."
- **Alternatives considered**:
  - **System.CommandLine.Rendering**: Offers good integration with `System.CommandLine` but is less feature-rich and currently has a slower development cycle.
  - **Mermaid.js (Text Output)**: While not a rendering library itself, providing a `--format mermaid` option is highly recommended for documentation purposes, but `Spectre.Console` will be the primary interactive renderer.

## 3. MCP SDK for .NET

- **Decision**: `ModelContextProtocol` (Official C# SDK).
- **Rationale**:
  - The official SDK (available on NuGet and GitHub under `modelcontextprotocol/csharp-sdk`) is maintained in collaboration with Microsoft and Anthropic.
  - It follows standard .NET idioms, including `Microsoft.Extensions.Hosting` and `DependencyInjection` support.
  - It ensures 100% compliance with the MCP 1.0 specification.
- **Alternatives considered**:
  - **MCPSharp**: A community-driven alternative. While well-constructed, the official SDK provides better long-term stability and alignment with the protocol's evolution.
  - **Manual JSON-RPC**: Low-level and error-prone; would require significant effort to reach features like transport negotiation and tool discovery.

## 4. Handling Circular Dependencies

- **Decision**: **Tarjan's Strongly Connected Components (SCC) Algorithm**.
- **Rationale**:
  - It is an $O(V + E)$ algorithm that identifies cycles in a single pass.
  - Unlike basic DFS cycle detection, Tarjan's identifies the specific "clusters" of projects that are inter-dependent, allowing for more helpful error reporting (e.g., "Projects A, B, and C form a circular reference").
  - Highly performant even for graphs with hundreds of nodes.
- **Alternatives considered**:
  - **Simple DFS (Back-edge detection)**: Easier to implement but less informative for complex multi-node cycles.
  - **QuikGraph (Library)**: While powerful, the core dependency graph logic is simple enough that a custom implementation of Tarjan's or Kosaraju's algorithm prevents an unnecessary external dependency in `ProjGraph.Lib`.

## Summary Table

| Category | Choice | Version (Target) |
| ---------- | -------- | ------------------ |
| **Framework** | .NET 10.0 | RC/Preview |
| **Parsing** | Buildalyzer + Microsoft.Build | 10.x |
| **CLI** | Spectre.Console | 0.49+ |
| **MCP** | ModelContextProtocol | 1.x |
| **Graph Logic** | Tarjan's SCC (Custom) | Internal |
