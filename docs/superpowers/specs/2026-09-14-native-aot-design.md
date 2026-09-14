# Native AOT for the CLI and MCP server

**Date:** 2026-09-14 · **Status:** approved design, pre-implementation
**Author:** Native AOT feasibility spike (Opus 5)

## Purpose

Ship `projgraph` (`ProjGraph.Cli`) and `ProjGraph.Mcp` as Native AOT executables so each
invocation starts in tens of milliseconds instead of hundreds, uses roughly half the memory,
and needs no .NET runtime on the main platforms. Output stays **byte-identical** to the
current JIT build, and installation (`dotnet tool install`, `dnx`) doesn't change for users.

The work is delivered as three sequential PRs:

1. **Library preparation.** Removes the AOT blockers from the shared libraries. Shipping is
   unchanged and everything is covered by the existing suites.
2. **Native build and PR smoke test.** Adds AOT publish configuration, the CLI trimming fix, and a
   CI job that runs the native binaries against the JIT build on every PR.
3. **Packaging and release.** Adds RID-specific tool packages with an `any` fallback and a
   per-platform release matrix.

Each PR gets its own implementation plan.

## Current state

A throwaway spike (detached worktree at `8c96f44`, linux-x64, SDK 10.0.401) published both
entry points with `PublishAot=true`. Both publishes succeed, but the binaries fail at runtime
for four reasons:

| # | Where | Failure | Root cause |
| --- | --- | --- | --- |
| 1 | CLI startup | `CommandRuntimeException: Could not get settings type for command of type 'ProjGraph.Cli.Commands.VisualizeCommand'` | Spectre.Console.Cli reflects over command/settings types; the trimmer removes the metadata |
| 2 | MCP startup | `NotSupportedException: JsonTypeInfo metadata for type 'ProjGraph.Lib.ClassDiagram.Application.AnalysisOptions' was not provided` | Tool parameter/result types need source-generated JSON metadata under AOT |
| 3 | `CompilationFactory` | `MetadataReference.CreateFromFile("")` | BCL references are built from `Assembly.Location`, which is always empty under AOT |
| 4 | `ProjectParser` / `SlnParser` | `ArgumentException: The value cannot be an empty string. (Parameter 'path')` in `BuildEnvironmentHelper.get_Instance` | `ProjectRootElement.Open` initialises MSBuild's global `ProjectCollection`, which probes `Assembly.Location` |

With spike fixes applied (root assemblies for #1, a `JsonSerializerContext` for #2, embedded
reference assemblies for #3, `MSBUILD_EXE_PATH` for #4), native output matched JIT
**byte-for-byte**:

- `erd` on both samples.
- `classdiagram` on all three samples, and on the whole `src/` tree with and without
  `--inheritance --dependencies`.
- `visualize` on `.slnx` and a legacy `.sln`.
- `stats` (identical apart from the timing row).
- The MCP server over stdio: initialize, `tools/list` with schemas, all four tools, prompts,
  and resources.

Measured on linux-x64 (best of 5; JIT = `dotnet ProjGraph.Cli.dll`):

| Command | Native AOT | JIT |
| --- | --- | --- |
| `--help` | 21 ms / 32 MB | 180 ms / 46 MB |
| `stats ProjGraph.slnx` | 27 ms / 42 MB | 284 ms / 70 MB |
| `classdiagram` (complex-hierarchy sample) | 33 ms / 50 MB | 686 ms / 128 MB |
| `erd` (complex-ecommerce sample) | 37 ms / 53 MB | 764 ms / 131 MB |
| `classdiagram ./src --inheritance --dependencies` | 0.47 s | 1.50 s |

The gap narrows on large inputs as JIT warm-up amortizes. Native binaries are about 38 MB (CLI)
and 44 MB (MCP).

Other relevant facts:

- **Shallow Microsoft.Build usage.** `ProjectParser` reads raw XML (properties,
  `ProjectReference`/`PackageReference` items, `Version` metadata, walking up to
  `Directory.Build.props`/`Directory.Packages.props`) and never evaluates.
  `SlnParser` only calls `SolutionFile.Parse` and filters `KnownToBeMSBuildFormat`.
  `SlnxParser` already uses `XDocument`.
- **Unused package.** `Microsoft.CodeAnalysis.Workspaces.MSBuild` (in `Lib.ClassDiagram`) is
  unused: the project builds with warnings-as-errors after removing it. It is what adds the
  `BuildHost-net472`/`BuildHost-netcore` folders to publish output.
- **In-process integration tests.** `Tests.Integration.Cli` and `Tests.Integration.Mcp` run
  in-process, so they can't exercise a native binary.
- **AOT warnings become errors.** The repo sets `TreatWarningsAsErrors=true`, and
  `IlcTreatWarningsAsErrors` defaults to it. About 150 AOT/trim warnings from Roslyn, MSBuild,
  and Spectre would therefore fail publish.
- **Stale samples.** Four committed samples (`simple-context.mmd`, `design-patterns.mmd`,
  `complex-hierarchy.mmd`, `simple-hierarchy.mmd`) are stale against current JIT output.
- **Packable projects.** `ProjGraph.Cli`, `ProjGraph.Mcp`, and the six library projects are
  all packable and published by `publish.yml` today.

## Decisions (locked)

1. **Scope:** CLI and MCP server together, in one design.
2. **Microsoft.Build is removed.**
   - Project and props files are read with `System.Xml.Linq`.
   - `.sln` files are read with `Microsoft.VisualStudio.SolutionPersistence`.
   - The `MSBUILD_EXE_PATH` workaround isn't used.
3. **Platforms:** native `win-x64`, `linux-x64`, `linux-arm64`, `osx-arm64`, plus a
   framework-dependent `any` fallback package.
4. **Verification:** a linux-x64 AOT smoke job on every PR, plus the same smoke suite on each
   platform's runner during release, before anything is pushed.
5. **Delivery:** three sequential PRs, as listed under Purpose.
6. **Reference assemblies:** `CompilationFactory` always uses the embedded reference
   assemblies, on JIT too, so unit tests exercise the native code path.
7. **Smoke-test oracle:** native output is compared against the JIT build of the **same
   commit**, never against committed samples.

## Non-goals

- Changing any command, option, MCP tool, prompt, resource, or output format.
- Removing the EF Core package references or changing how the EF Core metadata reference is
  resolved.
- Regenerating the stale samples, or fixing bugs uncovered by characterization tests. These
  get recorded in the PR description, not changed.
- Replacing `SlnxParser` with SolutionPersistence.
- Standalone native binaries as GitHub Release assets, or any distribution channel other than
  NuGet tool packages.
- Making `ISlnParser`/`IProjectParser` asynchronous (that belongs with the planned
  CancellationToken work).

## Design

### PR 1: library preparation

No packaging or publish change. Every item below runs on the JIT runtime too.

#### 1.1 Characterization tests (first commit, before any parser change)

Add tests to `tests/ProjGraph.Tests.Unit.Core/Parsers/` that record the **current**
Microsoft.Build-backed behavior. Expected values come from running the existing
implementation, not from reading MSBuild docs.

- `ProjectParser` inputs:
  - The legacy `xmlns="http://schemas.microsoft.com/developer/msbuild/2003"` namespace.
  - `Version` as an attribute vs a child element.
  - A `PackageReference Update="..."` item.
  - Property-name case variants.
  - Whitespace-only property values.
  - Properties and items inside a `<Target>`.
  - Multiple `PropertyGroup`s defining the same property.
  - `Directory.Build.props` at two ancestor levels.
  - `Directory.Packages.props` resolution, including a case-variant package name.
  - Malformed XML, which must throw `ParsingException`.
- `SlnParser` inputs (committed `.sln` text fixtures):
  - An SDK C# project.
  - A solution folder.
  - A nested solution folder.
  - A website project (type GUID `{E24C65DC-7377-472B-9ABA-BC803B73C61A}`).
  - A non-C# MSBuild project (`.fsproj`).
  - Malformed content, which must throw `ParsingException`.

These tests must pass unchanged before and after 1.2/1.3.

#### 1.2 `ProjectParser` on `System.Xml.Linq`

- **Reading.** The file is read via `IFileSystem.ReadAllText` and parsed with
  `XDocument.Parse`, matching `SlnxParser`. All three read sites (the project,
  `Directory.Build.props`, `Directory.Packages.props`) go through one private
  `LoadXml(string path)` helper.
- **Element matching.** Elements match on `LocalName`, so both namespaced and namespace-less
  projects work.
- **Properties.** A property is any element whose parent is a `PropertyGroup`. Names match
  case-insensitively, the first in document order wins, and null or whitespace counts as
  undefined. The scope of the property/item search (e.g. whether `<Target>` contents count)
  follows whatever 1.1 recorded.
- **Items.** An item is any element whose parent is an `ItemGroup`. `Include` comes from the
  attribute, and metadata (`Version`) comes from an attribute or a child element.
- **Errors.** `XmlException` and `IOException` map to `ParsingException` exactly where
  `InvalidProjectFileException`/`XmlException`/`IOException`/`InvalidOperationException` map
  today. Props-file read failures are still swallowed, and the walk continues.
- **Unchanged.** Public signatures, deterministic ID generation, and project-type
  classification stay the same.

#### 1.3 `SlnParser` on SolutionPersistence

- Add `Microsoft.VisualStudio.SolutionPersistence` to `Directory.Packages.props` (the version
  currently resolved transitively, 1.0.52 at the time of writing) and reference it from
  `Lib.Core`.
- `GetProjectPaths` keeps its signature and its "missing file returns `[]`" behavior.
- Parsing goes through `IFileSystem.ReadAllText`, a `MemoryStream`, and
  `SolutionSerializers.SlnFileV12.OpenAsync(stream, CancellationToken.None)`.
- **Sync over async.** The call is bridged with `.GetAwaiter().GetResult()`, with a
  `SuppressMessage` for the VS threading analyzer. Justification: the stream is in memory, and
  neither the CLI nor the MCP server calls it on a thread with a synchronization context.
- **Result.** `SolutionProjects` are mapped to absolute paths (the solution directory combined
  with `FilePath`, normalised with `Path.GetFullPath`) and filtered to reproduce the
  `KnownToBeMSBuildFormat` set recorded in 1.1.
- **Errors.** Serializer exceptions and `IOException`/`UnauthorizedAccessException` map to
  `ParsingException`.

#### 1.4 Remove Microsoft.Build and Workspaces.MSBuild

- Delete `Microsoft.Build` from `Lib.Core` and `Microsoft.CodeAnalysis.Workspaces.MSBuild`
  from `Lib.ClassDiagram`.
- Delete their `PackageVersion` entries from `Directory.Packages.props` if nothing else
  references them.
- Update `CLAUDE.md` and `ARCHITECTURE.md` wherever they describe parsing "via
  `Microsoft.Build.Construction`".

#### 1.5 `CompilationFactory` uses embedded reference assemblies

- **Embedding.** `Lib.Core.csproj` embeds `System.Runtime.dll`, `System.Collections.dll`,
  `System.ComponentModel.Annotations.dll`, and `netstandard.dll` as resources, each with
  `LogicalName="refs/<file>"`. They come from
  `$(NetCoreTargetingPackRoot)/Microsoft.NETCore.App.Ref/$(BundledNETCoreAppPackageVersion)/ref/$(TargetFramework)/`.
- **Loading.** `BuildMetadataReferences` loads every `refs/` resource with
  `MetadataReference.CreateFromStream`, replacing the `Assembly.Location`-based BCL
  references and the `System.Collections`/DataAnnotations `TryAdd*` helpers.
- **EF Core.** `TryAddEntityFrameworkCoreReference` keeps its lookup but skips the assembly
  when `Location` is empty. Otherwise a loaded EF Core assembly under AOT would pass `""` to
  `CreateFromFile` and throw. In the spike it found nothing to add, and output matched JIT.
- **Build guard.** A build-time check fails with a clear error if any embedded file is
  missing from the targeting pack.
- **Binding parity.** The embedded set is the union of the reference assemblies that define a
  public type the runtime set bound (23 files, about 2 MB), not just the four above. Residual
  divergence: 19 implementation-only types with no reference contract no longer bind, and 118
  types the runtime set couldn't bind (e.g. `ConcurrentDictionary<TKey,TValue>`) now bind, so
  they are filtered as System types instead of appearing as diagram nodes.
- **Test.** A new unit test asserts that the reference set contains all four resources and
  that a compilation using `List<T>`, `[Required]`, and `IEnumerable<T>` has no error-typed
  symbols.

#### 1.6 MCP source-generated JSON

- **Context.** Add `src/ProjGraph.Mcp/McpJsonContext.cs`, an `internal sealed partial`
  `JsonSerializerContext` with
  `[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]`
  (the same settings as the reflection-based options it replaces, and the configuration the
  spike validated) and `[JsonSerializable]` for `AnalysisOptions`, `SolutionStats`, and
  `IEnumerable<ChatMessage>`.
- **Registration.** `Program` builds `JsonSerializerOptions` from
  `McpJsonUtilities.DefaultOptions`, inserts `McpJsonContext.Default` at the head of
  `TypeInfoResolverChain`, and passes it to `WithTools<ProjGraphTools>(...)` and
  `WithPrompts<ProjGraphPrompts>(...)`.
- **Stats.** `SerializeStatsWithWarnings` uses `McpJsonContext.Default.SolutionStats` and
  `McpJsonContext.Default.Options`. The reflection-based `JsonSerializerOptions` field is
  removed.
- **Parity check.** `SolutionStats` JSON keeps its explicit `[JsonPropertyName]` names. The
  existing `McpProjectGraphTests`/stats assertions must pass unchanged, and a new contract test
  asserts `tools/list` input schemas are identical before and after (snapshot captured in the
  PR's first commit).

#### 1.7 AOT analyzers on first-party libraries

- Set `IsAotCompatible=true` on `ProjGraph.Core` and all `ProjGraph.Lib*` projects.
- Any resulting IL2xxx/IL3xxx warning is fixed in code. A suppression is allowed only with a
  justification naming the runtime guard.

### PR 2: native build and PR smoke test

AOT stays **opt-in**: no csproj sets `PublishAot`, so `dotnet pack` and `publish.yml` behave
exactly as today.

#### 2.1 Executable project configuration

- **CLI:** `<TrimmerRootAssembly Include="ProjGraph.Cli"/>` and
  `<TrimmerRootAssembly Include="Spectre.Console.Cli"/>`.
- **CLI suppressions.** Each AOT/trim warning the analyzer reports in our code because of
  Spectre's annotations gets `[UnconditionalSuppressMessage]` with a justification citing the
  rooting and the smoke suite. The spike saw IL3050 on `CommandApp` construction in
  `Program.Main` and IL2067 in `TypeRegistrar.Register`.
- **MCP:** `PublishSingleFile` applies only when `'$(PublishAot)' != 'true'`, because the two
  are incompatible. `SelfContained` is left as is.
- **Both executables:**
  - `EnableTrimAnalyzer=true` and `EnableAotAnalyzer=true`, so first-party code stays clean
    under warnings-as-errors.
  - `IlcTreatWarningsAsErrors=false`, so third-party ILC warnings don't fail publish.
  - Linker warnings stay collapsed with the default `TrimmerSingleWarn`.

#### 2.2 `tests/ProjGraph.Tests.Smoke.Aot`

A new xUnit project, added to `ProjGraph.slnx`, using FluentAssertions like the other suites.

- **Configuration.** The suite reads four environment variables:
  - `PROJGRAPH_SMOKE_CLI_NATIVE`, the native CLI executable.
  - `PROJGRAPH_SMOKE_CLI_REFERENCE`, the JIT `ProjGraph.Cli.dll`, run via `dotnet`.
  - `PROJGRAPH_SMOKE_MCP_NATIVE`, the native MCP executable.
  - `PROJGRAPH_SMOKE_MCP_REFERENCE`, the JIT `ProjGraph.Mcp.dll`.
- **Skipping.** `[AotSmokeFact]` (a `FactAttribute` subclass) sets `Skip` when a required
  variable is unset, so local `dotnet test ProjGraph.slnx` and the existing CI matrix skip the
  suite.
- **Working directory.** Tests run from the repository root, located by walking up to
  `ProjGraph.slnx`.
- **Legacy `.sln` fixture.** `tests/ProjGraph.Tests.Smoke.Aot/Fixtures/legacy.sln` references
  the `samples/visualize/modular-architecture` projects by relative path.
- **CLI cases.** Each case runs native and reference with the same arguments and asserts equal
  exit codes and equal stdout. Diagram cases also write with `--output` to separate temp
  files and compare the files byte-for-byte.

| Case | Arguments |
| --- | --- |
| visualize mermaid | `visualize samples/visualize/modular-architecture/ModularArchitecture.slnx --format mermaid` |
| visualize tree | same with `--format tree` |
| visualize flat | same with `--format flat` |
| visualize sln | `visualize <fixture>/legacy.sln --format mermaid` |
| erd ×2 | the two `samples/erd` DbContexts |
| classdiagram ×3 | the three `samples/classdiagram` invocations from `regenerate-samples.sh` |
| classdiagram directory | `classdiagram src --inheritance --dependencies` |
| stats | `stats ProjGraph.slnx` (drop the `Analysis time` row, collapse runs of spaces, then compare) |
| strict parsing | `stats ProjGraph.slnx --tpo 3` (non-zero exit on both; same error text) |

- **MCP cases.** The suite starts native and reference servers with the ModelContextProtocol SDK's
  stdio client transport and asserts:
  - The handshake succeeds, with the same `serverInfo.name`.
  - `tools/list` is equal (names, descriptions, input schemas).
  - `get_project_graph`, `get_project_stats`, `get_class_diagram` (with
    `options.inheritance = true`), and `get_erd`, called with absolute sample paths, all
    return `isError != true` and equal text content.
  - `prompts/list` is equal.
  - `resources/read` of `projgraph://welcome` is equal.

#### 2.3 CI job `aot-smoke`

A new job in `.github/workflows/ci.yml`, on `ubuntu-latest`, running in parallel with the
existing matrix and required for merge:

1. Check out, set up .NET from `global.json`, and ensure `clang` and `zlib1g-dev` are present.
2. `dotnet build ProjGraph.slnx -c Release`.
3. `dotnet publish src/ProjGraph.Cli -c Release -r linux-x64 -p:PublishAot=true -o out/cli`,
   and the same for `src/ProjGraph.Mcp` into `out/mcp`.
4. `dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build`, with the four
   variables pointing at `out/cli`, `out/mcp`, and the Release build outputs.

### PR 3: packaging and release

#### 3.1 Executable project files

- **Both executables:** `PublishAot=true` and
  `<ToolPackageRuntimeIdentifiers>win-x64;linux-x64;linux-arm64;osx-arm64;any</ToolPackageRuntimeIdentifiers>`.
- **MCP:** remove `SelfContained`, `PublishSelfContained`, and `PublishSingleFile` (and the
  PR 2 condition). RID-specific packages are self-contained by definition, and `any` must be
  framework-dependent.
- **CLI:** keep `RollForward=LatestMajor`, which applies to the `any` package.
- **Pointer package contents:** confirm `README.md`, `icon.png`, and `.mcp/server.json` still
  land in it.

#### 3.2 `publish.yml` job graph

Every job derives the version from the tag and passes `-p:Version=<version>` to
build/pack commands. The `sed` edit of `Directory.Build.props` is removed, because jobs
don't share a workspace. The `server.json` `sed` stays, in `publish` only.

1. **`prepare`** (`ubuntu-latest`): tag validation, version output, restore, build, and test.
   These are today's steps up to `Test`.
2. **`pack-native`** (needs `prepare`), one job per platform:

   | RID | Runner |
   | --- | --- |
   | `win-x64` | `windows-latest` |
   | `linux-x64` | `ubuntu-latest` |
   | `linux-arm64` | `ubuntu-24.04-arm` |
   | `osx-arm64` | `macos-latest` |

   Steps:
   1. On Linux, ensure `clang` and `zlib1g-dev` are installed.
   2. `dotnet build ProjGraph.slnx -c Release -p:Version=<v>`, which provides the JIT
      reference and the smoke project.
   3. `dotnet pack src/ProjGraph.Cli -c Release -r <rid> -p:Version=<v> -o artifacts`, and the
      same for `src/ProjGraph.Mcp`.
   4. Extract `ProjGraph.Cli.<rid>.<v>.nupkg` and `ProjGraph.Mcp.<rid>.<v>.nupkg`, locate the
      executables under `tools/`, and run `dotnet test tests/ProjGraph.Tests.Smoke.Aot` against
      them. The smoke suite therefore checks the exact shipped binary.
   5. Upload `artifacts/*.nupkg` (and any `.snupkg`) as `packages-<rid>`.
3. **`pack-portable`** (needs `prepare`, `ubuntu-latest`):
   1. `dotnet pack ProjGraph.slnx -c Release -p:Version=<v> -o artifacts`, which produces the
      six library packages and the two tool pointer packages.
   2. `dotnet pack src/ProjGraph.Cli -c Release -r any -p:PublishAot=false -p:Version=<v> -o artifacts`,
      and the same for Mcp.
   3. Upload as `packages-portable`.
   4. The job fails if the expected package set isn't exactly present: 6 libraries, 2
      pointers, 2 `any`.
4. **`publish`** (needs `pack-native` and `pack-portable`, `ubuntu-latest`):
   1. Download all artifacts and assert the full set (6 libraries, 2 pointers, 2 `any`, 8
      RID-specific packages).
   2. Push the libraries and all RID-specific and `any` packages to NuGet.org and GitHub
      Packages, with `--skip-duplicate`.
   3. Poll `https://api.nuget.org/v3-flatcontainer/<id-lowercase>/index.json` for each of the
      10 tool sub-packages until `<v>` is listed. Poll every 30 s with a 30-minute timeout; the
      job fails on timeout, before any pointer package is pushed.
   4. Push the two pointer packages to both feeds, with `--skip-duplicate`.
   5. Create the GitHub Release with all packages attached, update the `server.json` version,
      then run the existing NuGet validation wait and MCP Registry publish steps unchanged.
      `server.json` keeps `identifier: ProjGraph.Mcp`, which is now the pointer package.

Re-running via `workflow_dispatch` resumes safely: duplicates are skipped and the poll
succeeds immediately for already-listed packages.

#### 3.3 Documentation

- `src/ProjGraph.Cli/README.md` and `src/ProjGraph.Mcp/README.md` state that installation
  requires a .NET 10 SDK, and that on win-x64, linux-x64, linux-arm64, and osx-arm64 the tool
  runs as a native executable with no runtime dependency.
- The MCP Registry ownership comment stays at the end of the MCP README.
- The DocFX installation guide gets the same note.

### Data flow (release)

```text
tag v*
  └─ prepare (build + test)
       ├─ pack-native ×4 (pack -r rid → extract → smoke) ─┐
       └─ pack-portable (libs + pointers + any) ──────────┤
                                                          └─ publish
                                                               ├─ push libs + rid + any
                                                               ├─ poll NuGet flat container
                                                               ├─ push pointers
                                                               ├─ GitHub Release
                                                               └─ MCP Registry
```

### Error handling and degradation

- Parser error mapping is unchanged from the user's point of view: `ParsingException` with
  the same messages, and props-file failures skipped.
- A failed native smoke test on any platform fails the release before anything is pushed.
- If a platform's native package misbehaves after release, remove that RID from
  `ToolPackageRuntimeIdentifiers` in a patch release. That platform then falls back to the
  `any` package.

## Testing

| Layer | What | When |
| --- | --- | --- |
| Characterization (1.1) | Parser behavior pinned against Microsoft.Build, then against the replacements | PR 1, every CI run |
| Unit/contract | Embedded reference set; `tools/list` schema snapshot; existing suites unchanged | PR 1, every CI run |
| Sample parity | Outputs from PR 1's JIT build byte-identical to `develop`'s JIT build for every `regenerate-samples.sh` invocation | PR 1, recorded in the PR description |
| AOT smoke (2.2) | Native vs JIT, CLI and MCP | PR 2 onward, every PR on linux-x64 |
| Release smoke (3.2) | Same suite on the packaged binary for each RID | PR 3 onward, every release |
| Packaging dry run | `publish.yml` run on a pre-release tag (`vX.Y.Z-rc.1`) end-to-end, then `dnx ProjGraph.Mcp@<rc>` and `dotnet tool install -g ProjGraph.Cli --version <rc>` on one machine per native RID plus one fallback platform. This publishes public pre-release packages to NuGet.org and the MCP Registry, as any pre-release tag does today. | After PR 3 merges, before the first stable release with native packages |

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| XDocument or SolutionPersistence diverges from Microsoft.Build on edge cases | Characterization tests land first and must pass before and after; divergences are documented, not silently absorbed |
| SolutionPersistence has hidden AOT issues on `.sln` | The legacy `.sln` case is in the smoke suite on every PR and release platform |
| Windows/macOS native builds behave differently from linux-x64 (only platform spiked) | Release smoke runs on every RID before publish; RID removal gives a fallback |
| Third-party AOT warnings hide a real new problem | First-party analyzers stay strict; the smoke suite is the runtime guard; dependency bumps run through `aot-smoke` |
| `NUGET_API_KEY` can't push the 10 new package IDs | Verify the key's glob scope (e.g. `ProjGraph.*`) before the RC dry run |
| Users on SDK < 10 can't install the pointer package | Tools already require the .NET 10 runtime; requirement documented in READMEs and docs |
| Pointer package visible before sub-packages are indexed | Flat-container poll gates the pointer push |
| AOT has no tiered PGO, so long MCP sessions on huge solutions gain less | Accepted; spike shows native is still faster on the largest measured input |

## Exit criteria

- **PR 1:**
  - The suites are green, the characterization tests pass, and there are no Microsoft.Build or
    Workspaces.MSBuild references.
  - The embedded reference set is in use.
  - `tools/list` schemas are unchanged.
  - Sample parity is recorded.
- **PR 2:** the `aot-smoke` job is green and required, and no csproj enables `PublishAot`.
- **PR 3:**
  - The RC dry run publishes all 18 packages in the specified order.
  - The release smoke passes on all four RIDs.
  - `dnx`/`dotnet tool install` work on each native RID and on a fallback platform.
  - The MCP Registry entry resolves.
