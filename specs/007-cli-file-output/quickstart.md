# Quickstart: File Output (`--output` flag)

## Usage: CLI Commands

Save diagrams directly to disk using the `-o|--output` flag.

### Save Mermaid Diagram to `.mmd` (No code fence)

```bash
projgraph visualize ./MySolution.sln -o diagram.mmd
```

### Save Mermaid Diagram to `.md` (Wrapped in code fence)

```bash
projgraph visualize ./MySolution.sln -o README.md
```

### ERD and Class Diagrams

```bash
projgraph erd ./Data/AppDbContext.cs -o erd.mmd
projgraph classdiagram ./Models/User.cs -o class.mmd
```

## Troubleshooting

If the output file cannot be written (permission denied or invalid path), the CLI will report a clear error message and return a non-zero exit code.
No diagrams will be written to `stdout` when the `--output` flag is used.
Success is confirmed with a "Saved to <path>" message in the informational output (stderr-like).
