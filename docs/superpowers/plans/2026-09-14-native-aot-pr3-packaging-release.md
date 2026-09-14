# Native AOT PR 3: Packaging and Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `ProjGraph.Cli` and `ProjGraph.Mcp` as RID-specific Native AOT tool packages with a framework-dependent `any` fallback. Every native package is smoke-tested on its own platform before a release pushes anything.

**Architecture:** Both tool projects set `PublishAot=true` and `ToolPackageRuntimeIdentifiers`. A plain `dotnet pack` then produces only a small pointer package. Each native package comes from `dotnet pack -r <rid>` on a matching OS, and the fallback from `dotnet pack -r any -p:PublishAot=false`.

One bash script, `.github/scripts/native-tool-smoke.sh`, runs build → native pack → `dotnet tool install` from the local packages → smoke suite for one RID. A composite action runs that script on the runner, or inside an Alpine .NET SDK container for `linux-musl-*`. The existing `aot-smoke` PR job uses the action for linux-x64.

A new reusable workflow, `pack.yml`, runs the action for all six native RIDs and packs the libraries, the fallbacks, and the pointer packages. It is called by `publish.yml` and also runs on PRs that touch packaging. `publish.yml` pushes every package except the pointers, waits until NuGet.org lists all 14 tool sub-packages, then pushes the pointers. The smoke suite also gains the PR 2 review follow-ups: a guard that the "native" build really is Native AOT, and MCP server stderr in failure output.

**Tech Stack:** .NET 10 (SDK per `global.json`; verified on 10.0.401), C#, xUnit 2.9 + FluentAssertions 8, ModelContextProtocol 2.2.0, GitHub Actions (hosted `windows-latest`, `ubuntu-latest`, `ubuntu-24.04-arm`, `macos-latest`), `mcr.microsoft.com/dotnet/sdk:10.0-alpine`, bash, actionlint/shellcheck (via Docker).

**Spec:** `docs/superpowers/specs/2026-09-14-native-aot-design.md` (section "PR 3: packaging and release", plus "Testing", "Risks", and "Exit criteria"). Read it before starting. PR 1 (#191, `86082ea`) and PR 2 (#192, `fe0f3fd`) are merged. PR 2's plan is `docs/superpowers/plans/2026-09-14-native-aot-pr2-native-build-smoke.md`.

## Global Constraints

- `TreatWarningsAsErrors=true`, `AnalysisMode=All`, and `EnforceCodeStyleInBuild=true` apply to every project, tests included. Sonar, Roslynator, VS Threading, and CA analyzers run during build, so any warning fails it. Private members that have a `<summary>` also need their `<param>`/`<returns>`/`<exception>` elements (RCS1140/RCS1141).
- XML documentation is required on all public APIs. Test projects disable CS1591, but the Roslynator rules above still apply to any doc comment you write.
- CI runs `dotnet format ProjGraph.slnx --no-restore --verify-no-changes`, which must pass.
- Package versions are central (`Directory.Packages.props`). This PR adds no package.
- No change to any command, option, MCP tool, prompt, resource, or output format (spec Non-goals).
- Smoke-test oracle: native output is compared with the JIT build of the **same commit**, never with committed samples (spec decision 7).
- **Native RIDs (spec decision 3, widened, see deviation 1):** `win-x64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-arm64`, plus `any`. The list must be identical in both csproj files, in `pack.yml`'s matrix, and in the scripts' package sets.
- **Release package set: 22 packages.** 6 libraries, 2 pointer packages (`ProjGraph.Cli`, `ProjGraph.Mcp`), 2 `any` packages, and 12 native packages (2 × 6 RIDs).
- **Pointer packages are pushed last**, only after NuGet.org's flat container lists every one of the 14 tool sub-packages (spec 3.2 step 4.3).
- **Run native packs one at a time.** Each ILC run takes several GB of RAM, and back-to-back AOT builds have been OOM-killed on the 16 GB dev machine. `/tmp` on that machine is RAM-backed tmpfs, so delete scratch clones when you're done with them.
- **Before a local native pack, delete `src/ProjGraph.{Cli,Mcp}/bin/Release/net10.0/<rid>`.** A pack reuses that `publish/` folder and ships whatever it finds there, including a stale `.dbg` from an earlier publish.
- **Local `dotnet tool install` always uses a fresh `NUGET_PACKAGES` folder.** Otherwise a previously installed package with the same version is served from the global cache.
- Don't edit `ProjGraph.slnx` with `dotnet sln add` (it rewrites every `<File Path="..."/>` line). This PR doesn't need to touch it.
- **Create files with the Write tool or an editor, never a shell heredoc.** On the dev machine, a token-saving command hook (DTK/RTK) rewrites `dotnet restore|build|test` into `dtk dotnet …`, even inside heredoc text passed to `bash`. During planning this silently corrupted the scripts and workflows, and the Alpine run then failed with `dtk: command not found`. `grep -rnE '\b(dtk|rtk) ' .github CONTRIBUTING.md CLAUDE.md ARCHITECTURE.md` must print nothing before every commit that touches those files. The same hook also summarizes `dotnet` output; to see raw output, call the SDK by its full path, `"$(readlink -f "$(command -v dotnet)")"`.
- Pushing, opening the PR, tagging, and repository-settings changes are outward-facing: confirm with the user before each.

## Deviations from the spec (intentional; mention them in the PR description)

1. **Two extra native RIDs, `linux-musl-x64` and `linux-musl-arm64` (user decision, 2026-09-14).**
   - Why: without them, `dotnet tool install`/`dnx` on Alpine resolves the glibc `linux-x64` package, not `any`, and the tool fails with `projgraph: not found`. This was reproduced in `mcr.microsoft.com/dotnet/sdk:10.0-alpine`. The RID graph treats `linux-musl-x64` as compatible with `linux-x64`, and `linux-musl-arm64` with `linux-arm64` the same way.
   - With `linux-musl-x64` listed, Alpine selects the musl package and the glibc host still selects `linux-x64` (both verified).
   - The musl packages are built and smoke-tested inside the Alpine SDK container, because musl binaries must link on a musl system.
   - The release grows from 18 packages to 22, and from 10 polled sub-packages to 14.
2. **`pack.yml` is a reusable workflow, and it also runs on PRs that change packaging.** The spec puts `pack-native`/`pack-portable` directly in `publish.yml`. As a separate workflow, a PR that touches the tool projects, the smoke suite, the packaging scripts, `Directory.*.props`, or `global.json` builds and smoke-tests every platform before merge. Otherwise the first Windows, macOS, Arm64, or musl native build would happen on a release tag. `publish.yml` calls it with `uses:`, so the release job graph is the spec's `prepare → pack-native ×6 + pack-portable → publish`.
3. **The release smoke installs the tools with `dotnet tool install`** from the pointer package plus the native package, instead of extracting executables from the `.nupkg`.
   - This exercises the pointer's RID resolution and the exec-bit handling. Extracting would lose the exec bit, so the PR 2 note "chmod +x on extracted executables" doesn't apply.
   - The install uses a NuGet config that lists only the local packages and a fresh package folder. A missing native package fails the install (verified) instead of falling back to NuGet.org.
4. **`aot-smoke` uses the same pack → install → smoke path** through the shared composite action, instead of `dotnet publish`. The job id stays `aot-smoke`, so the pending "make it required" step still applies.
5. **Native debug symbols aren't packaged.**
   - The setting is `CopyOutputSymbolsToPublishDirectory=false` when `PublishAot=true`.
   - Why: the linux-x64 CLI package otherwise carries `ProjGraph.Cli.dbg`, 95 MB raw and 18.4 MB compressed, which makes the package 32.3 MB instead of 14.7 MB.
   - The `any` packages keep their `.pdb` files.
6. **`IncludeSymbols=false` on both tool projects.** A pointer package has no assemblies, and packing its symbols package fails with `NU5017: Cannot create a package that has no dependencies nor content`. That breaks `dotnet pack ProjGraph.slnx`. The two tool `.snupkg` files stop shipping; the six library `.snupkg` files are unchanged.
7. **Pointer packages travel separately.** `pack-portable` uploads them as a `pointers` artifact, and `publish` keeps them in `artifacts/pointers`. Push order is therefore a directory choice, not a file-name filter.
8. **`pack-portable` smoke-tests the `any` packages too.** It runs their `.dll`s against the JIT build, with the native-build guard filtered out. The guard would otherwise fail, because a fallback package is a JIT build.
9. **The MCP reference on Windows is the JIT apphost `ProjGraph.Mcp.exe`.** The SDK wraps stdio commands in `cmd.exe /c`, which is fragile with a quoted `C:\Program Files\dotnet\dotnet.exe` host path.
10. **`Tests.Integration.Mcp`'s `McpServerProcess` changes.**
    - ProjGraph.Mcp is no longer self-contained, so its apphost moves from `bin/<cfg>/net10.0/<rid>/` to `bin/<cfg>/net10.0/`, and the helper now launches it from there.
    - The spec didn't anticipate this; without the change, the four `McpTransportTests` fail.
11. **PR 2 final-review follow-ups included:**
    - the `NativeBuildGuardTests` class;
    - MCP server stderr captured into the test output;
    - the IL3050 suppression narrowed to a `CreateApp` helper;
    - split pack steps;
    - TRX upload on failure.
12. **Plain JIT builds now carry AOT feature switches.** With `PublishAot` in the csproj, `dotnet build` writes `IsDynamicCodeSupported=false`, `JsonSerializer.IsReflectionEnabledByDefault=false`, and similar switches into `ProjGraph.Cli.runtimeconfig.json`/`ProjGraph.Mcp.runtimeconfig.json`. The smoke reference and `dotnet run` therefore run with them. The `any` packages (`-p:PublishAot=false`) don't, which is why deviation 8 tests them separately.

## Verified facts this plan relies on

Checked on 2026-09-14 in throwaway copies of `develop` at `fe0f3fd` (linux-x64, SDK 10.0.401), plus `mcr.microsoft.com/dotnet/sdk:10.0-alpine` (10.0.401) for musl.

**Build and tests**

- Build with both csproj changes from Task 3: 0 warnings, 0 errors.
- Test results before and after the `McpServerProcess` fix:
  - Before: 1,174 passed and 4 failed. All four failures are `McpTransportTests`, with `Expected serverExe not to be <null> because the MCP server apphost must be present under …/src/ProjGraph.Mcp/bin/Release/net10.0.`
  - After: `Tests.Integration.Mcp` passes 150/150, the whole solution passes 1,178, and the smoke suite reports 30 skipped.

**Packing**

- `dotnet pack ProjGraph.slnx` with the tools' `IncludeSymbols` still on fails NU5017 for both tool projects.
- With `IncludeSymbols=false`, it produces 6 library `.nupkg` + 6 `.snupkg`, and 2 pointer packages of about 32 KB.
- Pointer package contents:
  - `tools/any/any/DotnetToolSettings.xml` with one `<RuntimeIdentifierPackage RuntimeIdentifier="…" Id="ProjGraph.Cli.<rid>" />` per listed RID.
  - Package type `DotnetTool`, plus `McpServer` for the MCP pointer.
  - The MCP pointer also contains `.mcp/server.json`, `README.md`, and `icon.png` (spec 3.1).
- `dotnet pack src/ProjGraph.Cli -r any -p:PublishAot=false`:
  - Produces `ProjGraph.Cli.any.<v>.nupkg` (8.2 MB, type `DotnetToolRidPackage`, files under `tools/net10.0/any/`, runtimeconfig without AOT switches).
  - Builds into `bin/Release/net10.0/any/`.
- `dotnet pack src/ProjGraph.Cli -r linux-x64`:
  - Takes 47 s (the MCP pack took 60 s).
  - Produces `ProjGraph.Cli.linux-x64.<v>.nupkg` with the binary at `tools/any/linux-x64/ProjGraph.Cli`.
  - Doesn't touch the JIT build: all 382 `ProjGraph.*` files under `src/*/bin/Release` and `tests/*/bin/Release` were byte-identical before and after. The pack only added `bin/Release/net10.0/linux-x64/`.
  - ILC prints third-party `IL2104`/`IL3053`/`IL3000` warnings and the known informational `OpenCliParser … NJsonSchema` line. These don't fail the pack.
- Sizes with native symbols excluded: `ProjGraph.Cli.linux-x64` 14.7 MB, `ProjGraph.Mcp.linux-x64` 17.1 MB. The MCP binary is 42,946,344 bytes, identical to PR 2.

**Installing and resolution**

- `dotnet tool install ProjGraph.Cli --version <v> --configfile <config listing only the local folder> --tool-path <dir>`:
  - Selects `projgraph.cli.linux-x64`.
  - `<dir>/projgraph` is a symlink to `.store/projgraph.cli/<v>/projgraph.cli.linux-x64/<v>/tools/any/linux-x64/ProjGraph.Cli`, with the exec bit set.
  - `ProjGraph.Mcp` installs the command `ProjGraph.Mcp` the same way.
- With the native package missing, the install fails: `Version <v> of package projgraph.cli.linux-x64 is not found in NuGet feeds …`, exit 1. There is no fallback to `any`.
- Alpine:
  - Pointer without musl RIDs: it installs `projgraph.cli.linux-x64`, and running it prints `sh: /tmp/t/projgraph: not found`.
  - Pointer listing `linux-musl-x64`: Alpine installs `projgraph.cli.linux-musl-x64`, and `projgraph stats ProjGraph.slnx` works. The glibc host still installs `linux-x64`.
- `dotnet pack src/ProjGraph.Cli -r linux-musl-x64` inside `sdk:10.0-alpine`, after `apk add --no-cache clang build-base zlib-dev`: 70 s, 15.4 MB.

**Smoke suite**

- The plan's `native-tool-smoke.sh`, run from a clean copy:
  - `linux-x64` on the host with `GITHUB_ACTIONS=true`: TRX `total="30" passed="30"`. Packages are ~15.4 MB (CLI) and ~17.9 MB (MCP), pointers ~32 KB, and both commands link into the `linux-x64` packages.
  - `linux-musl-x64` through the composite action's exact `docker run … sdk:10.0-alpine` command: `Passed: 30`, exit 0, and both commands link into the `linux-musl-x64` packages. The chown handed every file back to the host user.
- Installed native tools vs the JIT build (reference `.dll`s from `bin/Release/net10.0/`): 30/30 passed in 15 s with `GITHUB_ACTIONS=true`, including the two guard tests.
- The `any` package `.dll`s vs the JIT build (guard excluded): 28/28 passed in 29 s.
- Negative checks:
  - With the "native" CLI set to the JIT apphost, `CliNative_ShouldBeNativeAotExecutable` fails: `…/ProjGraph.Cli has a managed entry point beside it, so it is a JIT apphost, not a Native AOT executable`.
  - With the "native" MCP server set to a script that writes to stderr and exits 3, the 10 MCP parity tests fail. The exception message (SDK 2.2.0) already carries `Server's stderr tail`, and each failing test's `Standard Output Messages` shows `Native server stderr:` with the script's line.
  - Totals for that run: 19 passed, 11 failed.

**Other checks**

- Without the suppression on `CreateApp`, `Program.cs(68,16): error IL3050` points at `new CommandApp(registrar)`. With it, the build is clean, and so is `dotnet format --verify-no-changes`.
- `https://api.nuget.org/v3-flatcontainer/projgraph.cli/index.json` lists `1.1.0`, and an unknown ID returns 404.
- `docker run --rm --user "$(id -u):$(id -g)" -v "$PWD":/repo -w /repo rhysd/actionlint:latest -color=false <files>` works. So does `--entrypoint shellcheck`. Without `--user`, files with mode 600 are unreadable.
- The current `publish.yml` already has shellcheck `SC2086` infos; the new one must lint clean.
- MCP SDK 2.2.0 `StdioClientTransport` wraps any command other than `cmd.exe` in `cmd.exe /c` on Windows. Arguments without whitespace get `^`-escaping.
- `aot-smoke` isn't a required check yet. The `develop` ruleset has only deletion, non-fast-forward, and Copilot review rules.

## File Structure

| File | Change | Responsibility |
| --- | --- | --- |
| `tests/ProjGraph.Tests.Smoke.Aot/NativeBuildGuardTests.cs` | Create | Fails when a "native" variable points at a JIT apphost or `.dll` |
| `tests/ProjGraph.Tests.Smoke.Aot/Helpers/McpServerPair.cs` | Modify | Captures each server's stderr lines |
| `tests/ProjGraph.Tests.Smoke.Aot/McpParityTests.cs` | Modify | Writes captured stderr to the xUnit test output |
| `src/ProjGraph.Cli/Program.cs` | Modify | IL3050 suppression narrowed to `CreateApp` |
| `src/ProjGraph.Cli/ProjGraph.Cli.csproj` | Modify | `PublishAot`, RID list, symbol settings |
| `src/ProjGraph.Mcp/ProjGraph.Mcp.csproj` | Modify | Same; drops `SelfContained`/`PublishSelfContained`/`PublishSingleFile` |
| `tests/ProjGraph.Tests.Integration.Mcp/Helpers/McpServerProcess.cs` | Modify | Launches the framework-dependent apphost from `bin/<cfg>/<tfm>/` |
| `.github/scripts/native-tool-smoke.sh` | Create | Build → pack native → install → smoke, for one RID |
| `.github/actions/native-tool-smoke/action.yml` | Create | Runs the script on the runner, or in Alpine for `linux-musl-*` |
| `.github/scripts/verify-packages.sh` | Create | Requires a folder to hold exactly the named package sets |
| `.github/scripts/wait-for-nuget.sh` | Create | Polls NuGet.org's flat container until versions are listed |
| `.github/workflows/ci.yml` | Modify | `aot-smoke` uses the composite action |
| `.github/workflows/pack.yml` | Create | `pack-native` ×6 and `pack-portable`; reusable, also PR-triggered |
| `.github/workflows/publish.yml` | Modify | `prepare` → `pack` → `publish` with ordered pushes |
| `src/ProjGraph.Cli/README.md`, `src/ProjGraph.Mcp/README.md`, `docfx/guides/getting-started.md` | Modify | Installation note: SDK requirement, native platforms |
| `CLAUDE.md`, `CONTRIBUTING.md`, `ARCHITECTURE.md` | Modify | Packaging pattern, smoke suite, release flow |

---

### Task 1: Smoke suite guard and MCP stderr capture

Two PR 2 review follow-ups, verified before any packaging change with PR 2's publish commands: a guard that fails when a "native" build is really a JIT build, and MCP server stderr in the test output.

**Files:**
- Create: `tests/ProjGraph.Tests.Smoke.Aot/NativeBuildGuardTests.cs`
- Modify: `tests/ProjGraph.Tests.Smoke.Aot/Helpers/McpServerPair.cs`
- Modify: `tests/ProjGraph.Tests.Smoke.Aot/McpParityTests.cs`

**Interfaces:**
- Consumes (existing, unchanged): `SmokeEnvironment.CliNative`/`McpNative` (`SmokeCommand`), `SmokeCommand(string FileName, IReadOnlyList<string> LeadingArguments)`, `[AotSmokeFact]`.
- Produces:
  - `public sealed class NativeBuildGuardTests` in namespace `ProjGraph.Tests.Smoke.Aot`. Task 5's `pack-portable` filters it out by the name `ProjGraph.Tests.Smoke.Aot.NativeBuildGuardTests`.
  - `McpServerPair.DescribeStandardError()` returns a `string`.
  - The suite now has 30 tests: 18 CLI, 10 MCP, and 2 guard tests.

- [ ] **Step 1: Create the feature branch and commit this plan**

```bash
git switch develop
git pull --ff-only
git switch -c feat/aot-packaging
git add docs/superpowers/plans/2026-09-14-native-aot-pr3-packaging-release.md
git commit -m "docs: add the Native AOT PR 3 implementation plan"
```

(If the plan file is already committed, skip the `git add`/`commit`.)

- [ ] **Step 2: Build the native binaries the PR 2 way (they're needed to run the new tests)**

The csproj files don't set `PublishAot` yet, so this is PR 2's local loop from `CONTRIBUTING.md`. Run the two publishes one after the other:

```bash
dotnet build ProjGraph.slnx -c Release
dotnet publish src/ProjGraph.Cli -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/cli
dotnet publish src/ProjGraph.Mcp -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/mcp
```

Expected: both publishes succeed, and `artifacts/native/cli/ProjGraph.Cli` and `artifacts/native/mcp/ProjGraph.Mcp` exist.

- [ ] **Step 3: Write the guard tests**

Create `tests/ProjGraph.Tests.Smoke.Aot/NativeBuildGuardTests.cs`:

```csharp
using ProjGraph.Tests.Smoke.Aot.Helpers;

namespace ProjGraph.Tests.Smoke.Aot;

/// <summary>
/// Guards the suite's premise: the builds under test must be Native AOT executables. A JIT apphost
/// or a framework-dependent <c>.dll</c> matches the reference trivially, so a misconfigured job, or a
/// tool package that lost <c>PublishAot</c>, would otherwise pass every parity test.
/// </summary>
public sealed class NativeBuildGuardTests
{
    [AotSmokeFact]
    public void CliNative_ShouldBeNativeAotExecutable()
    {
        AssertNativeAotExecutable(SmokeEnvironment.CliNative);
    }

    [AotSmokeFact]
    public void McpNative_ShouldBeNativeAotExecutable()
    {
        AssertNativeAotExecutable(SmokeEnvironment.McpNative);
    }

    private static void AssertNativeAotExecutable(SmokeCommand command)
    {
        command.LeadingArguments.Should().BeEmpty("a Native AOT build starts directly, not through the dotnet host");

        // dotnet tool install links the command to the executable in its package store on Linux and macOS.
        var executable = new FileInfo(command.FileName);
        var target = executable.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? executable.FullName;

        // A JIT apphost always ships its managed entry point beside it: <name>.dll for <name> or <name>.exe.
        var stem = OperatingSystem.IsWindows() ? Path.ChangeExtension(target, extension: null) : target;
        File.Exists(stem + ".dll").Should().BeFalse(
            $"{target} has a managed entry point beside it, so it is a JIT apphost, not a Native AOT executable");
    }
}
```

Don't use `Path.ChangeExtension` on Linux/macOS: it would turn `ProjGraph.Mcp` into `ProjGraph.dll`.

- [ ] **Step 4: Run the guard against a JIT apphost and watch it fail**

```bash
dotnet build tests/ProjGraph.Tests.Smoke.Aot -c Release
GITHUB_ACTIONS=true \
PROJGRAPH_SMOKE_REQUIRED=true \
PROJGRAPH_SMOKE_CLI_NATIVE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE=artifacts/native/mcp/ProjGraph.Mcp \
PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.dll \
dotnet test tests/ProjGraph.Tests.Smoke.Aot --no-build -c Release --filter "FullyQualifiedName~NativeBuildGuardTests"
```

Expected: `Failed: 1, Passed: 1`. `CliNative_ShouldBeNativeAotExecutable` fails with `…/ProjGraph.Cli has a managed entry point beside it, so it is a JIT apphost, not a Native AOT executable`, and `McpNative_ShouldBeNativeAotExecutable` passes.

- [ ] **Step 5: Capture MCP server stderr**

In `tests/ProjGraph.Tests.Smoke.Aot/Helpers/McpServerPair.cs`:

Add `using System.Collections.Concurrent;` after `using ModelContextProtocol.Client;`.

Replace the two fields and `GetClientsAsync`:

```csharp
    private McpClient? _native;
    private McpClient? _reference;

    /// <summary>
    /// Connects to both servers on the first call and returns the same clients afterwards.
    /// </summary>
    /// <returns>The native and the reference client.</returns>
    public async Task<(McpClient Native, McpClient Reference)> GetClientsAsync()
    {
        _native ??= await ConnectAsync(SmokeEnvironment.McpNative);
        _reference ??= await ConnectAsync(SmokeEnvironment.McpReference);
        return (_native, _reference);
    }
```

with:

```csharp
    private readonly ConcurrentQueue<string> _nativeStandardError = new();
    private readonly ConcurrentQueue<string> _referenceStandardError = new();
    private McpClient? _native;
    private McpClient? _reference;

    /// <summary>
    /// Connects to both servers on the first call and returns the same clients afterwards.
    /// </summary>
    /// <returns>The native and the reference client.</returns>
    public async Task<(McpClient Native, McpClient Reference)> GetClientsAsync()
    {
        _native ??= await ConnectAsync(SmokeEnvironment.McpNative, _nativeStandardError);
        _reference ??= await ConnectAsync(SmokeEnvironment.McpReference, _referenceStandardError);
        return (_native, _reference);
    }

    /// <summary>
    /// Describes everything both servers have written to standard error so far. The servers log
    /// warnings and unhandled exceptions there, which the protocol results alone never show.
    /// </summary>
    /// <returns>The native and the reference server's standard error, labelled.</returns>
    public string DescribeStandardError()
    {
        return $"Native server stderr:\n{string.Join('\n', _nativeStandardError)}\n" +
               $"Reference server stderr:\n{string.Join('\n', _referenceStandardError)}";
    }
```

Replace `ConnectAsync`:

```csharp
    private static async Task<McpClient> ConnectAsync(SmokeCommand command)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ProjGraph AOT smoke",
            Command = command.FileName,
            Arguments = [.. command.LeadingArguments],
            WorkingDirectory = SmokeEnvironment.RepositoryRoot
        });
```

with:

```csharp
    private static async Task<McpClient> ConnectAsync(SmokeCommand command, ConcurrentQueue<string> standardError)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ProjGraph AOT smoke",
            Command = command.FileName,
            Arguments = [.. command.LeadingArguments],
            WorkingDirectory = SmokeEnvironment.RepositoryRoot,
            StandardErrorLines = standardError.Enqueue
        });
```

- [ ] **Step 6: Write the stderr to each MCP test's output**

In `tests/ProjGraph.Tests.Smoke.Aot/McpParityTests.cs`, add `using Xunit.Abstractions;` after `using System.Text.Json.Nodes;`, then replace:

```csharp
/// <param name="servers">The native and reference servers shared by this class.</param>
public sealed class McpParityTests(McpServerPair servers) : IClassFixture<McpServerPair>
{
```

with:

```csharp
/// <param name="servers">The native and reference servers shared by this class.</param>
/// <param name="output">Receives both servers' standard error, which xUnit reports with a failing test.</param>
public sealed class McpParityTests(McpServerPair servers, ITestOutputHelper output)
    : IClassFixture<McpServerPair>, IDisposable
{
```

and insert this directly above the first `[AotSmokeFact]` (`Initialize_ShouldAdvertiseTheSameServer`), after the `RequestTimeout` field:

```csharp
    public void Dispose()
    {
        output.WriteLine(servers.DescribeStandardError());
    }

```

The servers are shared, so the text is cumulative. xUnit shows it only under a failing test's `Standard Output Messages`.

- [ ] **Step 7: Prove the stderr reaches a failing test**

```bash
dotnet build tests/ProjGraph.Tests.Smoke.Aot -c Release
printf '#!/bin/sh\necho "boom: native server crashed on startup" >&2\nexit 3\n' > /tmp/fake-mcp && chmod +x /tmp/fake-mcp
GITHUB_ACTIONS=true \
PROJGRAPH_SMOKE_REQUIRED=true \
PROJGRAPH_SMOKE_CLI_NATIVE=artifacts/native/cli/ProjGraph.Cli \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE=/tmp/fake-mcp \
PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.dll \
dotnet test tests/ProjGraph.Tests.Smoke.Aot --no-build -c Release --filter "FullyQualifiedName~McpParityTests" \
  --logger "console;verbosity=normal" > /tmp/fake-mcp.log 2>&1
grep -c "Native server stderr:" /tmp/fake-mcp.log
grep -m1 -A2 "Native server stderr:" /tmp/fake-mcp.log
rm /tmp/fake-mcp
```

Expected: `Failed: 10`. The first `grep` counts at least 10, and the second prints `Native server stderr:` followed by `boom: native server crashed on startup`. If the log is a one-line summary, the command hook rewrote `dotnet` (see Global Constraints): rerun with `"$(readlink -f "$(command -v dotnet)")" test …`.

- [ ] **Step 8: Run the whole suite against the real native builds**

```bash
GITHUB_ACTIONS=true \
PROJGRAPH_SMOKE_REQUIRED=true \
PROJGRAPH_SMOKE_CLI_NATIVE=artifacts/native/cli/ProjGraph.Cli \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE=artifacts/native/mcp/ProjGraph.Mcp \
PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.dll \
dotnet test tests/ProjGraph.Tests.Smoke.Aot --no-build -c Release
dotnet format ProjGraph.slnx --no-restore --verify-no-changes
```

Expected: `Passed: 30, Failed: 0, Skipped: 0`, and format is clean.

- [ ] **Step 9: Commit**

```bash
git add tests/ProjGraph.Tests.Smoke.Aot/NativeBuildGuardTests.cs tests/ProjGraph.Tests.Smoke.Aot/Helpers/McpServerPair.cs tests/ProjGraph.Tests.Smoke.Aot/McpParityTests.cs
git commit -m "test: guard that smoke builds are native and capture MCP server stderr"
```

---

### Task 2: Narrow the CLI's IL3050 suppression

PR 2 put `[UnconditionalSuppressMessage("AOT", "IL3050")]` on all of `Program.Main`. Only `new CommandApp(registrar)` needs it, so it moves to a one-line helper, and a future `RequiresDynamicCode` call elsewhere in `Main` will fail the build again.

**Files:**
- Modify: `src/ProjGraph.Cli/Program.cs`

**Interfaces:**
- Consumes: `TypeRegistrar` (`ProjGraph.Cli.Infrastructure`), `Spectre.Console.Cli.CommandApp`.
- Produces: `private static CommandApp CreateApp(TypeRegistrar registrar)`, used only by `Main`.

- [ ] **Step 1: Move the construction into `CreateApp` without the attribute and watch the build fail**

In `src/ProjGraph.Cli/Program.cs`:

1. Delete the four-line `[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", …)]` attribute above `public static int Main(string[] args)`, and keep the text of its `Justification` for Step 3.
2. Replace:

```csharp
        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
```

with:

```csharp
        var app = CreateApp(new TypeRegistrar(services));
```

3. After the closing brace of `Main`, still inside the class, add:

```csharp

    private static CommandApp CreateApp(TypeRegistrar registrar)
    {
        return new CommandApp(registrar);
    }
```

Run:

```bash
dotnet build src/ProjGraph.Cli -c Release --no-incremental
```

Expected: FAIL with `Program.cs(…): error IL3050: Using member 'Spectre.Console.Cli.CommandApp.CommandApp(ITypeRegistrar)' which has 'RequiresDynamicCodeAttribute'…`, pointing at the `return new CommandApp(registrar);` line. That proves the analyzer runs on every build and that nothing else in `Main` needs the suppression.

- [ ] **Step 2: Put the suppression on `CreateApp`**

Replace the helper with:

```csharp

    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Spectre.Console.Cli reflects over the command and settings types. ProjGraph.Cli and " +
                        "Spectre.Console.Cli are trimmer root assemblies, so that metadata is kept, and " +
                        "tests/ProjGraph.Tests.Smoke.Aot runs every command natively.")]
    private static CommandApp CreateApp(TypeRegistrar registrar)
    {
        return new CommandApp(registrar);
    }
```

`using System.Diagnostics.CodeAnalysis;` is already at the top of the file.

- [ ] **Step 3: Verify**

```bash
dotnet build ProjGraph.slnx -c Release
dotnet test tests/ProjGraph.Tests.Integration.Cli -c Release --no-build
dotnet format ProjGraph.slnx --no-restore --verify-no-changes
```

Expected: 0 warnings, 0 errors, all `Tests.Integration.Cli` tests pass, and format is clean. Task 4 runs the CLI natively with this change.

- [ ] **Step 4: Commit**

```bash
git add src/ProjGraph.Cli/Program.cs
git commit -m "refactor(cli): narrow the IL3050 suppression to CommandApp construction"
```

---

### Task 3: RID-specific Native AOT tool packages

The two tool projects become RID-specific AOT tool packages. The MCP server stops being self-contained, which moves its apphost, so the MCP integration helper and the `aot-smoke` reference path follow.

**Files:**
- Modify: `src/ProjGraph.Cli/ProjGraph.Cli.csproj`
- Modify: `src/ProjGraph.Mcp/ProjGraph.Mcp.csproj`
- Modify: `tests/ProjGraph.Tests.Integration.Mcp/Helpers/McpServerProcess.cs`
- Modify: `.github/workflows/ci.yml` (one path; Task 4 replaces the job)
- Modify: `src/ProjGraph.Cli/README.md`, `src/ProjGraph.Mcp/README.md`, `docfx/guides/getting-started.md`, `CLAUDE.md`

**Interfaces:**
- Produces:
  - Pointer packages `ProjGraph.Cli.<v>.nupkg` and `ProjGraph.Mcp.<v>.nupkg` from `dotnet pack` (no `-r`).
  - Native packages `ProjGraph.<Tool>.<rid>.<v>.nupkg` from `dotnet pack -r <rid>`.
  - Fallback packages `ProjGraph.<Tool>.any.<v>.nupkg` from `dotnet pack -r any -p:PublishAot=false`.
  - JIT reference paths: `src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll` (unchanged) and `src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.dll`/`ProjGraph.Mcp[.exe]` (no longer under `linux-x64/`).
  - Installed command names: `projgraph` and `ProjGraph.Mcp`.

- [ ] **Step 1: Start from a clean MCP build output**

A leftover self-contained apphost under `src/ProjGraph.Mcp/bin/Release/net10.0/<rid>/` would let the old helper pass in Step 3 and hide the break:

```bash
rm -rf src/ProjGraph.Mcp/bin src/ProjGraph.Mcp/obj
```

- [ ] **Step 2: Configure both tool projects**

In `src/ProjGraph.Mcp/ProjGraph.Mcp.csproj`, replace:

```xml
    <!-- Set up the MCP server to be a self-contained application that does not rely on a shared framework -->
    <SelfContained>true</SelfContained>
    <PublishSelfContained>true</PublishSelfContained>

    <!-- Set up the MCP server to be a single file executable -->
    <PublishSingleFile>true</PublishSingleFile>

    <!-- Native AOT is opt-in (-p:PublishAot=true). The analyzers run on every build so first-party
         code stays AOT-clean under warnings-as-errors; third-party ILC warnings must not fail publish. -->
```

with:

```xml
    <!-- Native AOT tool packages. `dotnet pack` builds the pointer package, which lists one package per
         RID below; `dotnet pack -r <rid>` on a matching OS builds each native package, and
         `dotnet pack -r any -p:PublishAot=false` builds the framework-dependent fallback.
         .github/workflows/pack.yml builds them all. Keep linux-musl-* listed with linux-*: without
         them, musl systems resolve the glibc package, which can't start there. -->
    <PublishAot>true</PublishAot>
    <ToolPackageRuntimeIdentifiers>win-x64;linux-x64;linux-arm64;linux-musl-x64;linux-musl-arm64;osx-arm64;any</ToolPackageRuntimeIdentifiers>
    <!-- Native debug symbols (.dbg, .dSYM, .pdb) would more than double the size of each native package. -->
    <CopyOutputSymbolsToPublishDirectory Condition="'$(PublishAot)' == 'true'">false</CopyOutputSymbolsToPublishDirectory>
    <!-- A pointer package holds no assemblies, so packing a symbols package for it fails (NU5017). -->
    <IncludeSymbols>false</IncludeSymbols>

    <!-- The analyzers run on every build so first-party code stays AOT-clean under warnings-as-errors;
         third-party ILC warnings must not fail publish. -->
```

In `src/ProjGraph.Cli/ProjGraph.Cli.csproj`, replace:

```xml
    <!-- Allow the global tool to start on a newer major runtime than it was built against. -->
    <RollForward>LatestMajor</RollForward>

    <!-- Native AOT is opt-in (-p:PublishAot=true). The analyzers run on every build so first-party
         code stays AOT-clean under warnings-as-errors; third-party ILC warnings must not fail publish. -->
```

with:

```xml
    <!-- Lets the framework-dependent `any` package start on a newer major runtime than it was built against. -->
    <RollForward>LatestMajor</RollForward>

    <!-- Native AOT tool packages. `dotnet pack` builds the pointer package, which lists one package per
         RID below; `dotnet pack -r <rid>` on a matching OS builds each native package, and
         `dotnet pack -r any -p:PublishAot=false` builds the framework-dependent fallback.
         .github/workflows/pack.yml builds them all. Keep linux-musl-* listed with linux-*: without
         them, musl systems resolve the glibc package, which can't start there. -->
    <PublishAot>true</PublishAot>
    <ToolPackageRuntimeIdentifiers>win-x64;linux-x64;linux-arm64;linux-musl-x64;linux-musl-arm64;osx-arm64;any</ToolPackageRuntimeIdentifiers>
    <!-- Native debug symbols (.dbg, .dSYM, .pdb) would more than double the size of each native package. -->
    <CopyOutputSymbolsToPublishDirectory Condition="'$(PublishAot)' == 'true'">false</CopyOutputSymbolsToPublishDirectory>
    <!-- A pointer package holds no assemblies, so packing a symbols package for it fails (NU5017). -->
    <IncludeSymbols>false</IncludeSymbols>

    <!-- The analyzers run on every build so first-party code stays AOT-clean under warnings-as-errors;
         third-party ILC warnings must not fail publish. -->
```

Leave everything else in both files as it is: `EnableTrimAnalyzer`, `EnableAotAnalyzer`, `IlcTreatWarningsAsErrors`, `JsonSerializerIsReflectionEnabledByDefault`, the trimmer roots, and the package metadata.

- [ ] **Step 3: Run the MCP integration tests and watch the transport tests fail**

```bash
dotnet build ProjGraph.slnx -c Release
dotnet test tests/ProjGraph.Tests.Integration.Mcp -c Release --no-build
```

Expected: the build has 0 warnings. The tests end with `Failed: 4`, all in `McpTransportTests`, each with `Expected serverExe not to be <null> because the MCP server apphost must be present under …/src/ProjGraph.Mcp/bin/Release/net10.0.` The framework-dependent apphost now sits in that folder itself, and the helper only probes subfolders.

- [ ] **Step 4: Launch the apphost from the build output folder**

In `tests/ProjGraph.Tests.Integration.Mcp/Helpers/McpServerProcess.cs`, replace the `ConnectAsync` summary:

```csharp
    /// <summary>
    /// Connects a client to the server apphost in ProjGraph.Mcp's own build output (guaranteed
    /// up to date by the ProjectReference). ProjGraph.Mcp is a self-contained exe, so its build
    /// lands in a RID subdirectory and must be launched via its apphost — the DLL that the
    /// ProjectReference copies into the test output has no runtime next to it and cannot start.
    /// The client deliberately advertises no capabilities — in particular no workspace roots.
    /// </summary>
```

with:

```csharp
    /// <summary>
    /// Connects a client to the server apphost in ProjGraph.Mcp's own build output (guaranteed
    /// up to date by the ProjectReference). ProjGraph.Mcp builds framework-dependent, so the apphost
    /// sits directly in <c>bin/{Configuration}/{tfm}/</c>; RID subdirectories hold
    /// <c>dotnet pack -r</c> or <c>dotnet publish -r</c> output and are ignored.
    /// The client deliberately advertises no capabilities — in particular no workspace roots.
    /// </summary>
```

and replace the body of `LocateServerExecutable` from `Directory.Exists(binRoot).Should()…` to the end of the method:

```csharp
        Directory.Exists(binRoot).Should().BeTrue(
            $"the MCP server build output must exist at {binRoot}");
        var exeName = OperatingSystem.IsWindows() ? "ProjGraph.Mcp.exe" : "ProjGraph.Mcp";

        // The build RID matches the machine that built it, so probing the RID subdirectories
        // is exact enough without reconstructing the RID by hand. Preferring the most recently
        // written apphost keeps a dev machine with stale cross-RID leftovers deterministic.
        var serverExe = Directory.GetDirectories(binRoot)
            .Select(ridDir => new FileInfo(Path.Combine(ridDir, exeName)))
            .Where(apphost => apphost.Exists)
            .OrderByDescending(apphost => apphost.LastWriteTimeUtc)
            .FirstOrDefault();

        serverExe.Should().NotBeNull($"the MCP server apphost must be present under {binRoot}");
        return serverExe.FullName;
    }
```

with:

```csharp
        var exeName = OperatingSystem.IsWindows() ? "ProjGraph.Mcp.exe" : "ProjGraph.Mcp";
        var serverExe = new FileInfo(Path.Combine(binRoot, exeName));

        serverExe.Exists.Should().BeTrue($"the MCP server apphost must be present at {serverExe.FullName}");
        return serverExe.FullName;
    }
```

- [ ] **Step 5: Run the whole suite**

```bash
dotnet build ProjGraph.slnx -c Release
dotnet test ProjGraph.slnx -c Release --no-build
dotnet format ProjGraph.slnx --no-restore --verify-no-changes
```

Expected: `Tests.Integration.Mcp` passes 150/150, every other suite is green (1,178 passed in total), `Tests.Smoke.Aot` shows `Skipped: 30`, and format is clean.

- [ ] **Step 6: Check the pointer, fallback, and native package contents**

```bash
rm -rf artifacts/pkgcheck src/ProjGraph.Cli/bin/Release/net10.0/linux-x64 src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64
dotnet build ProjGraph.slnx -c Release -p:Version=0.0.0-local.1
dotnet pack ProjGraph.slnx --no-build -c Release -p:Version=0.0.0-local.1 -o artifacts/pkgcheck/solution
dotnet pack src/ProjGraph.Cli -c Release -r any -p:PublishAot=false -p:Version=0.0.0-local.1 -o artifacts/pkgcheck/any
dotnet pack src/ProjGraph.Mcp -c Release -r any -p:PublishAot=false -p:Version=0.0.0-local.1 -o artifacts/pkgcheck/any
dotnet pack src/ProjGraph.Cli -c Release -r linux-x64 -p:Version=0.0.0-local.1 -o artifacts/pkgcheck/native
dotnet pack src/ProjGraph.Mcp -c Release -r linux-x64 -p:Version=0.0.0-local.1 -o artifacts/pkgcheck/native
ls -la artifacts/pkgcheck/*
unzip -l artifacts/pkgcheck/solution/ProjGraph.Mcp.0.0.0-local.1.nupkg
unzip -p artifacts/pkgcheck/solution/ProjGraph.Cli.0.0.0-local.1.nupkg tools/any/any/DotnetToolSettings.xml
unzip -l artifacts/pkgcheck/native/ProjGraph.Cli.linux-x64.0.0.0-local.1.nupkg
grep -c IsDynamicCodeSupported src/ProjGraph.Cli/bin/Release/net10.0/any/ProjGraph.Cli.runtimeconfig.json || true
```

(Run the two `-r linux-x64` packs one at a time; each takes about a minute.)

Expected:
- `solution/` holds the 6 library `.nupkg` + 6 `.snupkg` and exactly two tool packages, `ProjGraph.Cli.0.0.0-local.1.nupkg` and `ProjGraph.Mcp.0.0.0-local.1.nupkg`, with no tool `.snupkg` and no `NU5017` error.
- The MCP pointer lists `.mcp/server.json`, `README.md`, `icon.png`, and `tools/any/any/DotnetToolSettings.xml`. The CLI pointer's settings list 7 `RuntimeIdentifierPackage` entries, one per RID including `any`.
- `any/` holds `ProjGraph.Cli.any.0.0.0-local.1.nupkg` (~8 MB) and `ProjGraph.Mcp.any.0.0.0-local.1.nupkg`.
- `native/` holds `ProjGraph.Cli.linux-x64.0.0.0-local.1.nupkg` (~15 MB) and `ProjGraph.Mcp.linux-x64.0.0.0-local.1.nupkg` (~17 MB), and no `.snupkg`. The CLI package lists `tools/any/linux-x64/ProjGraph.Cli` and **no** `.dbg` file.
- The `grep -c` prints `0`: the fallback build has no AOT feature switches.

- [ ] **Step 7: Check that installing from the packages resolves the native package**

```bash
rm -rf artifacts/pkgcheck/feed artifacts/pkgcheck/tools artifacts/pkgcheck/nuget
mkdir -p artifacts/pkgcheck/feed
cp artifacts/pkgcheck/solution/ProjGraph.Cli.0.0.0-local.1.nupkg artifacts/pkgcheck/native/ProjGraph.Cli.linux-x64.0.0.0-local.1.nupkg artifacts/pkgcheck/feed/
printf '<?xml version="1.0" encoding="utf-8"?>\n<configuration><packageSources><clear /><add key="local" value="." /></packageSources></configuration>\n' > artifacts/pkgcheck/feed/nuget.config
NUGET_PACKAGES="$PWD/artifacts/pkgcheck/nuget" dotnet tool install ProjGraph.Cli --version 0.0.0-local.1 --configfile artifacts/pkgcheck/feed/nuget.config --tool-path artifacts/pkgcheck/tools
ls -la artifacts/pkgcheck/tools
artifacts/pkgcheck/tools/projgraph stats ProjGraph.slnx | head -3
```

Expected: the install succeeds. `projgraph -> .store/projgraph.cli/0.0.0-local.1/projgraph.cli.linux-x64/0.0.0-local.1/tools/any/linux-x64/ProjGraph.Cli`, and `stats` prints the `── ProjGraph.slnx ──` header.

Then check Alpine resolution, if Docker is available (skip this with a note in the task report if it isn't; Task 4 Step 5 covers musl end to end):

```bash
docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp/home -v "$PWD/artifacts/pkgcheck/feed:/feed:ro" \
  mcr.microsoft.com/dotnet/sdk:10.0-alpine \
  sh -c 'mkdir -p /tmp/home && dotnet tool install ProjGraph.Cli --version 0.0.0-local.1 --configfile /feed/nuget.config --tool-path /tmp/t; echo "exit=$?"'
```

Expected: the install **fails** with `Version 0.0.0-local.1 of package projgraph.cli.linux-musl-x64 is not found`. Alpine now asks for the musl package, and only the linux-x64 one is in this feed. (Before this change it silently installed the glibc `linux-x64` package, which then couldn't start.)

```bash
rm -rf artifacts/pkgcheck
```

- [ ] **Step 8: Point `aot-smoke` at the moved MCP reference**

In `.github/workflows/ci.yml`, in the `Smoke test native executables` step, replace:

```yaml
          PROJGRAPH_SMOKE_MCP_REFERENCE: src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.dll
```

with:

```yaml
          PROJGRAPH_SMOKE_MCP_REFERENCE: src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.dll
```

This keeps the job working at this commit; Task 4 replaces the job.

- [ ] **Step 9: Document installation**

In `src/ProjGraph.Cli/README.md`, replace:

````markdown
## Installation

```bash
dotnet tool install -g ProjGraph.Cli
```
````

with:

````markdown
## Installation

```bash
dotnet tool install -g ProjGraph.Cli
```

Installing needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) or later. On Windows x64, Linux x64 and
Arm64 (glibc or musl), and macOS Arm64, the tool is a Native AOT executable that starts without loading the .NET
runtime. Other platforms get a framework-dependent build that runs on the .NET 10 runtime.
````

In `src/ProjGraph.Mcp/README.md`, replace:

````markdown
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

Or run from source:
````

with:

````markdown
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

`dnx` ships with the [.NET 10 SDK](https://dotnet.microsoft.com/download). On Windows x64, Linux x64 and Arm64 (glibc
or musl), and macOS Arm64, it runs a Native AOT build of the server that starts without loading the .NET runtime.
Other platforms get a framework-dependent build that runs on the .NET 10 runtime.

Or run from source:
````

Leave the `<!-- mcp-name: io.github.HandyS11/projgraph -->` comment as the last line of that README.

In `docfx/guides/getting-started.md`, replace:

```markdown
ProjGraph reads source files directly. You do not need to build the project you
are analysing, run a database, or apply migrations first.
```

with:

```markdown
On Windows x64, Linux x64 and Arm64 (glibc or musl), and macOS Arm64, both the
CLI and the MCP server install as Native AOT executables that start without
loading the .NET runtime. Other platforms get a framework-dependent build that
runs on the runtime the SDK includes.

ProjGraph reads source files directly. You do not need to build the project you
are analysing, run a database, or apply migrations first.
```

In `CLAUDE.md`, under `### Key patterns`, insert after the `**MCP stdout safety** — …` paragraph:

```markdown
**Tool packaging** — `ProjGraph.Cli` and `ProjGraph.Mcp` set `PublishAot=true` and `ToolPackageRuntimeIdentifiers`, so a plain `dotnet pack` builds only the pointer package. Each native package needs `dotnet pack -r <rid>` on a matching OS (Alpine for `linux-musl-*`), and the `any` fallback needs `dotnet pack -r any -p:PublishAot=false`. Because `PublishAot` is set, plain JIT builds also get AOT feature switches (e.g. `IsDynamicCodeSupported=false`) in their `runtimeconfig.json`. `.github/workflows/pack.yml` builds every package.
```

- [ ] **Step 10: Commit**

```bash
git add src/ProjGraph.Cli/ProjGraph.Cli.csproj src/ProjGraph.Mcp/ProjGraph.Mcp.csproj tests/ProjGraph.Tests.Integration.Mcp/Helpers/McpServerProcess.cs .github/workflows/ci.yml src/ProjGraph.Cli/README.md src/ProjGraph.Mcp/README.md docfx/guides/getting-started.md CLAUDE.md
git commit -m "feat: pack the CLI and MCP server as RID-specific Native AOT tools"
```

---

### Task 4: Native tool smoke script, composite action, and `aot-smoke`

One script runs the whole per-RID check: build → native pack → install from packages → smoke suite. A composite action runs it on the runner, or inside Alpine for musl, and `aot-smoke` switches to it.

**Files:**
- Create: `.github/scripts/native-tool-smoke.sh`
- Create: `.github/actions/native-tool-smoke/action.yml`
- Modify: `.github/workflows/ci.yml`
- Modify: `CONTRIBUTING.md`, `CLAUDE.md`

**Interfaces:**
- Consumes: Task 1's 30-test suite with `PROJGRAPH_SMOKE_*`, and Task 3's package commands and paths.
- Produces:
  - The script `.github/scripts/native-tool-smoke.sh <rid> <version>`, run from the repository root. It leaves `artifacts/packages/ProjGraph.{Cli,Mcp}.<rid>.<version>.nupkg`, `artifacts/pointers/ProjGraph.{Cli,Mcp}.<version>.nupkg`, and `artifacts/smoke-results/smoke.trx`.
  - The composite action `./.github/actions/native-tool-smoke` with inputs `rid` and `version`. It needs only a checkout beforehand, because it sets up .NET itself.

- [ ] **Step 1: Write the script**

Create `.github/scripts/native-tool-smoke.sh`:

```bash
#!/usr/bin/env bash
# Builds the solution, packs the Native AOT ProjGraph.Cli and ProjGraph.Mcp tool packages for one
# runtime identifier, installs both tools from those packages the way users do, and runs
# tests/ProjGraph.Tests.Smoke.Aot against the JIT build of the same commit.
# Usage: native-tool-smoke.sh <rid> <version>
# Run it from the repository root, on an OS that matches the RID (Alpine for linux-musl-*).
# Leaves the RID-specific packages in artifacts/packages, the pointer packages in artifacts/pointers,
# and the test results in artifacts/smoke-results.
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: $0 <rid> <version>" >&2
  exit 2
fi

rid=$1
version=$2

dotnet restore ProjGraph.slnx
dotnet build ProjGraph.slnx --no-restore --configuration Release -p:Version="$version"

# The pointer packages are only needed to install the tools below; a release publishes the ones
# from the pack-portable job.
dotnet pack src/ProjGraph.Cli --no-build --configuration Release -p:Version="$version" --output artifacts/pointers
dotnet pack src/ProjGraph.Mcp --no-build --configuration Release -p:Version="$version" --output artifacts/pointers

# One native pack at a time: each ILC run takes several GB of memory.
dotnet pack src/ProjGraph.Cli --configuration Release --runtime "$rid" -p:Version="$version" --output artifacts/packages
dotnet pack src/ProjGraph.Mcp --configuration Release --runtime "$rid" -p:Version="$version" --output artifacts/packages

# Only the local packages are sources, so nothing resolves from NuGet.org, and a fresh package folder
# keeps a cached copy of the same version from being installed instead.
cat > artifacts/smoke-nuget.config <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="packages" value="packages" />
    <add key="pointers" value="pointers" />
  </packageSources>
</configuration>
XML
rm -rf artifacts/tools artifacts/tool-install-packages
for tool in Cli Mcp; do
  NUGET_PACKAGES="$PWD/artifacts/tool-install-packages" dotnet tool install "ProjGraph.$tool" --version "$version" \
    --configfile artifacts/smoke-nuget.config --tool-path "artifacts/tools/$tool"
done

exe=''
mcp_reference=src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.dll
if [ "${RUNNER_OS:-}" = 'Windows' ]; then
  exe='.exe'
  # The MCP SDK starts stdio servers through `cmd.exe /c` on Windows, which is fragile with a quoted
  # dotnet host path. The JIT apphost runs the same build without one.
  mcp_reference=src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.exe
fi

PROJGRAPH_SMOKE_REQUIRED=true \
PROJGRAPH_SMOKE_CLI_NATIVE="artifacts/tools/Cli/projgraph$exe" \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE="artifacts/tools/Mcp/ProjGraph.Mcp$exe" \
PROJGRAPH_SMOKE_MCP_REFERENCE="$mcp_reference" \
dotnet test tests/ProjGraph.Tests.Smoke.Aot --no-build --configuration Release \
  --logger "console;verbosity=normal" --logger "trx;LogFileName=smoke.trx" \
  --results-directory artifacts/smoke-results
```

Mark it executable in git, so it also runs as `./.github/scripts/native-tool-smoke.sh`. The workflows call it through `bash`, so Windows doesn't depend on the bit.

```bash
chmod +x .github/scripts/native-tool-smoke.sh
git add .github/scripts/native-tool-smoke.sh
git update-index --chmod=+x .github/scripts/native-tool-smoke.sh
docker run --rm --user "$(id -u):$(id -g)" --entrypoint shellcheck -v "$PWD":/s -w /s rhysd/actionlint:latest .github/scripts/native-tool-smoke.sh
```

Expected: shellcheck prints nothing and exits 0.

- [ ] **Step 2: Write the composite action**

Create `.github/actions/native-tool-smoke/action.yml`:

```yaml
name: Native tool smoke test
description: >-
  Runs .github/scripts/native-tool-smoke.sh for one runtime identifier: builds the solution, packs the
  Native AOT ProjGraph.Cli and ProjGraph.Mcp tool packages, installs both tools from them, and runs
  tests/ProjGraph.Tests.Smoke.Aot against the JIT build of the same commit. linux-musl-* RIDs run
  inside an Alpine .NET SDK container. Expects the repository to be checked out.

inputs:
  rid:
    description: Runtime identifier to pack. The runner must match its OS and architecture (Linux for linux-musl-*).
    required: true
  version:
    description: Package version, passed as -p:Version to every build and pack.
    required: true

runs:
  using: composite
  steps:
    - name: Setup .NET
      if: ${{ !startsWith(inputs.rid, 'linux-musl-') }}
      uses: actions/setup-dotnet@v6
      with:
        global-json-file: global.json
        cache: true
        cache-dependency-path: Directory.Packages.props

    - name: Install Native AOT prerequisites
      if: ${{ runner.os == 'Linux' && !startsWith(inputs.rid, 'linux-musl-') }}
      shell: bash
      run: |
        sudo apt-get update
        sudo apt-get install -y --no-install-recommends clang zlib1g-dev

    - name: Pack and smoke test
      if: ${{ !startsWith(inputs.rid, 'linux-musl-') }}
      shell: bash
      env:
        RID: ${{ inputs.rid }}
        VERSION: ${{ inputs.version }}
      run: bash .github/scripts/native-tool-smoke.sh "$RID" "$VERSION"

    # musl binaries must be linked on a musl system. The container runs as root, so the files it
    # writes are handed back to the runner user even when the script fails.
    - name: Pack and smoke test in Alpine
      if: ${{ startsWith(inputs.rid, 'linux-musl-') }}
      shell: bash
      env:
        RID: ${{ inputs.rid }}
        VERSION: ${{ inputs.version }}
      run: |
        docker run --rm --volume "$PWD:/src" --workdir /src \
          --env RID --env VERSION --env GITHUB_ACTIONS --env DOTNET_CLI_TELEMETRY_OPTOUT=1 \
          mcr.microsoft.com/dotnet/sdk:10.0-alpine \
          sh -c 'apk add --no-cache bash clang build-base zlib-dev &&
                 status=0 &&
                 { bash .github/scripts/native-tool-smoke.sh "$RID" "$VERSION" || status=$?; } &&
                 chown -R "$(stat -c %u:%g /src)" /src &&
                 exit "$status"'

    - name: Upload smoke test results
      if: failure()
      uses: actions/upload-artifact@v7
      with:
        name: smoke-results-${{ inputs.rid }}
        path: artifacts/smoke-results
        if-no-files-found: ignore
        retention-days: 14
```

- [ ] **Step 3: Switch `aot-smoke` to the action**

In `.github/workflows/ci.yml`, replace the whole `aot-smoke` job with:

```yaml
  aot-smoke:
    runs-on: ubuntu-latest
    timeout-minutes: 30

    steps:
      - uses: actions/checkout@v7

      # The same pack → install → smoke path that pack.yml runs for every release platform.
      - name: Pack and smoke test the native tools
        uses: ./.github/actions/native-tool-smoke
        with:
          rid: linux-x64
          version: 0.0.0-ci.${{ github.run_number }}
```

Lint both files:

```bash
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD":/repo -w /repo rhysd/actionlint:latest -color=false .github/workflows/ci.yml
```

Expected: no output, exit 0. actionlint also validates the local action's inputs.

- [ ] **Step 4: Run the script on linux-x64 from a clean clone**

A clone only sees committed state, so commit first. Keep `GITHUB_ACTIONS=true`, because Spectre.Console turns on ANSI output under it (a PR 2 lesson):

```bash
git add .github/actions/native-tool-smoke/action.yml .github/workflows/ci.yml
git commit -m "ci: smoke-test native tools through their packages"
rm -rf /tmp/pg-pr3-host && git clone --quiet --branch feat/aot-packaging . /tmp/pg-pr3-host
cd /tmp/pg-pr3-host
GITHUB_ACTIONS=true bash .github/scripts/native-tool-smoke.sh linux-x64 0.0.0-local.2
ls artifacts/packages artifacts/pointers
ls -la artifacts/tools/Cli artifacts/tools/Mcp
cd - && rm -rf /tmp/pg-pr3-host
```

Expected:
- The run ends with `Passed: 30, Failed: 0, Skipped: 0`.
- `artifacts/packages` holds `ProjGraph.Cli.linux-x64.0.0.0-local.2.nupkg` and `ProjGraph.Mcp.linux-x64.0.0.0-local.2.nupkg`.
- `artifacts/pointers` holds the two pointer packages.
- `projgraph` and `ProjGraph.Mcp` are symlinks into `.store/…linux-x64…`.

- [ ] **Step 5: Run the script for linux-musl-x64 in Alpine, exactly as the action does**

This takes several minutes: the container restores every package from NuGet.org and runs two native packs. Don't run it at the same time as Step 4.

```bash
rm -rf /tmp/pg-pr3-musl && git clone --quiet --branch feat/aot-packaging . /tmp/pg-pr3-musl
cd /tmp/pg-pr3-musl
RID=linux-musl-x64 VERSION=0.0.0-local.3 GITHUB_ACTIONS=true \
docker run --rm --volume "$PWD:/src" --workdir /src \
  --env RID --env VERSION --env GITHUB_ACTIONS --env DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  mcr.microsoft.com/dotnet/sdk:10.0-alpine \
  sh -c 'apk add --no-cache bash clang build-base zlib-dev &&
         status=0 &&
         { bash .github/scripts/native-tool-smoke.sh "$RID" "$VERSION" || status=$?; } &&
         chown -R "$(stat -c %u:%g /src)" /src &&
         exit "$status"'
echo "exit=$?"
ls -la artifacts/packages
cd - && rm -rf /tmp/pg-pr3-musl
```

Expected: `Passed: 30, Failed: 0, Skipped: 0`, `exit=0`, and `ProjGraph.{Cli,Mcp}.linux-musl-x64.0.0.0-local.3.nupkg` owned by your user, not root. If this machine can't run Docker, say so in the task report; Task 7's PR run then provides the first musl evidence.

- [ ] **Step 6: Update the contributor docs**

In `CONTRIBUTING.md`, replace the `Tests.Smoke.Aot` row of the test table with the row below, and re-pad the table's columns so the `|` separators stay aligned:

```markdown
| `Tests.Smoke.Aot`       | Native AOT vs JIT parity for the CLI and MCP tools (skipped unless `PROJGRAPH_SMOKE_*` is set; CI runs it through `.github/actions/native-tool-smoke`) |
```

Then replace the whole `### Native AOT smoke tests` section, up to the next `## Adding a New Feature` heading, with:

````markdown
### Native AOT smoke tests

`Tests.Smoke.Aot` compares Native AOT builds of the CLI and MCP server with the JIT build of the
same commit. It is skipped unless the `PROJGRAPH_SMOKE_*` variables are set. CI runs
`.github/scripts/native-tool-smoke.sh` through the `.github/actions/native-tool-smoke` action: it
packs the native tool packages, installs them with `dotnet tool install`, and tests the installed
tools. The `aot-smoke` job does this for linux-x64 on every PR, and `pack.yml` does it for every
release platform. Native AOT on Linux needs `clang` (or `gcc`) and `zlib1g-dev`.

To run the same check locally on Linux:

```bash
bash .github/scripts/native-tool-smoke.sh linux-x64 0.0.0-local.1
```

For a quicker loop, test published binaries instead (run one publish at a time; each takes several GB
of memory):

```bash
dotnet build ProjGraph.slnx -c Release
dotnet publish src/ProjGraph.Cli -c Release -r linux-x64 -o artifacts/native/cli
dotnet publish src/ProjGraph.Mcp -c Release -r linux-x64 -o artifacts/native/mcp
PROJGRAPH_SMOKE_CLI_NATIVE=artifacts/native/cli/ProjGraph.Cli \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE=artifacts/native/mcp/ProjGraph.Mcp \
PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.dll \
dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build
```
````

In `CLAUDE.md`, replace the `Tests.Smoke.Aot` row of the test projects table with:

```markdown
| `Tests.Smoke.Aot` | Native AOT vs JIT parity for the installed CLI and MCP tools (skipped unless `PROJGRAPH_SMOKE_*` is set; `.github/scripts/native-tool-smoke.sh` runs it in the `aot-smoke` CI job and for every RID in `pack.yml`) |
```

- [ ] **Step 7: Verify the quick publish loop from CONTRIBUTING**

Run the second code block above from the repository, one publish at a time. Record hashes of the JIT build first, so the check also proves an in-place `-r linux-x64` publish leaves the JIT reference alone:

```bash
dotnet build ProjGraph.slnx -c Release
find src/ProjGraph.Cli/bin/Release/net10.0 src/ProjGraph.Mcp/bin/Release/net10.0 -maxdepth 1 -type f -exec sha256sum {} + | sort > /tmp/jit-before.txt
dotnet publish src/ProjGraph.Cli -c Release -r linux-x64 -o artifacts/native/cli
dotnet publish src/ProjGraph.Mcp -c Release -r linux-x64 -o artifacts/native/mcp
find src/ProjGraph.Cli/bin/Release/net10.0 src/ProjGraph.Mcp/bin/Release/net10.0 -maxdepth 1 -type f -exec sha256sum {} + | sort > /tmp/jit-after.txt
diff /tmp/jit-before.txt /tmp/jit-after.txt && echo "JIT build untouched"
PROJGRAPH_SMOKE_CLI_NATIVE=artifacts/native/cli/ProjGraph.Cli \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE=artifacts/native/mcp/ProjGraph.Mcp \
PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.dll \
dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build
rm -f /tmp/jit-before.txt /tmp/jit-after.txt
```

Expected: `JIT build untouched`, `Passed: 30`, and no `.dbg` in `artifacts/native/cli`. If the diff isn't empty, don't document the in-place publish: restore `--artifacts-path artifacts/aot-build` in both commands, both here and in `CONTRIBUTING.md`.

- [ ] **Step 8: Commit**

```bash
git add CONTRIBUTING.md CLAUDE.md
git commit -m "docs: describe the package-based Native AOT smoke check"
```

---

### Task 5: `pack.yml` and the package-set check

The reusable workflow that builds every release package. A strict package-set check in each job catches a missing or extra package before `publish` sees it.

**Files:**
- Create: `.github/scripts/verify-packages.sh`
- Create: `.github/workflows/pack.yml`

**Interfaces:**
- Consumes: the composite action from Task 4, and Task 3's pack commands.
- Produces:
  - Workflow `./.github/workflows/pack.yml` with `workflow_call` inputs `ref` (string, required) and `version` (string, required).
  - Artifacts `packages-<rid>` for each of the six RIDs, each holding two `.nupkg`.
  - Artifact `packages-portable`: 6 library `.nupkg` + 6 `.snupkg` + 2 `any` `.nupkg`.
  - Artifact `pointers`: the 2 pointer `.nupkg`.
  - The script `.github/scripts/verify-packages.sh <directory> <version> <set>...` with sets `libraries`, `pointers`, `any`, and each of the six RIDs.

- [ ] **Step 1: Write the package-set check**

Create `.github/scripts/verify-packages.sh`:

```bash
#!/usr/bin/env bash
# Fails unless a directory holds exactly the .nupkg files of the named package sets.
# Usage: verify-packages.sh <directory> <version> <set>...
# Sets: libraries, pointers, any, win-x64, linux-x64, linux-arm64, linux-musl-x64, linux-musl-arm64, osx-arm64.
# Keep it Bash 3.2 compatible: the osx-arm64 pack job runs it on macOS.
set -euo pipefail

if [ "$#" -lt 3 ]; then
  echo "usage: $0 <directory> <version> <set>..." >&2
  exit 2
fi

directory=$1
version=$2
shift 2

expected=""
add() {
  expected="$expected$1.$version.nupkg"$'\n'
}

for set in "$@"; do
  case "$set" in
    libraries)
      for id in ProjGraph.Core ProjGraph.Lib ProjGraph.Lib.Core ProjGraph.Lib.Dependencies \
                ProjGraph.Lib.ClassDiagram ProjGraph.Lib.EntityFramework; do
        add "$id"
      done
      ;;
    pointers)
      add ProjGraph.Cli
      add ProjGraph.Mcp
      ;;
    any | win-x64 | linux-x64 | linux-arm64 | linux-musl-x64 | linux-musl-arm64 | osx-arm64)
      add "ProjGraph.Cli.$set"
      add "ProjGraph.Mcp.$set"
      ;;
    *)
      echo "::error::Unknown package set '$set'." >&2
      exit 2
      ;;
  esac
done

expected=$(printf '%s' "$expected" | sort)
actual=$(for package in "$directory"/*.nupkg; do
  if [ -e "$package" ]; then basename "$package"; fi
done | sort)

if [ "$actual" != "$expected" ]; then
  echo "::error::$directory doesn't hold exactly the packages for: $*" >&2
  diff <(printf '%s\n' "$expected") <(printf '%s\n' "$actual") >&2 || true
  exit 1
fi

echo "$directory holds the $(printf '%s\n' "$actual" | wc -l | tr -d ' ') expected packages."
```

```bash
chmod +x .github/scripts/verify-packages.sh
git add .github/scripts/verify-packages.sh
git update-index --chmod=+x .github/scripts/verify-packages.sh
docker run --rm --user "$(id -u):$(id -g)" --entrypoint shellcheck -v "$PWD":/s -w /s rhysd/actionlint:latest .github/scripts/verify-packages.sh
```

- [ ] **Step 2: Test the check against fake package folders**

```bash
rm -rf /tmp/vp && mkdir -p /tmp/vp/ok /tmp/vp/pointers /tmp/vp/empty
for id in ProjGraph.Core ProjGraph.Lib ProjGraph.Lib.Core ProjGraph.Lib.Dependencies ProjGraph.Lib.ClassDiagram ProjGraph.Lib.EntityFramework ProjGraph.Cli.any ProjGraph.Mcp.any; do touch "/tmp/vp/ok/$id.1.2.3-rc.1.nupkg"; done
touch /tmp/vp/ok/ProjGraph.Core.1.2.3-rc.1.snupkg /tmp/vp/pointers/ProjGraph.Cli.1.2.3-rc.1.nupkg /tmp/vp/pointers/ProjGraph.Mcp.1.2.3-rc.1.nupkg
bash .github/scripts/verify-packages.sh /tmp/vp/ok 1.2.3-rc.1 libraries any; echo "1 exit=$? (want 0)"
bash .github/scripts/verify-packages.sh /tmp/vp/pointers 1.2.3-rc.1 pointers; echo "2 exit=$? (want 0)"
bash .github/scripts/verify-packages.sh /tmp/vp/ok 1.2.3-rc.1 libraries any linux-musl-x64; echo "3 exit=$? (want 1, diff lists the two musl packages)"
bash .github/scripts/verify-packages.sh /tmp/vp/ok 1.2.3-rc.1 libraries; echo "4 exit=$? (want 1, diff lists the two extra any packages)"
bash .github/scripts/verify-packages.sh /tmp/vp/empty 1.2.3-rc.1 pointers; echo "5 exit=$? (want 1)"
bash .github/scripts/verify-packages.sh /tmp/vp/ok 1.2.3-rc.1 bogus; echo "6 exit=$? (want 2)"
rm -rf /tmp/vp
```

Expected: exit codes 0, 0, 1, 1, 1, 2. Runs 1–2 print `… holds the 8 expected packages.` and `… holds the 2 expected packages.`; the `.snupkg` doesn't count.

- [ ] **Step 3: Write the workflow**

Create `.github/workflows/pack.yml`:

```yaml
name: Pack

# Builds every package a release ships: two Native AOT tool packages per runtime identifier, each
# smoke-tested on a matching runner, plus the libraries, the framework-dependent fallback packages,
# and the pointer packages. publish.yml calls it for a tag. It also runs on pull requests that
# change packaging, so every native build is proven before a release depends on it.
on:
  workflow_call:
    inputs:
      ref:
        description: Git ref to check out (the release tag).
        required: true
        type: string
      version:
        description: Package version.
        required: true
        type: string
  pull_request:
    paths:
      - .github/workflows/pack.yml
      - .github/actions/native-tool-smoke/**
      - .github/scripts/native-tool-smoke.sh
      - .github/scripts/verify-packages.sh
      - src/ProjGraph.Cli/ProjGraph.Cli.csproj
      - src/ProjGraph.Mcp/ProjGraph.Mcp.csproj
      - tests/ProjGraph.Tests.Smoke.Aot/**
      - Directory.Build.props
      - Directory.Packages.props
      - global.json

permissions:
  contents: read

concurrency:
  group: pack-${{ github.event_name }}-${{ github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

env:
  # Pull request runs have no version input; any version works as long as every command uses it.
  VERSION: ${{ inputs.version || format('0.0.0-ci.{0}', github.run_number) }}

jobs:
  pack-native:
    name: pack-native (${{ matrix.rid }})
    runs-on: ${{ matrix.runner }}
    timeout-minutes: 45
    strategy:
      fail-fast: false
      matrix:
        include:
          - rid: win-x64
            runner: windows-latest
          - rid: linux-x64
            runner: ubuntu-latest
          - rid: linux-arm64
            runner: ubuntu-24.04-arm
          - rid: linux-musl-x64
            runner: ubuntu-latest
          - rid: linux-musl-arm64
            runner: ubuntu-24.04-arm
          - rid: osx-arm64
            runner: macos-latest

    steps:
      - uses: actions/checkout@v7
        with:
          ref: ${{ inputs.ref }}

      - name: Pack and smoke test the native tools
        uses: ./.github/actions/native-tool-smoke
        with:
          rid: ${{ matrix.rid }}
          version: ${{ env.VERSION }}

      - name: Verify the native packages
        shell: bash
        env:
          RID: ${{ matrix.rid }}
        run: bash .github/scripts/verify-packages.sh artifacts/packages "$VERSION" "$RID"

      - name: Upload native packages
        uses: actions/upload-artifact@v7
        with:
          name: packages-${{ matrix.rid }}
          path: artifacts/packages/*.nupkg
          if-no-files-found: error
          retention-days: 7

  pack-portable:
    runs-on: ubuntu-latest
    timeout-minutes: 30

    steps:
      - uses: actions/checkout@v7
        with:
          ref: ${{ inputs.ref }}

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json
          cache: true
          cache-dependency-path: Directory.Packages.props

      - name: Build
        run: |
          dotnet restore ProjGraph.slnx
          dotnet build ProjGraph.slnx --no-restore --configuration Release -p:Version="$VERSION"

      # With PublishAot set, packing the solution produces the RID-agnostic pointer packages for the
      # two tools; they move to their own folder because publish.yml pushes them last.
      - name: Pack libraries and pointer packages
        run: |
          dotnet pack ProjGraph.slnx --no-build --configuration Release -p:Version="$VERSION" --output artifacts/packages
          mkdir -p artifacts/pointers
          mv "artifacts/packages/ProjGraph.Cli.$VERSION.nupkg" "artifacts/packages/ProjGraph.Mcp.$VERSION.nupkg" artifacts/pointers/

      - name: Pack framework-dependent fallback packages
        run: |
          dotnet pack src/ProjGraph.Cli --configuration Release --runtime any -p:PublishAot=false -p:Version="$VERSION" --output artifacts/packages
          dotnet pack src/ProjGraph.Mcp --configuration Release --runtime any -p:PublishAot=false -p:Version="$VERSION" --output artifacts/packages

      - name: Verify the portable packages
        run: |
          bash .github/scripts/verify-packages.sh artifacts/packages "$VERSION" libraries any
          bash .github/scripts/verify-packages.sh artifacts/pointers "$VERSION" pointers

      # The fallback packages are JIT builds, so the native-build guard is filtered out; every parity
      # test still runs them against the JIT build of the same commit.
      - name: Smoke test the fallback packages
        env:
          PROJGRAPH_SMOKE_REQUIRED: 'true'
          PROJGRAPH_SMOKE_CLI_NATIVE: artifacts/fallback/cli/tools/net10.0/any/ProjGraph.Cli.dll
          PROJGRAPH_SMOKE_CLI_REFERENCE: src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll
          PROJGRAPH_SMOKE_MCP_NATIVE: artifacts/fallback/mcp/tools/net10.0/any/ProjGraph.Mcp.dll
          PROJGRAPH_SMOKE_MCP_REFERENCE: src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.dll
        run: |
          mkdir -p artifacts/fallback
          unzip -q "artifacts/packages/ProjGraph.Cli.any.$VERSION.nupkg" -d artifacts/fallback/cli
          unzip -q "artifacts/packages/ProjGraph.Mcp.any.$VERSION.nupkg" -d artifacts/fallback/mcp
          dotnet test tests/ProjGraph.Tests.Smoke.Aot --no-build --configuration Release \
            --filter "FullyQualifiedName!~ProjGraph.Tests.Smoke.Aot.NativeBuildGuardTests" \
            --logger "console;verbosity=normal"

      - name: Upload libraries and fallback packages
        uses: actions/upload-artifact@v7
        with:
          name: packages-portable
          path: |
            artifacts/packages/*.nupkg
            artifacts/packages/*.snupkg
          if-no-files-found: error
          retention-days: 7

      - name: Upload pointer packages
        uses: actions/upload-artifact@v7
        with:
          name: pointers
          path: artifacts/pointers/*.nupkg
          if-no-files-found: error
          retention-days: 7
```

```bash
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD":/repo -w /repo rhysd/actionlint:latest -color=false .github/workflows/pack.yml .github/workflows/ci.yml
```

Expected: no output, exit 0.

- [ ] **Step 4: Run `pack-portable`'s commands from a clean clone**

```bash
git add .github/workflows/pack.yml
git commit -m "ci: add the pack workflow for native, fallback, and pointer packages"
rm -rf /tmp/pg-pr3-portable && git clone --quiet --branch feat/aot-packaging . /tmp/pg-pr3-portable
cd /tmp/pg-pr3-portable
export VERSION=0.0.0-local.4
dotnet restore ProjGraph.slnx
dotnet build ProjGraph.slnx --no-restore --configuration Release -p:Version="$VERSION"
dotnet pack ProjGraph.slnx --no-build --configuration Release -p:Version="$VERSION" --output artifacts/packages
mkdir -p artifacts/pointers
mv "artifacts/packages/ProjGraph.Cli.$VERSION.nupkg" "artifacts/packages/ProjGraph.Mcp.$VERSION.nupkg" artifacts/pointers/
dotnet pack src/ProjGraph.Cli --configuration Release --runtime any -p:PublishAot=false -p:Version="$VERSION" --output artifacts/packages
dotnet pack src/ProjGraph.Mcp --configuration Release --runtime any -p:PublishAot=false -p:Version="$VERSION" --output artifacts/packages
bash .github/scripts/verify-packages.sh artifacts/packages "$VERSION" libraries any
bash .github/scripts/verify-packages.sh artifacts/pointers "$VERSION" pointers
mkdir -p artifacts/fallback
unzip -q "artifacts/packages/ProjGraph.Cli.any.$VERSION.nupkg" -d artifacts/fallback/cli
unzip -q "artifacts/packages/ProjGraph.Mcp.any.$VERSION.nupkg" -d artifacts/fallback/mcp
GITHUB_ACTIONS=true \
PROJGRAPH_SMOKE_REQUIRED=true \
PROJGRAPH_SMOKE_CLI_NATIVE=artifacts/fallback/cli/tools/net10.0/any/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE=artifacts/fallback/mcp/tools/net10.0/any/ProjGraph.Mcp.dll \
PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/ProjGraph.Mcp.dll \
dotnet test tests/ProjGraph.Tests.Smoke.Aot --no-build --configuration Release \
  --filter "FullyQualifiedName!~ProjGraph.Tests.Smoke.Aot.NativeBuildGuardTests"
unset VERSION
cd - && rm -rf /tmp/pg-pr3-portable
```

Expected: `… holds the 8 expected packages.`, `… holds the 2 expected packages.`, and `Passed: 28, Failed: 0, Skipped: 0`. The step's commands in `pack.yml` must match these exactly.

- [ ] **Step 5: Commit**

Already committed in Step 4 together with the workflow. If Step 4 needed fixes, commit them now:

```bash
git add .github/workflows/pack.yml .github/scripts/verify-packages.sh
git commit -m "ci: fix the pack workflow"
```

---

### Task 6: `publish.yml` job graph and ordered pushes

`publish.yml` becomes `prepare` → `pack` (calls `pack.yml`) → `publish`. The `publish` job pushes the pointer packages last, and only after NuGet.org lists every tool sub-package.

**Files:**
- Create: `.github/scripts/wait-for-nuget.sh`
- Modify: `.github/workflows/publish.yml`
- Modify: `ARCHITECTURE.md`, `CLAUDE.md`

**Interfaces:**
- Consumes: `pack.yml`'s inputs and artifacts (Task 5), and `verify-packages.sh` (Task 5).
- Produces: `.github/scripts/wait-for-nuget.sh <version> <package-id>...`, with `NUGET_WAIT_TIMEOUT_SECONDS` (default 1800) and `NUGET_WAIT_INTERVAL_SECONDS` (default 30).

- [ ] **Step 1: Write the NuGet.org wait**

Create `.github/scripts/wait-for-nuget.sh`:

```bash
#!/usr/bin/env bash
# Waits until NuGet.org's flat container lists a version for every package ID, so a pointer package
# is pushed only once every package it points to can be installed.
# Usage: wait-for-nuget.sh <version> <package-id>...
# NUGET_WAIT_TIMEOUT_SECONDS (default 1800) and NUGET_WAIT_INTERVAL_SECONDS (default 30) set the timing.
set -euo pipefail

if [ "$#" -lt 2 ]; then
  echo "usage: $0 <version> <package-id>..." >&2
  exit 2
fi

version=$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')
shift
timeout=${NUGET_WAIT_TIMEOUT_SECONDS:-1800}
interval=${NUGET_WAIT_INTERVAL_SECONDS:-30}
deadline=$((SECONDS + timeout))
pending=("$@")

while :; do
  waiting=()
  for id in "${pending[@]}"; do
    lower_id=$(printf '%s' "$id" | tr '[:upper:]' '[:lower:]')
    if curl --silent --fail "https://api.nuget.org/v3-flatcontainer/$lower_id/index.json" |
      jq --exit-status --arg version "$version" '.versions | index($version) != null' > /dev/null; then
      echo "$id $version is listed on NuGet.org."
    else
      waiting+=("$id")
    fi
  done

  if [ "${#waiting[@]}" -eq 0 ]; then
    exit 0
  fi

  if [ "$SECONDS" -ge "$deadline" ]; then
    echo "::error::Timed out after ${timeout}s waiting for $version of: ${waiting[*]}" >&2
    exit 1
  fi

  echo "Waiting ${interval}s for: ${waiting[*]}"
  sleep "$interval"
  pending=("${waiting[@]}")
done
```

```bash
chmod +x .github/scripts/wait-for-nuget.sh
git add .github/scripts/wait-for-nuget.sh
git update-index --chmod=+x .github/scripts/wait-for-nuget.sh
docker run --rm --user "$(id -u):$(id -g)" --entrypoint shellcheck -v "$PWD":/s -w /s rhysd/actionlint:latest .github/scripts/wait-for-nuget.sh
```

- [ ] **Step 2: Test the wait against live NuGet.org**

It only reads NuGet.org's public flat container and publishes nothing:

```bash
NUGET_WAIT_TIMEOUT_SECONDS=5 NUGET_WAIT_INTERVAL_SECONDS=2 bash .github/scripts/wait-for-nuget.sh 1.1.0 ProjGraph.Cli ProjGraph.Mcp; echo "1 exit=$? (want 0)"
NUGET_WAIT_TIMEOUT_SECONDS=3 NUGET_WAIT_INTERVAL_SECONDS=2 bash .github/scripts/wait-for-nuget.sh 1.1.0 ProjGraph.Cli ProjGraph.Cli.linux-x64; echo "2 exit=$? (want 1)"
NUGET_WAIT_TIMEOUT_SECONDS=3 NUGET_WAIT_INTERVAL_SECONDS=2 bash .github/scripts/wait-for-nuget.sh 9.9.9 ProjGraph.Cli; echo "3 exit=$? (want 1)"
```

Expected:
1. Both IDs are `listed on NuGet.org`, exit 0.
2. `ProjGraph.Cli` is listed, then `Waiting 2s for: ProjGraph.Cli.linux-x64` (twice), then `::error::Timed out after 3s …`, exit 1.
3. Times out, exit 1.

If `ProjGraph.Cli.linux-x64` has been published by the time you run this, pick any unpublished ID for case 2.

- [ ] **Step 3: Rewrite `publish.yml`**

Replace the whole file with:

```yaml
name: Publish

on:
  push:
    tags:
      - 'v*'
  # Allow re-running a release on demand (e.g. after a transient NuGet/MCP Registry failure).
  # The tag must already exist: every job below derives the version from it. Pushes skip packages
  # that are already on a feed, and the NuGet.org wait passes at once for packages already listed.
  workflow_dispatch:
    inputs:
      tag:
        description: 'Existing release tag to publish (e.g. v1.0.3)'
        required: true
        type: string

permissions:
  contents: write
  packages: write
  id-token: write # Required for MCP Registry OIDC authentication

jobs:
  prepare:
    runs-on: ubuntu-latest
    outputs:
      tag: ${{ steps.get_version.outputs.tag }}
      version: ${{ steps.get_version.outputs.version }}

    steps:
      - name: Validate tag input
        if: github.event_name == 'workflow_dispatch'
        env:
          TAG: ${{ github.event.inputs.tag }}
        run: |
          if [[ ! "$TAG" =~ ^v[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
            echo "::error::'$TAG' is not a valid release tag (expected e.g. v1.0.3 or v1.0.0-rc.1)."
            exit 1
          fi

      - uses: actions/checkout@v7
        with:
          fetch-depth: 0
          ref: ${{ github.event.inputs.tag || github.ref }}

      - name: Extract version from tag
        id: get_version
        env:
          TAG: ${{ github.event.inputs.tag || github.ref_name }}
        run: |
          {
            echo "tag=$TAG"
            echo "version=${TAG#v}"
          } >> "$GITHUB_OUTPUT"
          echo "Tag: $TAG / Version: ${TAG#v}"

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json
          cache: true
          cache-dependency-path: Directory.Packages.props

      - name: Restore dependencies
        run: dotnet restore ProjGraph.slnx

      - name: Build
        env:
          VERSION: ${{ steps.get_version.outputs.version }}
        run: dotnet build ProjGraph.slnx --no-restore --configuration Release -p:Version="$VERSION"

      - name: Test
        run: dotnet test ProjGraph.slnx --no-build --configuration Release --verbosity normal

  pack:
    needs: prepare
    uses: ./.github/workflows/pack.yml
    with:
      ref: ${{ needs.prepare.outputs.tag }}
      version: ${{ needs.prepare.outputs.version }}

  publish:
    needs: [prepare, pack]
    runs-on: ubuntu-latest
    env:
      VERSION: ${{ needs.prepare.outputs.version }}

    steps:
      - uses: actions/checkout@v7
        with:
          ref: ${{ needs.prepare.outputs.tag }}

      - name: Download packages
        uses: actions/download-artifact@v8
        with:
          pattern: packages-*
          path: artifacts/packages
          merge-multiple: true

      - name: Download pointer packages
        uses: actions/download-artifact@v8
        with:
          name: pointers
          path: artifacts/pointers

      - name: Verify the release package set
        run: |
          bash .github/scripts/verify-packages.sh artifacts/packages "$VERSION" libraries any win-x64 linux-x64 linux-arm64 linux-musl-x64 linux-musl-arm64 osx-arm64
          bash .github/scripts/verify-packages.sh artifacts/pointers "$VERSION" pointers

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json

      # A pointer package names its RID-specific packages, and installing it fails until they exist, so
      # every other package goes first.
      - name: Publish packages to NuGet.org
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
        run: dotnet nuget push "artifacts/packages/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key "$NUGET_API_KEY" --skip-duplicate

      - name: Publish packages to GitHub Packages
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          OWNER: ${{ github.repository_owner }}
        run: dotnet nuget push "artifacts/packages/*.nupkg" --source "https://nuget.pkg.github.com/$OWNER/index.json" --api-key "$GITHUB_TOKEN" --skip-duplicate

      - name: Wait for the tool packages on NuGet.org
        run: |
          bash .github/scripts/wait-for-nuget.sh "$VERSION" \
            ProjGraph.Cli.any ProjGraph.Cli.win-x64 ProjGraph.Cli.linux-x64 ProjGraph.Cli.linux-arm64 \
            ProjGraph.Cli.linux-musl-x64 ProjGraph.Cli.linux-musl-arm64 ProjGraph.Cli.osx-arm64 \
            ProjGraph.Mcp.any ProjGraph.Mcp.win-x64 ProjGraph.Mcp.linux-x64 ProjGraph.Mcp.linux-arm64 \
            ProjGraph.Mcp.linux-musl-x64 ProjGraph.Mcp.linux-musl-arm64 ProjGraph.Mcp.osx-arm64

      - name: Publish pointer packages to NuGet.org
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
        run: dotnet nuget push "artifacts/pointers/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key "$NUGET_API_KEY" --skip-duplicate

      - name: Publish pointer packages to GitHub Packages
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          OWNER: ${{ github.repository_owner }}
        run: dotnet nuget push "artifacts/pointers/*.nupkg" --source "https://nuget.pkg.github.com/$OWNER/index.json" --api-key "$GITHUB_TOKEN" --skip-duplicate

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v3
        with:
          files: |
            artifacts/packages/*
            artifacts/pointers/*
          tag_name: ${{ needs.prepare.outputs.tag }}
          generate_release_notes: true
          draft: false
          # Treat a tag with a pre-release suffix (e.g. v1.0.0-rc.1) as a pre-release.
          prerelease: ${{ contains(needs.prepare.outputs.tag, '-') }}

      - name: Update server.json version
        run: |
          sed -i "s/\"version\": \"[^\"]\+\"/\"version\": \"$VERSION\"/g" src/ProjGraph.Mcp/.mcp/server.json
          echo "Updated server.json:"
          grep "\"version\"" src/ProjGraph.Mcp/.mcp/server.json

      - name: Install mcp-publisher
        run: |
          curl -L "https://github.com/modelcontextprotocol/registry/releases/latest/download/mcp-publisher_$(uname -s | tr '[:upper:]' '[:lower:]')_$(uname -m | sed 's/x86_64/amd64/;s/aarch64/arm64/').tar.gz" | tar -xzf -

      - name: Wait for NuGet.org validation
        run: sleep 300

      - name: Publish to MCP Registry
        uses: nick-fields/retry@v4
        with:
          timeout_minutes: 15
          max_attempts: 5
          retry_wait_seconds: 120
          command: |
            ./mcp-publisher login github-oidc
            ./mcp-publisher publish src/ProjGraph.Mcp/.mcp/server.json
```

What changed compared with the old file, so a reviewer can check it quickly:
- The `Update Directory.Build.props version` step is gone. Every build and pack gets `-p:Version` instead, because jobs don't share a workspace.
- `server.json` is still edited with `sed`, now only in `publish` (spec 3.2), and `identifier` stays `ProjGraph.Mcp`, which is now the pointer package.
- The wait lists all 14 tool sub-packages, and pointer pushes come after it.

```bash
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD":/repo -w /repo rhysd/actionlint:latest -color=false .github/workflows/publish.yml .github/workflows/pack.yml .github/workflows/ci.yml
```

Expected: no output, exit 0. The old file's `SC2086` infos are gone.

- [ ] **Step 4: Check the push order and package counts by reading**

With `publish.yml` open, confirm:
- (a) No step before `Wait for the tool packages on NuGet.org` touches `artifacts/pointers` except the download and verify steps.
- (b) The wait's ID list has 14 entries and matches `verify-packages.sh`'s `any` and six RID sets.
- (c) The verify step's sets (`libraries any` + 6 RIDs = 16 files, `pointers` = 2) add up to the 22 packages in Global Constraints.

Every list must use the same RID spellings. A mismatch here would only surface on a release tag.

```bash
grep -o 'linux-musl-arm64' .github/workflows/pack.yml .github/workflows/publish.yml .github/scripts/verify-packages.sh src/ProjGraph.Cli/ProjGraph.Cli.csproj src/ProjGraph.Mcp/ProjGraph.Mcp.csproj | sort | uniq -c
```

Expected: `pack.yml` has 1, `publish.yml` 3 (verify line + 2 wait IDs), `verify-packages.sh` 2 (comment + case), and each csproj 1.

- [ ] **Step 5: Update the release docs**

In `ARCHITECTURE.md`, replace the `### Release Flow` section (the heading and its code block, up to `### MCP Registry Ownership Verification`) with:

````markdown
### Release Flow

```sh
Tag push (v*)
    │
    ├── prepare: dotnet build -p:Version=<tag version> + dotnet test
    ├── pack (.github/workflows/pack.yml)
    │     ├── pack-native ×6, each on a matching runner (linux-musl-* inside Alpine):
    │     │     win-x64 · linux-x64 · linux-arm64 · linux-musl-x64 · linux-musl-arm64 · osx-arm64
    │     │     dotnet pack -r <rid> → dotnet tool install from the packages → Tests.Smoke.Aot
    │     └── pack-portable: libraries, pointer packages, framework-dependent `any` packages → Tests.Smoke.Aot
    └── publish
          ├── dotnet nuget push libraries + native + any → NuGet.org, GitHub Packages
          ├── wait until NuGet.org lists all 14 tool packages (30 min timeout)
          ├── dotnet nuget push pointer packages → NuGet.org, GitHub Packages
          ├── Create GitHub Release with every package attached
          └── mcp-publisher publish (GitHub OIDC auth, no token required)
                └── Submits src/ProjGraph.Mcp/.mcp/server.json to the Official MCP Registry
```

`pack.yml` also runs on pull requests that change the tool projects, the smoke suite, the packaging
scripts, `Directory.*.props`, or `global.json`.

### Tool Packages

`ProjGraph.Cli` and `ProjGraph.Mcp` are pointer packages that list one package per runtime identifier.
`dotnet tool install` and `dnx` pick the Native AOT package on win-x64, linux-x64, linux-arm64,
linux-musl-x64, linux-musl-arm64, and osx-arm64, and the framework-dependent `.any` package elsewhere.
Installing a pointer package fails until every package it lists is on the feed, which is why `publish`
pushes it last.

If a native package misbehaves after a release, remove its RID from `ToolPackageRuntimeIdentifiers` in
both tool projects and ship a patch; that platform then falls back to `any`. Remove a `linux-<arch>`
RID together with its `linux-musl-<arch>` RID, and never a musl RID alone. The RID graph treats musl
as compatible with glibc, so musl systems would install the glibc package, which can't start there.
````

In `CLAUDE.md`, replace the paragraph under `### Release`:

```markdown
Releases are triggered by pushing a `v*` tag. The publish workflow builds, packs, pushes to NuGet.org and GitHub Packages, submits to the MCP Registry via `mcp-publisher`, and creates a GitHub Release. The MCP Registry ownership comment (`<!-- mcp-name: io.github.HandyS11/projgraph -->`) must remain at the end of `src/ProjGraph.Mcp/README.md`.
```

with:

```markdown
Releases are triggered by pushing a `v*` tag. `publish.yml` builds and tests with `-p:Version` from the tag, then calls `pack.yml`. That workflow packs and smoke-tests the Native AOT tool packages on a matching runner for each RID (Alpine for `linux-musl-*`) and packs the libraries, the `any` fallbacks, and the two pointer packages. `publish` then pushes everything except the pointer packages to NuGet.org and GitHub Packages, waits until NuGet.org lists all 14 tool packages, pushes the pointer packages, creates a GitHub Release, and submits to the MCP Registry via `mcp-publisher`. See `ARCHITECTURE.md` ("Tool Packages") before changing `ToolPackageRuntimeIdentifiers`. The MCP Registry ownership comment (`<!-- mcp-name: io.github.HandyS11/projgraph -->`) must remain at the end of `src/ProjGraph.Mcp/README.md`.
```

- [ ] **Step 6: Commit**

```bash
git add .github/scripts/wait-for-nuget.sh .github/workflows/publish.yml ARCHITECTURE.md CLAUDE.md
git commit -m "ci: publish native tool packages before their pointer packages"
```

---

### Task 7: Pull request and validation on every platform

Pushing and opening the PR are outward-facing: confirm with the user first. Nothing in this task publishes a package. `pack.yml` runs on the PR with a `0.0.0-ci.<n>` version and only uploads workflow artifacts.

- [ ] **Step 1: Final local gate**

```bash
dotnet build ProjGraph.slnx -c Release
dotnet format ProjGraph.slnx --no-restore --verify-no-changes
dotnet test ProjGraph.slnx -c Release --no-build
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD":/repo -w /repo rhysd/actionlint:latest -color=false .github/workflows/ci.yml .github/workflows/pack.yml .github/workflows/publish.yml
docker run --rm --user "$(id -u):$(id -g)" --entrypoint shellcheck -v "$PWD":/s -w /s rhysd/actionlint:latest .github/scripts/native-tool-smoke.sh .github/scripts/verify-packages.sh .github/scripts/wait-for-nuget.sh
git ls-files -s .github/scripts
grep -rnE '\b(dtk|rtk) ' .github CONTRIBUTING.md CLAUDE.md ARCHITECTURE.md
git status --short
```

Expected:
- 0 warnings, format clean, and every suite green with `Tests.Smoke.Aot` `Skipped: 30`.
- Both linters silent.
- All three scripts show mode `100755`.
- The `grep` prints nothing.
- A clean working tree.

- [ ] **Step 2: Push and open the PR (after user confirmation)**

Write the description to `/tmp/pr3-description.md` (with the Write tool), then:

```bash
git push -u origin feat/aot-packaging
gh pr create --base develop --title "feat: Native AOT tool packages and release pipeline (PR 3 of 3)" --body-file /tmp/pr3-description.md
```

The description must include:
- A summary: the two tool projects as RID-specific AOT packages with an `any` fallback, the shared smoke script/action, `pack.yml`, and the new `publish.yml` order.
- The 12 "Deviations from the spec" above. Deviation 1 goes first and cites the Alpine reproduction.
- The package sizes, and that the release now ships 22 packages.
- The PR 2 follow-ups that are included (guard tests, stderr capture, `CreateApp`, split steps, TRX upload), and the one that's obsolete (chmod on extracted binaries).
- The maintainer checklist from Step 5 below.
- It ends with the attribution line from the session's system reminder.

- [ ] **Step 3: Watch the checks**

```bash
gh pr checks --watch
```

Expected checks:
- The three `build` matrix jobs.
- `aot-smoke`.
- `pack-native (win-x64)`, `pack-native (linux-x64)`, `pack-native (linux-arm64)`, `pack-native (linux-musl-x64)`, `pack-native (linux-musl-arm64)`, and `pack-native (osx-arm64)`.
- `pack-portable`.

Each `pack-native` log ends its smoke step with `Passed: 30, Failed: 0, Skipped: 0` and `… holds the 2 expected packages.`. `pack-portable` shows the `8` and `2` package counts and `Passed: 28`.

- [ ] **Step 4: Triage runner-only failures (use superpowers:systematic-debugging; read the log and the `smoke-results-<rid>` artifact before changing anything)**

The platforms below were never run locally. Likely causes and the fix to try, in order:

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| `pack-native (win-x64)`: `NativeBuildGuardTests` fails on `artifacts/tools/Cli/projgraph.exe`, or the smoke tests can't start it | On Windows the tool-path command may be a launcher rather than the native executable | Point the native variables at the executable in the tool store: `find artifacts/tools/Cli/.store -type f -name ProjGraph.Cli.exe -path '*/tools/*'`, and the same for `ProjGraph.Mcp.exe`. Set the variables from those paths in the script's Windows branch, then add a deviation note |
| `pack-native (win-x64)`: the MCP reference apphost exits at once, "You must install .NET" | The apphost can't find the runtime | Export `DOTNET_ROOT` from the directory of `command -v dotnet` in the script's Windows branch |
| Windows output differs only in line endings or path separators | Both builds should see the same environment, so this is a real finding, not noise | Find which build differs and why; don't normalize it away without understanding it |
| `pack-native (linux-musl-arm64)`: `docker: … no matching manifest` or Docker isn't available | Arm64 hosted runner image issue | Confirm with `docker info` in a debug step. If Docker is missing there, stop and ask the user: running the job in `container:` doesn't work, because JavaScript actions don't run in Alpine containers on Arm64 |
| musl legs: `apk add` fails | Transient mirror error | Re-run the job; if it persists, pin the image tag (e.g. `10.0-alpine3.22`) |
| `pack-native (osx-arm64)`: ILC link errors | Xcode command-line tools | Read the linker message; `macos-latest` ships Xcode, so a missing SDK is unexpected |
| Any leg: `NU1101`/`not found in NuGet feeds` during `dotnet tool install` | The pointer and native packages don't have the same version | Check that the script's `-p:Version` is identical for the build, pointer pack, and native pack |

Commit fixes with focused messages. Re-run until every check is green.

- [ ] **Step 5: Hand the post-merge release checks to the maintainer**

These publish public packages or change settings, so they belong to the user. Put them in the PR description as a checklist:

1. **NuGet.org API key scope.** Confirm that the key behind `NUGET_API_KEY` can push the 14 new package IDs, i.e. its glob covers `ProjGraph.*` (spec risk table). If it lists explicit package IDs, the release fails at the first native push.
2. **Make `aot-smoke` required** on `develop`. This is still pending from PR 2; the ruleset has no required status checks yet.
3. **RC dry run** (spec Testing table). Push a pre-release tag such as `v1.2.0-rc.1` on the merge commit and watch `publish.yml`. The `publish` log must show:
   - `… holds the 16 expected packages.` and `… holds the 2 expected packages.`;
   - the package pushes;
   - `is listed on NuGet.org.` for all 14 IDs;
   - only then the pointer pushes, the GitHub Release, and the MCP Registry publish.
4. **Install checks with the RC version**, on one machine per native RID:
   - `dotnet tool install -g ProjGraph.Cli --version <rc>`, then `projgraph --help`.
   - `(printf '%s\n' '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"rc-check","version":"1.0"}}}'; sleep 10) | dnx ProjGraph.Mcp@<rc> --yes`, which must print a result whose `serverInfo.version` starts with `<rc>`.
   - On Linux and macOS, `ls -l ~/.dotnet/tools/projgraph` shows the link into `…projgraph.cli.<rid>…`.
   - For musl: `docker run --rm mcr.microsoft.com/dotnet/sdk:10.0-alpine sh -c 'dotnet tool install -g ProjGraph.Cli --version <rc> && ~/.dotnet/tools/projgraph --help'`.
5. **Fallback check.** On an unlisted platform (e.g. an Intel Mac, `osx-x64`, or Windows on Arm, `win-arm64`), the same install must resolve `projgraph.cli.any` and run.
6. **MCP Registry.** `curl -s "https://registry.modelcontextprotocol.io/v0/servers?search=io.github.HandyS11/projgraph"` lists the RC version.

## Exit criteria (spec, PR 3, with deviation 1)

- **This PR:**
  - Every check above is green, including `pack-native` on all six RIDs and `pack-portable`.
  - Local suites are green; actionlint and shellcheck are clean.
- **After merge (maintainer):**
  - The RC dry run publishes all 22 packages in the specified order, and the release smoke passes on all six RIDs.
  - `dnx`/`dotnet tool install` work on each native RID and on a fallback platform.
  - The MCP Registry entry resolves.
