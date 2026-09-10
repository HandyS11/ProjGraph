---
title: Output formats
---

# Output formats

Every ProjGraph command writes text to stdout. What that text *is* — a Mermaid
diagram, an ASCII tree, or a fenced Markdown block — depends on the command, the
`--format` flag, and whether you asked for a file. This page explains the
choices so you can pick one deliberately.

## The three renderings

| Rendering | Available on | Shows | Renders as a picture in |
| --- | --- | --- | --- |
| `mermaid` | `visualize`, and always for `erd` / `classdiagram` | The whole graph, as diagram source | GitHub, GitLab, this site, VS Code |
| `tree` | `visualize` | Each root expanded recursively through its whole chain | Nowhere — it is already the picture |
| `flat` | `visualize` | Every project once, with its direct references only | Nowhere — it is already the picture |

`erd` and `classdiagram` have no `--format` flag. Both always emit Mermaid,
because an entity relationship diagram and a class diagram have no useful
plain-text form.

## Choosing between them

`tree` and `flat` answer different questions, and the difference is not
cosmetic.

**Tracing what a project pulls in transitively? Use `tree`.** It starts from the
projects nothing else references and expands each chain to its full depth. A
project that appears more than once is expanded on first sight and marked
`(see above)` afterwards, so the output stays finite:

```bash
projgraph visualize ./MySolution.slnx --format tree
```

```text
🧪 ProjGraph.Tests.Contract
└── ProjGraph.Mcp
    └── ProjGraph.Lib
        ├── ProjGraph.Lib.ClassDiagram
        │   └── ProjGraph.Lib.Core
        │       └── ProjGraph.Core
        └── ProjGraph.Lib.Dependencies
            └── ProjGraph.Lib.Core (see above)
```

**Auditing what each project references directly? Use `flat`.** Every project is
listed exactly once, grouped by type, with only its immediate references. No
chain is followed, so nothing is repeated and nothing is elided:

```bash
projgraph visualize ./MySolution.slnx --format flat
```

```text
🔷 ProjGraph.Lib
├── → ProjGraph.Lib.ClassDiagram
├── → ProjGraph.Lib.Dependencies
└── → ProjGraph.Lib.EntityFramework
🔷 ProjGraph.Lib.ClassDiagram
└── → ProjGraph.Lib.Core
```

**Committing to docs? Use `mermaid`,** which is the default for `visualize`:

```bash
projgraph visualize ./MySolution.slnx
```

The emoji mark the project type: 🔷 library, 🚀 executable, 🧪 test project.

## Fenced or raw: what `--output` changes

This is the part that surprises people. The same command produces two different
things depending on where it is going:

- **To stdout**, Mermaid arrives wrapped in a `mermaid` code fence, so you can
  paste it straight into a Markdown file and have it render.
- **To a file via `--output`**, the extension decides. Only `.mmd` suppresses
  the fence — a `.mmd` file is already understood to contain nothing but a
  diagram. Every other extension, `.md` included, keeps it.

```bash
# Fenced Markdown, ready to commit into a docs page
projgraph visualize ./MySolution.slnx --output docs/dependencies.md

# Raw Mermaid, for a tool that parses .mmd directly
projgraph visualize ./MySolution.slnx --output docs/dependencies.mmd
```

Prefer `--output` over a shell redirect. It creates missing directories for you,
and it is what selects the right fencing.

## Piping the output

`mermaid` keeps stdout clean: progress messages go to stderr, so a redirect
captures the diagram and nothing else.

`tree` and `flat` are written for a human at a terminal, and their progress
header goes to stdout along with the graph. If you redirect them, that header
lands in your file too. Use `--output`, which writes the rendered graph and
leaves the progress message on the terminal where it belongs:

```bash
# Captures the header as well - probably not what you want
projgraph visualize ./MySolution.slnx --format tree > deps.txt

# Captures the graph, with its title block
projgraph visualize ./MySolution.slnx --format tree --output deps.txt
```

The diagram's own title block is part of the render, so it is written to the
file either way. Drop it with `--show-title false`.

## Titles

Every diagram carries a title block naming what was analysed. Drop it when you
are embedding the diagram under a heading that already says the same thing:

```bash
projgraph erd ./Data/LibraryContext.cs --show-title false
```

`--show-title` is available on `visualize`, `erd`, and `classdiagram`.

## What `stats` does instead

`stats` is the exception: it reports numbers, not a graph, so it has no
`--format` and no `--output`. It prints a formatted table to the terminal and
nothing else.

```bash
projgraph stats ./MySolution.slnx --top 10
```

## Rendering Mermaid on this site

The [showcase](../../samples/README.md) pages embed their Mermaid output
directly, so you can see exactly what each command produces without running it.
That is the same text the CLI writes — the pages are regenerated from the tool
whenever it changes.
