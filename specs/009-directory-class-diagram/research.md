# Research: Folder/Directory Scanning for classdiagram

## Decision: Recursive Directory Scanning with Exclusion

**Decision**: Use a manual breadth-first search (BFS) traversal of the directory tree.
**Rationale**: While `Directory.EnumerateFiles` with `SearchOption.AllDirectories` is simple, it doesn't allow skipping entire subtrees (like `bin/`, `obj/`, `.git/`). By manually traversing the tree using a `Queue<string>`, we can check each directory against `DirectoryFilters.ShouldSkipDirectory` before entering it, ensuring optimal performance and avoiding irrelevant files.
**Alternatives considered**:

- `Directory.EnumerateFiles(..., AllDirectories)`: Rejected because it processes all files before filtering, leading to unnecessary I/O and potential issues with deep or protected folders like `.git`.

## Decision: Unified Roslyn Compilation for Multiple Files

**Decision**: Create a `DiscoverCsFilesUseCase` to find all relevant files and then pass the entire list of `SyntaxTree`s to a single `CSharpCompilation`.
**Service Changes**:

- `IClassAnalysisService` will be updated with `AnalyzeDirectoryAsync(string directoryPath, AnalysisOptions? options)`.
- A new `AnalyzeDirectoryUseCase` will be introduced to orchestrate the discovery and analysis across multiple files.
**Rationale**: Providing all syntax trees to one compilation allows Roslyn to resolve cross-file references (like partial classes or internal relationships) much faster and more accurately than analyzing each file in isolation.
**Alternatives considered**:
- Analyzing each file separately: Rejected as it would miss relationships between files unless they were explicitly "discovered" via the complex dependency/inheritance discovery logic. Analyzing the whole folder as a "unit" is the primary goal of the feature.

## Decision: Warning Threshold for Large Directories

**Decision**: Implement a threshold of 50 `.cs` files.
**Rationale**: In the CLI and MCP context, a diagram with >50 classes is rarely useful as it becomes a "hairball". A warning helps the user understand why the output might be overwhelming or slow.
**Threshold**: 50 files.
