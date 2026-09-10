---
title: Troubleshooting
---

# Troubleshooting

ProjGraph reads source files rather than compiled assemblies, so when a diagram
comes back empty it is almost always because it could not find the thing you
meant, not because your code is wrong. This page maps the messages you might see
onto what to do about them.

## `projgraph` command not found

The tool installed, but your shell has not picked up the .NET global tools
directory. Open a new terminal first — that resolves it most of the time.

If it persists, add the tools directory to your `PATH`:

```bash
# Linux and macOS
export PATH="$PATH:$HOME/.dotnet/tools"

# Windows (PowerShell)
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

Add the same line to your shell profile to make it stick.

## `File must be a .sln, .slnx, or .csproj file.`

`visualize` and `stats` analyse a solution or a project, not a directory and not
a source file. Point them at the file itself:

```bash
projgraph visualize ./MySolution.slnx
projgraph stats ./src/MyApp/MyApp.csproj
```

## `No DbContext or ModelSnapshot .cs file found.`

`erd` was given no path, and found nothing to analyse in the current directory.
It looks for files whose names end in `DbContext.cs` or that contain a model
snapshot.

Give it the path explicitly:

```bash
projgraph erd ./Data/LibraryContext.cs
```

If your context does not follow the `*DbContext.cs` naming convention, name it
directly with `--context`:

```bash
projgraph erd ./Data/Persistence.cs --context LibraryContext
```

## The ERD is empty, or entities are missing

ProjGraph discovers entities from the `DbSet<T>` properties on your context, so
an entity that is not exposed as a `DbSet` will not appear unless something else
references it.

Work through these in order:

1. **Is the context `public`?** A non-public class is skipped.
2. **Are the entities reachable?** An entity registered only through
   `modelBuilder.Entity<T>()` in `OnModelCreating`, with no `DbSet`, is found
   only if ProjGraph can resolve the type.
3. **Do the entity types live in another project?** ProjGraph searches the
   workspace for them, starting from the nearest `.sln`, `.slnx`, or `.csproj`.
   If your context and your entities are in unrelated directories with no
   solution file above them, it has nothing to search.

If the model is already migrated, the snapshot is the more reliable source,
because EF has already resolved the model in full:

```bash
projgraph erd ./Migrations/LibraryContextModelSnapshot.cs
```

## The class diagram shows one class and nothing else

That is the default. Base types and dependencies are opt-in, because following
them across a large workspace is expensive:

```bash
projgraph classdiagram ./Models/Book.cs --inheritance --dependencies
```

Still missing types? Discovery only follows one level by default. Raise it:

```bash
projgraph classdiagram ./Models/Book.cs -i -d --depth 5
```

## `stats` reports zero projects

The solution file parsed, but none of the projects it lists could be read.
Usually one of:

- The `.csproj` files it references have moved or been deleted.
- The solution genuinely contains no projects.
- You pointed at a solution file in a different directory tree, so the relative
  project paths inside it no longer resolve.

Confirm the paths inside the solution file match what is on disk.

## Parsing fails on newer C# syntax

ProjGraph parses with Roslyn, and the version of Roslyn it ships with sets the
syntax it understands. If you use a language feature newer than the tool,
parsing that file can fail.

Update the tool:

```bash
dotnet tool update -g ProjGraph.Cli
```

## The MCP server returns nothing, or the client will not start it

The server speaks JSON-RPC over stdio, which means **anything else written to
stdout corrupts the protocol**. If you are running a locally built copy, make
sure nothing in your build writes to stdout on startup.

Check these in order:

1. **Is `dnx` available?** Run `dnx --help`. It ships with the .NET 10 SDK.
2. **Is the version in your config real?** Replace `x.x.x` with a published
   version from [NuGet](https://www.nuget.org/packages/ProjGraph.Mcp).
3. **Did the client restart?** Most MCP clients only read their server
   configuration at startup.
4. **Can the server see your files?** The tools take paths, and the server can
   only read what its process can reach.

## Still stuck

Open an issue at
[github.com/HandyS11/ProjGraph/issues](https://github.com/HandyS11/ProjGraph/issues)
with the command you ran and the output you got.
