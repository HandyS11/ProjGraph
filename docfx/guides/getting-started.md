---
title: Getting started
---

# Getting started

Install ProjGraph, point it at code you already have, and read the diagram it
gives back. This page takes you through the first run for both the CLI and the
MCP server. For the full set of flags on each command, see the
[CLI reference](../../src/ProjGraph.Cli/README.md).

## Before you start

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) or later.
Check what you have:

```bash
dotnet --version
```

ProjGraph reads source files directly. You do not need to build the project you
are analysing, run a database, or apply migrations first.

## Install the CLI

```bash
dotnet tool install -g ProjGraph.Cli
```

Confirm the tool is on your path. This lists the four commands and their
options:

```bash
projgraph --help
```

If the command is not found, your shell has not picked up the global tools
directory yet. Open a new terminal, or see
[Troubleshooting](troubleshooting.md#projgraph-command-not-found).

## Draw your first diagram

Start with the solution you are standing in, because it needs no arguments
beyond a path:

```bash
projgraph visualize ./MySolution.slnx
```

That prints a Mermaid graph to stdout, wrapped in a fenced code block ready to
paste into Markdown. To read it in the terminal instead, ask for the tree:

```bash
projgraph visualize ./MySolution.slnx --format tree
```

Both render the same graph. [Output formats](output-formats.md) covers when to
reach for which.

## Draw the rest

Each command takes a path and writes to stdout, so you can redirect it anywhere:

```bash
# Database schema, from an EF Core DbContext
projgraph erd ./Data/LibraryContext.cs

# One class, with its base types and dependencies
projgraph classdiagram ./Models/Book.cs --inheritance --dependencies

# Architectural metrics for the whole solution
projgraph stats ./MySolution.slnx
```

Use `--output` rather than a shell redirect when you want the file written for
you, including any missing directories:

```bash
projgraph erd ./Data/LibraryContext.cs --output docs/database-schema.md
```

## Use it from an AI assistant

The MCP server exposes the same four analyses as tools an assistant can call,
so you can ask about your architecture instead of remembering command names.

Add the server to your MCP client's configuration. Replace `x.x.x` with the
current version from
[NuGet](https://www.nuget.org/packages/ProjGraph.Mcp):

```json
{
  "servers": {
    "ProjGraph.Mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": ["ProjGraph.Mcp@x.x.x", "--yes"]
    }
  }
}
```

Restart the client, then ask it something that needs the tools:

```text
Show me the entity relationships in my DbContext.
Which projects in this solution are referenced the most?
```

The [MCP reference](../../src/ProjGraph.Mcp/README.md) documents every tool,
prompt, and resource the server provides.

## Where to go next

- [Output formats](output-formats.md) — pick between Mermaid, tree, and Markdown.
- [Showcase](../../samples/README.md) — real commands and the diagrams they produced.
- [Troubleshooting](troubleshooting.md) — when a diagram comes back empty.
