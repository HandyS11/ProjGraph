# Research: File Output (`--output` flag)

## Decision: File Writing Orchestration

- **Chosen**: Logic stays in `Cli` commands but uses `IFileSystem` for execution.
- **Rationale**: Writing to a file is a transport-level concern. The commands already coordinate between analysis services and renderers. The `IFileSystem` provides abstraction for testing.
- **Alternatives Considered**:
  - Creating a separate `IOutputFileService`. Rejected as it would be too much abstraction for a simple `WriteAllTextAsync` call.

## Decision: Markdown Fencing Logic

- **Chosen**: Automatically wrap in a code fence if the output file extension is `.md` or anything other than `.mmd`.
- **Rationale**: `.mmd` is a Mermaid-specific file format which should not contain markdown fences. `.md` files are intended for embedding in markdown environments where fences are required.
- **Implementation**:
  - `visualize`: Check `settings.Output` extension.
  - Set `DiagramOptions.WrapInMarkdownFence = !outputPath.EndsWith(".mmd", StringComparison.OrdinalIgnoreCase)`.

## Best Practices: CLI File Output

- Follow standard `-o|--output` flag naming.
- Use `System.IO.File` through `IFileSystem`.
- Default to UTF-8 without BOM (modern .NET default).
- Ensure output directory exists before writing (optional but good). I'll use `IFileSystem.GetDirectoryName` and `Directory.CreateDirectory`. Wait, `IFileSystem` doesn't have `CreateDirectory`.
- Let's check `IFileSystem` again.
- I might need to add `CreateDirectory` or `GetDirectoryName` is already there.

## Dependency Check

The `IFileSystem` should be used for testing.
I'll check `ProjGraph.Tests.Integration.Cli` to see how it's tested.

- It might use a real file system or a mock.
- `ProjGraph.Cli` usually uses `PhysicalFileSystem`.
