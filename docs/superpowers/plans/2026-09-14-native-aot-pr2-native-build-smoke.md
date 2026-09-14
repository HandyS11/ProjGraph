# Native AOT PR 2: Native Build and PR Smoke Test Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `ProjGraph.Cli` and `ProjGraph.Mcp` publish as working Native AOT executables on demand, and add a CI job that checks the native binaries against the JIT build of the same commit on every PR.

**Architecture:** AOT stays opt-in: no csproj sets `PublishAot`, so `dotnet pack` and `publish.yml` don't change. The CLI gets trimmer root assemblies for Spectre.Console.Cli's reflection, two justified suppressions, and build-time AOT/trim analyzers. The MCP server gets the analyzers and stops treating third-party ILC warnings as errors. A new test project, `ProjGraph.Tests.Smoke.Aot`, launches a native and a JIT build side by side and requires identical output. It skips unless four environment variables point at the builds, so the existing CI matrix and local `dotnet test` are unaffected. A new `aot-smoke` job on `ubuntu-latest` publishes both executables and runs the suite.

**Tech Stack:** .NET 10 (SDK per `global.json`; verified on 10.0.401), C#, xUnit 2.9 + FluentAssertions 8, ModelContextProtocol 2.2.0 (stdio client), Spectre.Console.Cli 0.55.0, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-14-native-aot-design.md` (section "PR 2: native build and PR smoke test"). Read it before starting. PR 1 (`docs/superpowers/plans/2026-09-14-native-aot-pr1-library-preparation.md`) is merged as `86082ea`.

## Global Constraints

- `TreatWarningsAsErrors=true`, `AnalysisMode=All`, and `EnforceCodeStyleInBuild=true` apply to every project, tests included. Sonar, Roslynator, VS Threading, and CA analyzers all run during build. Any warning is a build failure. Private members that have a `<summary>` also need their `<param>`/`<returns>`/`<exception>` elements (RCS1140/RCS1141).
- XML documentation is required on all public APIs (`GenerateDocumentationFile=true`). Test projects have CS1591 disabled, but the Roslynator rules above still apply to any doc comment you write.
- CI runs `dotnet format ProjGraph.slnx --no-restore --verify-no-changes`, which must pass.
- Package versions are central (`Directory.Packages.props`). This PR adds no package.
- **No csproj sets `PublishAot`.** AOT is enabled only with `-p:PublishAot=true` on the command line (spec PR 2 intro, exit criterion).
- No change to any command, option, MCP tool, prompt, resource, or output format (spec Non-goals).
- Smoke-test oracle: native output is compared against the JIT build of the **same commit**, never against committed samples (spec decision 7). Don't regenerate samples.
- Don't edit `ProjGraph.slnx` with `dotnet sln add`: it rewrites every `<File Path="..."/>` as `<File Path="..." />`. Insert the one line by hand.
- Always pass `--artifacts-path` to a native `dotnet publish` (see deviation 2). Run native publishes one at a time: each ILC run uses several GB of RAM, and back-to-back publishes on a 16 GB machine have been OOM-killed.

## Deviations from the spec (intentional; mention them in the PR description)

1. **No `PublishSingleFile` condition on the MCP project.** Spec §2.1 makes `PublishSingleFile` conditional on `PublishAot != true`. Verified on SDK 10.0.401: with it left unconditional, the AOT publish succeeds and produces the same 42,946,344-byte binary, so the condition is a no-op. PR 3 removes `PublishSingleFile` entirely.
2. **`--artifacts-path` on the native publish.** `ProjGraph.Mcp` is self-contained, so its `dotnet build` output already lives in `bin/Release/net10.0/linux-x64/`. An in-place `dotnet publish -r linux-x64 -p:PublishAot=true` reuses that directory: it deletes the JIT apphost and rewrites `ProjGraph.Mcp.runtimeconfig.json` with AOT feature switches (`IsDynamicCodeSupported=false`, …), which corrupts the JIT reference. With `--artifacts-path`, all 301 `ProjGraph.*` files under `src/*/bin` and `tests/*/bin` were byte-identical before and after both publishes.
3. **Reference variables accept a `.dll` or an executable.** A path ending in `.dll` is started through the `dotnet` host (`DOTNET_HOST_PATH` when `dotnet test` provides it); anything else is started directly. The MCP reference is `src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.dll`: running a self-contained build's `.dll` through `dotnet` works (verified).
4. **A fifth variable, `PROJGRAPH_SMOKE_REQUIRED`.** When it is `true`, `[AotSmokeFact]` never skips, so a missing or misspelled variable in CI fails the required job instead of passing it with 26 skipped tests.
5. **Standard error is compared too, and `--output` runs share one path.** The CLI writes status, warnings, and `Saved to <path>` to stderr. Spectre wraps that line at 80 columns when output is redirected (verified), so replacing two different temp paths can't make stderr comparable. Both builds write to the same path one after the other, and the file is read and deleted in between.
6. **Reference outcomes are pinned.** Every CLI case asserts that the reference exits 0 (or non-zero for the two failure cases), and every MCP tool case asserts that the reference returns `isError != true`. Otherwise a wrong path that makes both builds fail identically would pass as parity.
7. **Extra cases.** PR 1's final review showed that only exe-launching tests catch reflection-off JSON bugs, and that parity needs edge inputs:
   - CLI: a solution with a malformed project (`visualize` and `stats`), a `.sln` that lists the same project twice (fails on both builds), and a class diagram using `ConcurrentDictionary`/`Barrier`/`[Required]` (bound only through the embedded reference assemblies).
   - MCP: `get_project_stats` on a solution with a malformed project (the `warnings` array), `prompts/get` (serializes `IEnumerable<ChatMessage>` through `McpJsonContext`), and server capabilities in the handshake.
8. **Parameter name.** The spec's MCP case says `options.inheritance`; the real parameter is `options.includeInheritance`.
9. **Shared MCP servers.** Disposing a stdio `McpClient` takes about 5 s in SDK 2.2.0 (the existing `McpTransportTests` pay this too; the servers themselves exit at once on stdin EOF). One native and one reference server are shared across the MCP test class. That brings the whole suite from 1 min 51 s to about 20 s, and it mirrors how clients hold one long session.
10. **Legacy `.sln` fixture.** Besides the five `modular-architecture` projects, it contains a solution folder, a `NestedProjects` section, and one classic C# project-type GUID, so the SolutionPersistence filtering path runs natively.

## Verified facts this plan relies on

All of these were checked on 2026-09-14 in a throwaway worktree at `86082ea` (linux-x64, SDK 10.0.401, no clang installed; ILC fell back to gcc):

- Unmodified CLI, `dotnet publish -r linux-x64 -p:PublishAot=true`: fails with `Program.cs(26,19): error IL3050` (`CommandApp(ITypeRegistrar)` has `RequiresDynamicCodeAttribute`) and `DependencyInjection.cs(29,9): error IL2067` (`TypeRegistrar.Register` → `AddSingleton(IServiceCollection, Type, Type)`). `-p:IlcTreatWarningsAsErrors=false` doesn't help, because these come from the compile-time analyzers.
- CLI with both suppressions but **no** `TrimmerRootAssembly`: publishes, then every command crashes with `Unhandled exception. Spectre.Console.Cli.CommandRuntimeException: Could not get settings type for command of type 'ProjGraph.Cli.Commands.VisualizeCommand'.` and exit code 134.
- Unmodified MCP, AOT publish: fails with ILC errors `IL2104` (Microsoft.CodeAnalysis produced trim warnings) and `IL3000` (`CommonCompiler.GetAssemblyLocation`). With `IlcTreatWarningsAsErrors=false` it publishes and works (PR 1 already fixed the runtime blockers).
- The MCP project builds clean with `EnableTrimAnalyzer`/`EnableAotAnalyzer` on. The CLI reports only the two diagnostics above.
- With the configuration in Tasks 2–3: the CLI binary is ~36.6 MB and the MCP binary ~42.9 MB. All 26 smoke tests pass native vs JIT. The suite also passes JIT apphost vs JIT `.dll`, skips all 26 tests with no variables set, and fails when the "native" CLI is `/bin/true`.
- `stats` output differs between runs only in the `Analysis time` row. `stats ProjGraph.slnx --tpo 3` exits 255 on both builds and prints `Error: Unknown option 'tpo'.` to stdout.
- `artifacts/` is already in `.gitignore`; `out/` is not.

## File Structure

| File | Change | Responsibility |
| --- | --- | --- |
| `tests/ProjGraph.Tests.Smoke.Aot/ProjGraph.Tests.Smoke.Aot.csproj` | Create | Test project; references only `Tests.Shared` (no product projects) |
| `tests/ProjGraph.Tests.Smoke.Aot/Helpers/SmokeCommand.cs` | Create | How to start a build: executable directly, `.dll` via `dotnet` |
| `tests/ProjGraph.Tests.Smoke.Aot/Helpers/SmokeEnvironment.cs` | Create | Reads the `PROJGRAPH_SMOKE_*` variables; finds the repository root |
| `tests/ProjGraph.Tests.Smoke.Aot/Helpers/AotSmokeFactAttribute.cs` | Create | `[AotSmokeFact]`: skips unless configured (or required) |
| `tests/ProjGraph.Tests.Smoke.Aot/Helpers/ProcessRunner.cs` | Create | Runs a CLI build from the repo root, captures exit code/stdout/stderr |
| `tests/ProjGraph.Tests.Smoke.Aot/Helpers/McpServerPair.cs` | Create | Lazily started native + reference MCP clients shared by a test class |
| `tests/ProjGraph.Tests.Smoke.Aot/Fixtures/legacy.sln` | Create | Legacy `.sln` referencing the `modular-architecture` sample projects |
| `tests/ProjGraph.Tests.Smoke.Aot/CliParityTests.cs` | Create | 16 CLI parity cases |
| `tests/ProjGraph.Tests.Smoke.Aot/McpParityTests.cs` | Create | 10 MCP parity cases |
| `ProjGraph.slnx` | Modify | Adds the smoke project |
| `src/ProjGraph.Cli/ProjGraph.Cli.csproj` | Modify | Analyzers, `IlcTreatWarningsAsErrors=false`, trimmer root assemblies |
| `src/ProjGraph.Cli/Program.cs` | Modify | IL3050 suppression on `Main` |
| `src/ProjGraph.Cli/Infrastructure/DependencyInjection.cs` | Modify | IL2067 suppression on `TypeRegistrar.Register` |
| `src/ProjGraph.Mcp/ProjGraph.Mcp.csproj` | Modify | Analyzers, `IlcTreatWarningsAsErrors=false` |
| `.github/workflows/ci.yml` | Modify | New `aot-smoke` job |
| `CLAUDE.md`, `CONTRIBUTING.md`, `ARCHITECTURE.md` | Modify | List the smoke project; explain how to run it locally |

---

### Task 1: Smoke test harness and CLI parity cases

The harness and the CLI cases, verified without any native build: the JIT apphost is compared with the JIT `.dll`. Those are two launch paths for the same code, so they must agree, and a deliberately different "native" binary must fail.

**Files:**
- Create: `tests/ProjGraph.Tests.Smoke.Aot/ProjGraph.Tests.Smoke.Aot.csproj`
- Create: `tests/ProjGraph.Tests.Smoke.Aot/Helpers/SmokeCommand.cs`
- Create: `tests/ProjGraph.Tests.Smoke.Aot/Helpers/SmokeEnvironment.cs`
- Create: `tests/ProjGraph.Tests.Smoke.Aot/Helpers/AotSmokeFactAttribute.cs`
- Create: `tests/ProjGraph.Tests.Smoke.Aot/Helpers/ProcessRunner.cs`
- Create: `tests/ProjGraph.Tests.Smoke.Aot/Fixtures/legacy.sln`
- Create: `tests/ProjGraph.Tests.Smoke.Aot/CliParityTests.cs`
- Modify: `ProjGraph.slnx`, `CLAUDE.md`, `CONTRIBUTING.md`, `ARCHITECTURE.md`

**Interfaces:**
- Consumes: `ProjGraph.Tests.Shared.Helpers.TestDirectory` (`DirectoryPath`, `CreateFile(string relativePath, string content)` which creates subdirectories and returns the full path, `IDisposable`).
- Produces (used by Task 3):
  - `internal sealed record SmokeCommand(string FileName, IReadOnlyList<string> LeadingArguments)` with `static SmokeCommand For(string path)`.
  - `internal static class SmokeEnvironment` with `string RepositoryRoot`, `bool IsRequired`, `SmokeCommand CliNative`, `CliReference`, `McpNative`, `McpReference`, `IReadOnlyList<string> GetMissingVariables()`, and `string GetRootPath(string relativePath)`.
  - `public sealed class AotSmokeFactAttribute : FactAttribute`.
  - `internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)` and `internal static class ProcessRunner` with `Task<ProcessResult> RunAsync(SmokeCommand command, IReadOnlyList<string> arguments)`.

- [ ] **Step 1: Create the feature branch and commit this plan**

```bash
git switch develop
git pull --ff-only
git switch -c feat/aot-native-smoke
git add docs/superpowers/plans/2026-09-14-native-aot-pr2-native-build-smoke.md
git commit -m "docs: add the Native AOT PR 2 implementation plan"
```

(If the plan file isn't in the working tree, it was already committed. Skip the `git add`/`commit`.)

- [ ] **Step 2: Create the project file**

Create `tests/ProjGraph.Tests.Smoke.Aot/ProjGraph.Tests.Smoke.Aot.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector"/>
    <PackageReference Include="FluentAssertions"/>
    <PackageReference Include="ModelContextProtocol"/>
    <PackageReference Include="Microsoft.NET.Test.Sdk"/>
    <PackageReference Include="xunit"/>
    <PackageReference Include="xunit.runner.visualstudio"/>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit"/>
    <Using Include="FluentAssertions"/>
  </ItemGroup>

  <!-- No reference to ProjGraph.Cli or ProjGraph.Mcp: the suite only launches the builds it is
       pointed at through the PROJGRAPH_SMOKE_* environment variables. -->
  <ItemGroup>
    <ProjectReference Include="..\ProjGraph.Tests.Shared\ProjGraph.Tests.Shared.csproj"/>
  </ItemGroup>

</Project>
```

Then add one line to `ProjGraph.slnx` by hand, between the `Tests.Shared` and `Tests.Unit.ClassDiagram` entries (keep the file's `"/>` style):

```xml
    <Project Path="tests/ProjGraph.Tests.Shared/ProjGraph.Tests.Shared.csproj"/>
    <Project Path="tests/ProjGraph.Tests.Smoke.Aot/ProjGraph.Tests.Smoke.Aot.csproj"/>
    <Project Path="tests/ProjGraph.Tests.Unit.ClassDiagram/ProjGraph.Tests.Unit.ClassDiagram.csproj"/>
```

- [ ] **Step 3: Create the helpers**

Create `tests/ProjGraph.Tests.Smoke.Aot/Helpers/SmokeCommand.cs`:

```csharp
namespace ProjGraph.Tests.Smoke.Aot.Helpers;

/// <summary>
/// How to start one build of a tool: an executable is started directly, and a framework-dependent
/// <c>.dll</c> is started through the <c>dotnet</c> host.
/// </summary>
/// <param name="FileName">The program to start.</param>
/// <param name="LeadingArguments">Arguments that precede the tool's own arguments.</param>
internal sealed record SmokeCommand(string FileName, IReadOnlyList<string> LeadingArguments)
{
    /// <summary>
    /// Creates the command that starts the build at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The absolute path to an executable or a <c>.dll</c>.</param>
    /// <returns>The command.</returns>
    public static SmokeCommand For(string path)
    {
        if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new SmokeCommand(path, []);
        }

        // dotnet test exports the host that runs the tests, so the reference build runs on the same SDK.
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        return new SmokeCommand(string.IsNullOrEmpty(host) ? "dotnet" : host, [path]);
    }
}
```

Create `tests/ProjGraph.Tests.Smoke.Aot/Helpers/SmokeEnvironment.cs`:

```csharp
namespace ProjGraph.Tests.Smoke.Aot.Helpers;

/// <summary>
/// The smoke suite's configuration: the four builds under comparison, read from environment
/// variables, and the repository root that every command runs from.
/// </summary>
internal static class SmokeEnvironment
{
    private const string CliNativeVariable = "PROJGRAPH_SMOKE_CLI_NATIVE";
    private const string CliReferenceVariable = "PROJGRAPH_SMOKE_CLI_REFERENCE";
    private const string McpNativeVariable = "PROJGRAPH_SMOKE_MCP_NATIVE";
    private const string McpReferenceVariable = "PROJGRAPH_SMOKE_MCP_REFERENCE";
    private const string RequiredVariable = "PROJGRAPH_SMOKE_REQUIRED";

    private static readonly string[] RequiredVariables =
        [CliNativeVariable, CliReferenceVariable, McpNativeVariable, McpReferenceVariable];

    private static readonly Lazy<string> LazyRepositoryRoot = new(FindRepositoryRoot);

    /// <summary>
    /// Gets the repository root: the nearest ancestor of the test output directory that contains
    /// <c>ProjGraph.slnx</c>.
    /// </summary>
    public static string RepositoryRoot => LazyRepositoryRoot.Value;

    /// <summary>
    /// Gets a value indicating whether the suite must run. CI sets <c>PROJGRAPH_SMOKE_REQUIRED=true</c>,
    /// so a misspelled or missing variable fails the job instead of skipping every test.
    /// </summary>
    public static bool IsRequired =>
        string.Equals(Environment.GetEnvironmentVariable(RequiredVariable), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets the native CLI executable.</summary>
    public static SmokeCommand CliNative => Resolve(CliNativeVariable);

    /// <summary>Gets the JIT CLI build of the same commit.</summary>
    public static SmokeCommand CliReference => Resolve(CliReferenceVariable);

    /// <summary>Gets the native MCP server executable.</summary>
    public static SmokeCommand McpNative => Resolve(McpNativeVariable);

    /// <summary>Gets the JIT MCP server build of the same commit.</summary>
    public static SmokeCommand McpReference => Resolve(McpReferenceVariable);

    /// <summary>
    /// Lists the required variables that are unset or empty.
    /// </summary>
    /// <returns>The missing variable names, empty when the suite is fully configured.</returns>
    public static IReadOnlyList<string> GetMissingVariables()
    {
        return
        [
            .. RequiredVariables.Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
        ];
    }

    /// <summary>
    /// Resolves an absolute path under the repository root.
    /// </summary>
    /// <param name="relativePath">A path relative to the repository root, using <c>/</c> separators.</param>
    /// <returns>The absolute path.</returns>
    public static string GetRootPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(RepositoryRoot, relativePath));
    }

    private static SmokeCommand Resolve(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{variable} is not set.");
        }

        // A configured path that does not exist is a broken CI setup, never a reason to skip.
        var path = Path.GetFullPath(value, RepositoryRoot);
        return File.Exists(path)
            ? SmokeCommand.For(path)
            : throw new FileNotFoundException($"{variable} points to a file that does not exist.", path);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ProjGraph.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No ProjGraph.slnx found above {AppContext.BaseDirectory}.");
    }
}
```

Create `tests/ProjGraph.Tests.Smoke.Aot/Helpers/AotSmokeFactAttribute.cs` (the explicit `AttributeUsage` is required by Sonar S3993):

```csharp
namespace ProjGraph.Tests.Smoke.Aot.Helpers;

/// <summary>
/// A fact that runs only when all four <c>PROJGRAPH_SMOKE_*</c> variables are set, so a plain
/// <c>dotnet test ProjGraph.slnx</c> reports the smoke suite as skipped instead of failing it. When
/// <c>PROJGRAPH_SMOKE_REQUIRED=true</c>, nothing is skipped and a missing variable fails the test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AotSmokeFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AotSmokeFactAttribute"/> class.
    /// </summary>
    public AotSmokeFactAttribute()
    {
        var missing = SmokeEnvironment.GetMissingVariables();
        if (missing.Count > 0 && !SmokeEnvironment.IsRequired)
        {
            Skip = $"Native AOT smoke suite not configured; set {string.Join(", ", missing)}.";
        }
    }
}
```

Create `tests/ProjGraph.Tests.Smoke.Aot/Helpers/ProcessRunner.cs`:

```csharp
using System.Diagnostics;
using System.Text;

namespace ProjGraph.Tests.Smoke.Aot.Helpers;

/// <summary>
/// The observable result of one CLI invocation.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Everything written to standard output.</param>
/// <param name="StandardError">Everything written to standard error.</param>
internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Runs a build of the CLI from the repository root and captures its output.
/// </summary>
internal static class ProcessRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Runs <paramref name="command"/> with <paramref name="arguments"/> and waits for it to exit.
    /// </summary>
    /// <param name="command">The build to run.</param>
    /// <param name="arguments">The CLI arguments.</param>
    /// <returns>The exit code and the captured output streams.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the process cannot be started.</exception>
    /// <exception cref="TimeoutException">Thrown when the process does not exit in time; it is killed.</exception>
    public static async Task<ProcessResult> RunAsync(SmokeCommand command, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(command.FileName)
        {
            WorkingDirectory = SmokeEnvironment.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false
        };
        foreach (var argument in command.LeadingArguments.Concat(arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException($"Could not start {command.FileName}.");
        using var timeout = new CancellationTokenSource(Timeout);

        // Both streams are drained concurrently so a full pipe buffer can never block the child.
        var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var standardError = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"'{command.FileName} {string.Join(' ', arguments)}' did not exit within {Timeout}.");
        }

        return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
    }
}
```

- [ ] **Step 4: Create the legacy `.sln` fixture**

Create `tests/ProjGraph.Tests.Smoke.Aot/Fixtures/legacy.sln` with exactly this content (tabs inside `Global`; `.gitattributes` normalizes `*.sln` line endings). The project paths are relative to the fixture directory:

```text
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "modular-architecture", "modular-architecture", "{0A5E0000-0000-4000-8000-000000000001}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "App", "..\..\..\samples\visualize\modular-architecture\App\App.csproj", "{0A5E0000-0000-4000-8000-000000000002}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Core", "..\..\..\samples\visualize\modular-architecture\Core\Core.csproj", "{0A5E0000-0000-4000-8000-000000000003}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Infrastructure", "..\..\..\samples\visualize\modular-architecture\Infrastructure\Infrastructure.csproj", "{0A5E0000-0000-4000-8000-000000000004}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Services", "..\..\..\samples\visualize\modular-architecture\Services\Services.csproj", "{0A5E0000-0000-4000-8000-000000000005}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Shared", "..\..\..\samples\visualize\modular-architecture\Shared\Shared.csproj", "{0A5E0000-0000-4000-8000-000000000006}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{0A5E0000-0000-4000-8000-000000000002} = {0A5E0000-0000-4000-8000-000000000001}
		{0A5E0000-0000-4000-8000-000000000003} = {0A5E0000-0000-4000-8000-000000000001}
	EndGlobalSection
EndGlobal
```

Sanity check after the build in Step 6: `dotnet src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll visualize tests/ProjGraph.Tests.Smoke.Aot/Fixtures/legacy.sln --format mermaid` exits 0 and prints a graph titled `legacy.sln` with the five nodes `App`, `Core`, `Infrastructure`, `Services`, `Shared`.

- [ ] **Step 5: Write the CLI parity cases**

Create `tests/ProjGraph.Tests.Smoke.Aot/CliParityTests.cs`:

```csharp
using ProjGraph.Tests.Shared.Helpers;
using ProjGraph.Tests.Smoke.Aot.Helpers;
using System.Text.RegularExpressions;

namespace ProjGraph.Tests.Smoke.Aot;

/// <summary>
/// Runs CLI commands through the native executable and through the JIT build of the same commit,
/// and requires the same exit code, standard output, standard error, and written diagram file.
/// </summary>
public sealed partial class CliParityTests : IDisposable
{
    private const string ModularSolution = "samples/visualize/modular-architecture/ModularArchitecture.slnx";

    private readonly TestDirectory _temp = new();

    public void Dispose()
    {
        _temp.Dispose();
    }

    [AotSmokeFact]
    public async Task Visualize_Mermaid_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("visualize", ModularSolution, "--format", "mermaid");
    }

    [AotSmokeFact]
    public async Task Visualize_Tree_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("visualize", ModularSolution, "--format", "tree");
    }

    [AotSmokeFact]
    public async Task Visualize_Flat_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("visualize", ModularSolution, "--format", "flat");
    }

    [AotSmokeFact]
    public async Task Visualize_LegacySln_ShouldMatchReference()
    {
        await AssertDiagramParityAsync(
            "visualize", "tests/ProjGraph.Tests.Smoke.Aot/Fixtures/legacy.sln", "--format", "mermaid");
    }

    [AotSmokeFact]
    public async Task Erd_ComplexEcommerce_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("erd", "samples/erd/complex-ecommerce/Data/MyDbContext.cs");
    }

    [AotSmokeFact]
    public async Task Erd_SimpleContext_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("erd", "samples/erd/simple-context/EntityFramework/MyDbContext.cs");
    }

    [AotSmokeFact]
    public async Task ClassDiagram_DesignPatterns_ShouldMatchReference()
    {
        await AssertDiagramParityAsync(
            "classdiagram", "samples/classdiagram/design-patterns/Domain/Order.cs",
            "--inheritance", "--dependencies", "--depth", "2", "--properties", "true", "--functions", "true");
    }

    [AotSmokeFact]
    public async Task ClassDiagram_ComplexHierarchy_ShouldMatchReference()
    {
        await AssertDiagramParityAsync(
            "classdiagram", "samples/classdiagram/complex-hierarchy/Domain/Models/CEO.cs",
            "--inheritance", "--dependencies", "--depth", "5", "--properties", "true", "--functions", "true");
    }

    [AotSmokeFact]
    public async Task ClassDiagram_SimpleHierarchy_ShouldMatchReference()
    {
        await AssertDiagramParityAsync(
            "classdiagram", "samples/classdiagram/simple-hierarchy/Models/Admin.cs",
            "--inheritance", "--dependencies", "--depth", "2", "--properties", "true", "--functions", "true");
    }

    [AotSmokeFact]
    public async Task ClassDiagram_SourceDirectory_ShouldMatchReference()
    {
        await AssertDiagramParityAsync("classdiagram", "src", "--inheritance", "--dependencies");
    }

    [AotSmokeFact]
    public async Task ClassDiagram_ConcurrencyAndAnnotationTypes_ShouldMatchReference()
    {
        // BCL types that bind only through the embedded reference assemblies (spec §1.5).
        _temp.CreateFile("Types/Types.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var file = _temp.CreateFile("Types/Inventory.cs",
            """
            using System.Collections.Concurrent;
            using System.ComponentModel.DataAnnotations;

            namespace Types;

            public class Inventory : Dictionary<string, int>, IDisposable
            {
                [Required]
                public string Name { get; set; } = "";

                public ConcurrentDictionary<string, StockItem> Items { get; } = new();

                public Barrier? Gate { get; set; }

                public IEnumerable<StockItem> Available() => Items.Values;

                public void Dispose() => Gate?.Dispose();
            }

            public record StockItem(string Sku, decimal Price);
            """);

        await AssertDiagramParityAsync("classdiagram", file, "--inheritance", "--dependencies");
    }

    [AotSmokeFact]
    public async Task Visualize_SolutionWithMalformedProject_ShouldMatchReference()
    {
        var solution = CreateSolutionWithMalformedProject();

        await AssertDiagramParityAsync("visualize", solution, "--format", "mermaid");
    }

    [AotSmokeFact]
    public async Task Visualize_SlnListingAProjectTwice_ShouldFailLikeReference()
    {
        _temp.CreateFile("Good/Good.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var solution = _temp.CreateFile("Duplicate.sln",
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Good", "Good\Good.csproj", "{0A5E0000-0000-4000-8000-000000000001}"
            EndProject
            Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "GoodAgain", "Good\Good.csproj", "{0A5E0000-0000-4000-8000-000000000002}"
            EndProject
            """);

        await AssertParityAsync(expectSuccess: false, "visualize", solution, "--format", "mermaid");
    }

    [AotSmokeFact]
    public async Task Stats_Repository_ShouldMatchReferenceApartFromTiming()
    {
        await AssertStatsParityAsync("stats", "ProjGraph.slnx");
    }

    [AotSmokeFact]
    public async Task Stats_SolutionWithMalformedProject_ShouldMatchReferenceApartFromTiming()
    {
        var solution = CreateSolutionWithMalformedProject();

        await AssertStatsParityAsync("stats", solution);
    }

    [AotSmokeFact]
    public async Task Stats_UnknownOption_ShouldFailLikeReference()
    {
        // Strict parsing rejects the typo before the command runs (Spectre's error rendering path).
        await AssertParityAsync(expectSuccess: false, "stats", "ProjGraph.slnx", "--tpo", "3");
    }

    private string CreateSolutionWithMalformedProject()
    {
        _temp.CreateFile("Good/Good.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        _temp.CreateFile("Bad/Bad.csproj", "<Project><PropertyGroup></Project>");
        return _temp.CreateFile("Malformed.slnx",
            "<Solution><Project Path=\"Good/Good.csproj\" /><Project Path=\"Bad/Bad.csproj\" /></Solution>");
    }

    private static async Task AssertParityAsync(bool expectSuccess, params string[] arguments)
    {
        var (native, reference) = await RunBothAsync(arguments);

        AssertReferenceOutcome(reference, expectSuccess);
        AssertSameResult(native, reference);
    }

    /// <summary>
    /// Compares a diagram command twice: printed to the console, then written with <c>--output</c>.
    /// Both builds write to the same path one after the other, because the "Saved to" line on
    /// standard error names the path and Spectre wraps it at the console width, so two different
    /// paths could not be compared exactly.
    /// </summary>
    /// <param name="arguments">The CLI arguments, without <c>--output</c>.</param>
    private async Task AssertDiagramParityAsync(params string[] arguments)
    {
        await AssertParityAsync(expectSuccess: true, arguments);

        var outputPath = Path.Combine(_temp.DirectoryPath, "output", "diagram.mmd");
        string[] withOutput = [.. arguments, "--output", outputPath];

        var native = await ProcessRunner.RunAsync(SmokeEnvironment.CliNative, withOutput);
        File.Exists(outputPath).Should().BeTrue($"the native build must write the diagram; stderr:\n{native.StandardError}");
        var nativeFile = await File.ReadAllBytesAsync(outputPath);
        File.Delete(outputPath);

        var reference = await ProcessRunner.RunAsync(SmokeEnvironment.CliReference, withOutput);
        AssertReferenceOutcome(reference, expectSuccess: true);
        var referenceFile = await File.ReadAllBytesAsync(outputPath);

        AssertSameResult(native, reference);
        nativeFile.Should().Equal(referenceFile, "the native build must write the same diagram bytes");
    }

    private static async Task AssertStatsParityAsync(params string[] arguments)
    {
        var (native, reference) = await RunBothAsync(arguments);

        AssertReferenceOutcome(reference, expectSuccess: true);
        native.ExitCode.Should().Be(reference.ExitCode);
        NormalizeStats(native.StandardOutput).Should().Be(NormalizeStats(reference.StandardOutput));
        native.StandardError.Should().Be(reference.StandardError);
    }

    private static async Task<(ProcessResult Native, ProcessResult Reference)> RunBothAsync(string[] arguments)
    {
        var native = await ProcessRunner.RunAsync(SmokeEnvironment.CliNative, arguments);
        var reference = await ProcessRunner.RunAsync(SmokeEnvironment.CliReference, arguments);
        return (native, reference);
    }

    /// <summary>
    /// Pins the reference outcome, so a case where both builds fail the same way (a wrong path, a
    /// missing sample) cannot pass as parity.
    /// </summary>
    /// <param name="reference">The reference build's result.</param>
    /// <param name="expectSuccess">Whether the reference build must exit with 0.</param>
    private static void AssertReferenceOutcome(ProcessResult reference, bool expectSuccess)
    {
        if (expectSuccess)
        {
            reference.ExitCode.Should().Be(0, $"the reference build must succeed; stderr:\n{reference.StandardError}");
        }
        else
        {
            reference.ExitCode.Should().NotBe(0, "the reference build must reject this input");
        }
    }

    private static void AssertSameResult(ProcessResult native, ProcessResult reference)
    {
        native.ExitCode.Should().Be(reference.ExitCode, $"native stderr:\n{native.StandardError}");
        native.StandardOutput.Should().Be(reference.StandardOutput);
        native.StandardError.Should().Be(reference.StandardError);
    }

    /// <summary>
    /// Drops the wall-clock "Analysis time" row and collapses runs of spaces, because the timing
    /// value's width can shift the table's padding.
    /// </summary>
    /// <param name="output">The captured standard output of <c>stats</c>.</param>
    /// <returns>The output without timing, for comparison.</returns>
    private static string NormalizeStats(string output)
    {
        return string.Join('\n', output.ReplaceLineEndings("\n")
            .Split('\n')
            .Where(line => !line.TrimStart().StartsWith("Analysis time", StringComparison.Ordinal))
            .Select(line => RunOfSpaces().Replace(line, " ")));
    }

    [GeneratedRegex(" {2,}")]
    private static partial Regex RunOfSpaces();
}
```

- [ ] **Step 6: Build and confirm the suite skips by default**

```bash
dotnet build ProjGraph.slnx -c Release
dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build
```

Expected: build succeeds with 0 warnings. Tests report `Skipped: 16, Total: 16` (no failures).

- [ ] **Step 7: Verify the harness JIT apphost vs JIT `.dll` (must pass)**

The MCP variables must be set because `[AotSmokeFact]` needs all four. No MCP tests exist yet.

```bash
export PROJGRAPH_SMOKE_CLI_NATIVE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli
export PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll
export PROJGRAPH_SMOKE_MCP_NATIVE=src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp
export PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.dll
dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build
```

Expected: `Passed: 16`. (On Windows the apphost is `ProjGraph.Cli.exe` and the MCP RID directory is `win-x64`.)

- [ ] **Step 8: Verify the harness detects divergence and a missing variable (must fail)**

```bash
PROJGRAPH_SMOKE_CLI_NATIVE=/bin/true dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build --filter "FullyQualifiedName~Visualize_Mermaid"
```

Expected: `Failed: 1`, with `Expected native.StandardOutput to be "```mermaid` in the message.

```bash
env -u PROJGRAPH_SMOKE_CLI_NATIVE PROJGRAPH_SMOKE_REQUIRED=true dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build --filter "FullyQualifiedName~Visualize_Mermaid"
```

Expected: `Failed: 1` with `System.InvalidOperationException : PROJGRAPH_SMOKE_CLI_NATIVE is not set.` Then `unset` the four variables.

- [ ] **Step 9: Document the project**

In `CLAUDE.md`, add a row to the "Test projects" table after `Tests.Contract`:

```markdown
| `Tests.Smoke.Aot` | Native AOT vs JIT parity for the CLI and MCP executables (skipped unless `PROJGRAPH_SMOKE_*` is set; runs in the `aot-smoke` CI job) |
```

In `CONTRIBUTING.md`, add the same row to the "Test Organisation" table after `Tests.Contract`. Keep the table's padded column alignment: widen every row of the table so the columns line up, because markdownlint checks it.

In `ARCHITECTURE.md`, add a line to the `tests/` tree after `ProjGraph.Tests.Contract`:

```text
│   ├── ProjGraph.Tests.Smoke.Aot       # Native AOT vs JIT parity (CI aot-smoke job)
```

- [ ] **Step 10: Format check and commit**

```bash
dotnet format ProjGraph.slnx --verify-no-changes
git add ProjGraph.slnx tests/ProjGraph.Tests.Smoke.Aot CLAUDE.md CONTRIBUTING.md ARCHITECTURE.md
git commit -m "test: add the Native AOT smoke suite with CLI parity cases"
```

---

### Task 2: CLI native publish configuration

**Files:**
- Modify: `src/ProjGraph.Cli/ProjGraph.Cli.csproj`
- Modify: `src/ProjGraph.Cli/Program.cs`
- Modify: `src/ProjGraph.Cli/Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: the Task 1 suite and its four variables.
- Produces: `dotnet publish src/ProjGraph.Cli -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/cli` yields a working `artifacts/native/cli/ProjGraph.Cli`. Task 4's CI job runs this exact command.

- [ ] **Step 1: Confirm the native publish fails today**

```bash
dotnet publish src/ProjGraph.Cli -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/cli
```

Expected: FAIL with `Program.cs(26,19): error IL3050: Using member 'Spectre.Console.Cli.CommandApp.CommandApp(ITypeRegistrar)' which has 'RequiresDynamicCodeAttribute'…` and `DependencyInjection.cs(29,9): error IL2067: 'implementationType' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors'…`.

- [ ] **Step 2: Turn on the analyzers at build time**

In `src/ProjGraph.Cli/ProjGraph.Cli.csproj`, after the `<RollForward>` line in the first `PropertyGroup`, add:

```xml
    <!-- Native AOT is opt-in (-p:PublishAot=true). The analyzers run on every build so first-party
         code stays AOT-clean under warnings-as-errors; third-party ILC warnings must not fail publish. -->
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <EnableAotAnalyzer>true</EnableAotAnalyzer>
    <IlcTreatWarningsAsErrors>false</IlcTreatWarningsAsErrors>
```

Run: `dotnet build src/ProjGraph.Cli -c Release`
Expected: FAIL with the same IL3050 and IL2067 errors. A plain build now catches them too.

- [ ] **Step 3: Add the two suppressions**

In `src/ProjGraph.Cli/Program.cs`, add `using System.Diagnostics.CodeAnalysis;` after `using Spectre.Console.Cli;`, and put the attribute on `Main`:

```csharp
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Spectre.Console.Cli reflects over the command and settings types. ProjGraph.Cli and " +
                        "Spectre.Console.Cli are trimmer root assemblies, so that metadata is kept, and " +
                        "tests/ProjGraph.Tests.Smoke.Aot runs every command natively.")]
    public static int Main(string[] args)
```

In `src/ProjGraph.Cli/Infrastructure/DependencyInjection.cs`, add `using System.Diagnostics.CodeAnalysis;` after `using Spectre.Console.Cli;`, and put the attribute on `TypeRegistrar.Register(Type, Type)` (after its XML doc comment):

```csharp
    [UnconditionalSuppressMessage("Trimming", "IL2067:DynamicallyAccessedMembers",
        Justification = "Spectre.Console.Cli registers the command and settings types it discovers. They live in " +
                        "ProjGraph.Cli and Spectre.Console.Cli, which are trimmer root assemblies, so their " +
                        "public constructors are kept; tests/ProjGraph.Tests.Smoke.Aot covers every command.")]
    public void Register(Type service, Type implementation)
```

Run: `dotnet build src/ProjGraph.Cli -c Release`
Expected: build succeeds, 0 warnings.

- [ ] **Step 4: Publish and confirm the smoke suite catches the Spectre crash**

```bash
dotnet publish src/ProjGraph.Cli -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/cli
artifacts/native/cli/ProjGraph.Cli stats ProjGraph.slnx; echo "exit=$?"
```

Expected: publish succeeds, with third-party warnings only (`IL2104` for Spectre.Console.Cli and Microsoft.CodeAnalysis, `IL3053`, `IL3000`). The run crashes with `Unhandled exception. Spectre.Console.Cli.CommandRuntimeException: Could not get settings type for command of type 'ProjGraph.Cli.Commands.VisualizeCommand'.` and `exit=134`.

```bash
dotnet build ProjGraph.slnx -c Release
export PROJGRAPH_SMOKE_CLI_NATIVE=artifacts/native/cli/ProjGraph.Cli
export PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll
export PROJGRAPH_SMOKE_MCP_NATIVE=src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp
export PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.dll
dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build
```

Expected: FAIL, all 16 CLI cases (exit code 134 vs the reference's).

- [ ] **Step 5: Root the reflected assemblies**

In `src/ProjGraph.Cli/ProjGraph.Cli.csproj`, after the `Spectre.Console.Cli` `PackageReference` `ItemGroup`, add (items, not `-p:` properties; the spike showed properties don't work):

```xml
  <!-- Spectre.Console.Cli discovers commands and settings through reflection. Rooting both assemblies
       keeps that metadata in a trimmed or Native AOT build; the AOT smoke suite guards it. -->
  <ItemGroup>
    <TrimmerRootAssembly Include="ProjGraph.Cli"/>
    <TrimmerRootAssembly Include="Spectre.Console.Cli"/>
  </ItemGroup>
```

- [ ] **Step 6: Republish and run the suite**

```bash
dotnet publish src/ProjGraph.Cli -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/cli
dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build
```

Expected: `Passed: 16`. The binary is about 36.6 MB.

- [ ] **Step 7: Confirm the JIT CLI is unchanged**

```bash
unset PROJGRAPH_SMOKE_CLI_NATIVE PROJGRAPH_SMOKE_CLI_REFERENCE PROJGRAPH_SMOKE_MCP_NATIVE PROJGRAPH_SMOKE_MCP_REFERENCE
dotnet build ProjGraph.slnx -c Release
dotnet test tests/ProjGraph.Tests.Integration.Cli -c Release --no-build
git grep -n "PublishAot" -- '*.csproj' '*.props'
```

Expected: build 0 warnings, `Tests.Integration.Cli` all pass, and `git grep` prints nothing.

- [ ] **Step 8: Commit**

```bash
dotnet format ProjGraph.slnx --verify-no-changes
git add src/ProjGraph.Cli
git commit -m "feat(cli): make the CLI publishable as Native AOT"
```

---

### Task 3: MCP parity cases and MCP native publish configuration

**Files:**
- Create: `tests/ProjGraph.Tests.Smoke.Aot/Helpers/McpServerPair.cs`
- Create: `tests/ProjGraph.Tests.Smoke.Aot/McpParityTests.cs`
- Modify: `src/ProjGraph.Mcp/ProjGraph.Mcp.csproj`

**Interfaces:**
- Consumes: `SmokeEnvironment` (`McpNative`, `McpReference`, `RepositoryRoot`, `GetRootPath`), `SmokeCommand`, `AotSmokeFactAttribute`, `TestDirectory` (Task 1). From ModelContextProtocol 2.2.0: `StdioClientTransport`, `StdioClientTransportOptions` (`Name`, `Command`, `Arguments`, `WorkingDirectory`), `McpClient.CreateAsync`, `McpClient.ServerInfo`, `ServerCapabilities`, `ListToolsAsync(ListToolsRequestParams, CancellationToken)`, `ListPromptsAsync(ListPromptsRequestParams, CancellationToken)`, `GetPromptAsync(string, IReadOnlyDictionary<string, object?>, …)`, `ReadResourceAsync(Uri, …)`, `CallToolAsync(string, IReadOnlyDictionary<string, object?>, …)`, and `McpJsonUtilities.DefaultOptions`.
- Produces: `public sealed class McpServerPair : IAsyncLifetime` with `Task<(McpClient Native, McpClient Reference)> GetClientsAsync()`. `dotnet publish src/ProjGraph.Mcp -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/mcp` yields a working `artifacts/native/mcp/ProjGraph.Mcp`.

- [ ] **Step 1: Create the shared server pair**

Create `tests/ProjGraph.Tests.Smoke.Aot/Helpers/McpServerPair.cs`:

```csharp
using ModelContextProtocol.Client;

namespace ProjGraph.Tests.Smoke.Aot.Helpers;

/// <summary>
/// One native and one reference MCP server, shared by every test in a class. Stopping a stdio
/// client takes seconds, and one long-lived session per build is also how clients use the server.
/// The servers start on first use, so a skipped suite never launches them.
/// </summary>
public sealed class McpServerPair : IAsyncLifetime
{
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

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_native is not null)
        {
            await _native.DisposeAsync();
        }

        if (_reference is not null)
        {
            await _reference.DisposeAsync();
        }
    }

    private static async Task<McpClient> ConnectAsync(SmokeCommand command)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ProjGraph AOT smoke",
            Command = command.FileName,
            Arguments = [.. command.LeadingArguments],
            WorkingDirectory = SmokeEnvironment.RepositoryRoot
        });

        return await McpClient.CreateAsync(transport);
    }
}
```

- [ ] **Step 2: Write the MCP parity cases**

Create `tests/ProjGraph.Tests.Smoke.Aot/McpParityTests.cs` (`ReadResourceAsync` takes a `Uri` because CA2234 rejects the string overload):

```csharp
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ProjGraph.Tests.Shared.Helpers;
using ProjGraph.Tests.Smoke.Aot.Helpers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProjGraph.Tests.Smoke.Aot;

/// <summary>
/// Starts the native MCP server and the JIT build of the same commit over stdio, sends both the
/// same requests, and requires identical protocol results.
/// </summary>
/// <param name="servers">The native and reference servers shared by this class.</param>
public sealed class McpParityTests(McpServerPair servers) : IClassFixture<McpServerPair>
{
    [AotSmokeFact]
    public async Task Initialize_ShouldAdvertiseTheSameServer()
    {
        var (native, reference) = await servers.GetClientsAsync();

        reference.ServerInfo.Name.Should().Be("ProjGraph");
        AssertSameJson(native.ServerInfo, reference.ServerInfo);
        AssertSameJson(native.ServerCapabilities, reference.ServerCapabilities);
    }

    [AotSmokeFact]
    public async Task ListTools_ShouldMatchReference()
    {
        await AssertSameResultAsync(client => client.ListToolsAsync(new ListToolsRequestParams()));
    }

    [AotSmokeFact]
    public async Task ListPrompts_ShouldMatchReference()
    {
        await AssertSameResultAsync(client => client.ListPromptsAsync(new ListPromptsRequestParams()));
    }

    [AotSmokeFact]
    public async Task GetPrompt_ShouldMatchReference()
    {
        // Prompt results serialize IEnumerable<ChatMessage> through McpJsonContext.
        var path = SmokeEnvironment.GetRootPath("samples/classdiagram/complex-hierarchy/Domain/Models/CEO.cs");

        await AssertSameResultAsync(client => client.GetPromptAsync(
            "class_structure_review", new Dictionary<string, object?> { ["path"] = path }));
    }

    [AotSmokeFact]
    public async Task ReadWelcomeResource_ShouldMatchReference()
    {
        await AssertSameResultAsync(client => client.ReadResourceAsync(new Uri("projgraph://welcome")));
    }

    [AotSmokeFact]
    public async Task GetProjectGraph_ShouldMatchReference()
    {
        await AssertSameToolResultAsync("get_project_graph", new Dictionary<string, object?>
        {
            ["path"] = SmokeEnvironment.GetRootPath("samples/visualize/modular-architecture/ModularArchitecture.slnx")
        });
    }

    [AotSmokeFact]
    public async Task GetProjectStats_ShouldMatchReference()
    {
        await AssertSameToolResultAsync("get_project_stats", new Dictionary<string, object?>
        {
            ["path"] = SmokeEnvironment.GetRootPath("samples/visualize/modular-architecture/ModularArchitecture.slnx")
        });
    }

    [AotSmokeFact]
    public async Task GetProjectStats_SolutionWithMalformedProject_ShouldMatchReference()
    {
        // The warnings array is built with JsonNode APIs that fail without reflection-based JSON.
        using var temp = new TestDirectory();
        temp.CreateFile("Good/Good.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        temp.CreateFile("Bad/Bad.csproj", "<Project><PropertyGroup></Project>");
        var solution = temp.CreateFile("Malformed.slnx",
            "<Solution><Project Path=\"Good/Good.csproj\" /><Project Path=\"Bad/Bad.csproj\" /></Solution>");

        var reference = await AssertSameToolResultAsync(
            "get_project_stats", new Dictionary<string, object?> { ["path"] = solution });

        var warnings = JsonNode.Parse(JoinText(reference))?["warnings"]?.AsArray();
        warnings.Should().NotBeNullOrEmpty("the malformed project must surface as a warning");
    }

    [AotSmokeFact]
    public async Task GetClassDiagram_ShouldMatchReference()
    {
        await AssertSameToolResultAsync("get_class_diagram", new Dictionary<string, object?>
        {
            ["path"] = SmokeEnvironment.GetRootPath("samples/classdiagram/complex-hierarchy/Domain/Models/CEO.cs"),
            ["options"] = new Dictionary<string, object?> { ["includeInheritance"] = true }
        });
    }

    [AotSmokeFact]
    public async Task GetErd_ShouldMatchReference()
    {
        await AssertSameToolResultAsync("get_erd", new Dictionary<string, object?>
        {
            ["path"] = SmokeEnvironment.GetRootPath("samples/erd/complex-ecommerce/Data/MyDbContext.cs")
        });
    }

    private async Task<T> AssertSameResultAsync<T>(Func<McpClient, ValueTask<T>> request)
    {
        var (native, reference) = await servers.GetClientsAsync();

        var referenceResult = await request(reference);
        AssertSameJson(await request(native), referenceResult);
        return referenceResult;
    }

    private async Task<CallToolResult> AssertSameToolResultAsync(
        string toolName, Dictionary<string, object?> arguments)
    {
        var reference = await AssertSameResultAsync(client => client.CallToolAsync(toolName, arguments));

        // Pins the reference outcome, so an error both builds return identically cannot pass as parity.
        reference.IsError.Should().NotBe(true, JoinText(reference));
        return reference;
    }

    private static void AssertSameJson<T>(T native, T reference)
    {
        var nativeJson = JsonSerializer.SerializeToNode(native, McpJsonUtilities.DefaultOptions);
        var referenceJson = JsonSerializer.SerializeToNode(reference, McpJsonUtilities.DefaultOptions);

        JsonNode.DeepEquals(nativeJson, referenceJson).Should().BeTrue(
            $"the native server must return the reference result.\nNative:\n{nativeJson}\nReference:\n{referenceJson}");
    }

    private static string JoinText(CallToolResult result)
    {
        return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
    }
}
```

- [ ] **Step 3: Verify the MCP cases JIT apphost vs JIT `.dll` (must pass)**

```bash
dotnet build ProjGraph.slnx -c Release
export PROJGRAPH_SMOKE_CLI_NATIVE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli
export PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll
export PROJGRAPH_SMOKE_MCP_NATIVE=src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp
export PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.dll
dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build --filter "FullyQualifiedName~McpParityTests"
```

Expected: `Passed: 10`, in seconds rather than ~10 s per test (the shared pair costs ~10 s once, at class teardown).

- [ ] **Step 4: Confirm the MCP native publish fails today**

```bash
dotnet publish src/ProjGraph.Mcp -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/mcp
```

Expected: FAIL with ILC errors including `Microsoft.CodeAnalysis.dll : error IL2104: Assembly 'Microsoft.CodeAnalysis' produced trim warnings` and `CommonCompiler.cs(175): error IL3000: …'System.Reflection.Assembly.Location.get' always returns an empty string…`.

- [ ] **Step 5: Configure the MCP project**

In `src/ProjGraph.Mcp/ProjGraph.Mcp.csproj`, after `<PublishSingleFile>true</PublishSingleFile>` (leave that line unchanged; see deviation 1), add:

```xml

    <!-- Native AOT is opt-in (-p:PublishAot=true). The analyzers run on every build so first-party
         code stays AOT-clean under warnings-as-errors; third-party ILC warnings must not fail publish. -->
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <EnableAotAnalyzer>true</EnableAotAnalyzer>
    <IlcTreatWarningsAsErrors>false</IlcTreatWarningsAsErrors>
```

Run: `dotnet build src/ProjGraph.Mcp -c Release`
Expected: build succeeds, 0 warnings.

- [ ] **Step 6: Publish both executables and run the whole suite natively**

Publish one at a time:

```bash
dotnet publish src/ProjGraph.Cli -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/cli
dotnet publish src/ProjGraph.Mcp -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/mcp
export PROJGRAPH_SMOKE_CLI_NATIVE=artifacts/native/cli/ProjGraph.Cli
export PROJGRAPH_SMOKE_MCP_NATIVE=artifacts/native/mcp/ProjGraph.Mcp
dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build
```

Expected: `Passed: 26`, about 20 s. The MCP binary is about 42.9 MB.

- [ ] **Step 7: Confirm the publish didn't touch the JIT reference, and that the MCP suites are unchanged**

```bash
ls src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp
grep -c IsDynamicCodeSupported src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.runtimeconfig.json
unset PROJGRAPH_SMOKE_CLI_NATIVE PROJGRAPH_SMOKE_CLI_REFERENCE PROJGRAPH_SMOKE_MCP_NATIVE PROJGRAPH_SMOKE_MCP_REFERENCE
dotnet test tests/ProjGraph.Tests.Integration.Mcp -c Release --no-build
dotnet test tests/ProjGraph.Tests.Contract -c Release --no-build
```

Expected: the apphost exists; `grep -c` prints `0` (no AOT feature switches in the JIT runtimeconfig); both suites pass, including `McpSurfaceSnapshotTests` and `McpTransportTests`.

- [ ] **Step 8: Commit**

```bash
dotnet format ProjGraph.slnx --verify-no-changes
git add src/ProjGraph.Mcp/ProjGraph.Mcp.csproj tests/ProjGraph.Tests.Smoke.Aot
git commit -m "feat(mcp): make the MCP server publishable as Native AOT and add MCP parity cases"
```

---

### Task 4: `aot-smoke` CI job

**Files:**
- Modify: `.github/workflows/ci.yml`
- Modify: `CONTRIBUTING.md`

**Interfaces:**
- Consumes: the publish commands from Tasks 2–3, and the suite with `PROJGRAPH_SMOKE_REQUIRED` from Task 1.
- Produces: a GitHub Actions check named `aot-smoke` (the job id, since the job sets no `name`).

- [ ] **Step 1: Add the job**

Append to `.github/workflows/ci.yml`, under `jobs:` after the `build` job (same indentation as `build:`):

```yaml

  aot-smoke:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v7

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json
          cache: true
          cache-dependency-path: Directory.Packages.props

      - name: Install Native AOT prerequisites
        run: |
          sudo apt-get update
          sudo apt-get install -y --no-install-recommends clang zlib1g-dev

      - name: Restore dependencies
        run: dotnet restore ProjGraph.slnx

      - name: Build
        run: dotnet build ProjGraph.slnx --no-restore --configuration Release

      # --artifacts-path keeps the native publish out of src/*/bin and obj. ProjGraph.Mcp is
      # self-contained, so an in-place publish would overwrite the JIT reference build's apphost
      # and runtimeconfig.json in bin/Release/net10.0/linux-x64.
      - name: Publish native executables
        run: |
          dotnet publish src/ProjGraph.Cli --configuration Release --runtime linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build --output artifacts/native/cli
          dotnet publish src/ProjGraph.Mcp --configuration Release --runtime linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build --output artifacts/native/mcp

      - name: Smoke test native executables
        env:
          PROJGRAPH_SMOKE_REQUIRED: 'true'
          PROJGRAPH_SMOKE_CLI_NATIVE: artifacts/native/cli/ProjGraph.Cli
          PROJGRAPH_SMOKE_CLI_REFERENCE: src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll
          PROJGRAPH_SMOKE_MCP_NATIVE: artifacts/native/mcp/ProjGraph.Mcp
          PROJGRAPH_SMOKE_MCP_REFERENCE: src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.dll
        run: dotnet test tests/ProjGraph.Tests.Smoke.Aot --no-build --configuration Release --logger "console;verbosity=normal"
```

- [ ] **Step 2: Run the job's commands locally from a clean clone**

This proves the YAML's commands work from scratch, with no leftovers from earlier tasks. A clone only sees committed state, so commit the workflow first:

```bash
git add .github/workflows/ci.yml && git commit -m "ci: add the aot-smoke job"
rm -rf /tmp/pg-aot-ci && git clone --quiet --branch feat/aot-native-smoke . /tmp/pg-aot-ci
cd /tmp/pg-aot-ci
dotnet restore ProjGraph.slnx
dotnet build ProjGraph.slnx --no-restore --configuration Release
dotnet publish src/ProjGraph.Cli --configuration Release --runtime linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build --output artifacts/native/cli
dotnet publish src/ProjGraph.Mcp --configuration Release --runtime linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build --output artifacts/native/mcp
PROJGRAPH_SMOKE_REQUIRED=true \
PROJGRAPH_SMOKE_CLI_NATIVE=artifacts/native/cli/ProjGraph.Cli \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE=artifacts/native/mcp/ProjGraph.Mcp \
PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.dll \
dotnet test tests/ProjGraph.Tests.Smoke.Aot --no-build --configuration Release
cd - && rm -rf /tmp/pg-aot-ci
```

Expected: `Passed: 26, Skipped: 0`. The job's step names and commands must match these exactly. If a `git status` in the clone shows anything untracked outside `artifacts/`, `bin/`, and `obj/`, fix that before continuing.

- [ ] **Step 3: Confirm the existing matrix is unaffected**

From the repository (not the clone), run the matrix's commands:

```bash
dotnet restore ProjGraph.slnx
dotnet build ProjGraph.slnx --no-restore --configuration Release
dotnet format ProjGraph.slnx --no-restore --verify-no-changes
dotnet test ProjGraph.slnx --no-build --configuration Release
```

Expected: build 0 warnings, format clean, every suite green, and `ProjGraph.Tests.Smoke.Aot` reports `Skipped: 26`.

- [ ] **Step 4: Document running the suite locally**

In `CONTRIBUTING.md`, after the "Test Organisation" table, add:

````markdown
### Native AOT smoke tests

`Tests.Smoke.Aot` compares Native AOT builds of the CLI and MCP server with the JIT build of the
same commit. It is skipped unless the `PROJGRAPH_SMOKE_*` variables are set, and the `aot-smoke`
CI job runs it on every PR. To run it locally on Linux (requires `clang` or `gcc`; the `--artifacts-path`
keeps the self-contained MCP publish from overwriting the JIT build):

```bash
dotnet build ProjGraph.slnx -c Release
dotnet publish src/ProjGraph.Cli -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/cli
dotnet publish src/ProjGraph.Mcp -c Release -r linux-x64 -p:PublishAot=true --artifacts-path artifacts/aot-build -o artifacts/native/mcp
PROJGRAPH_SMOKE_CLI_NATIVE=artifacts/native/cli/ProjGraph.Cli \
PROJGRAPH_SMOKE_CLI_REFERENCE=src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll \
PROJGRAPH_SMOKE_MCP_NATIVE=artifacts/native/mcp/ProjGraph.Mcp \
PROJGRAPH_SMOKE_MCP_REFERENCE=src/ProjGraph.Mcp/bin/Release/net10.0/linux-x64/ProjGraph.Mcp.dll \
dotnet test tests/ProjGraph.Tests.Smoke.Aot -c Release --no-build
```
````

- [ ] **Step 5: Commit**

```bash
git add CONTRIBUTING.md
git commit -m "docs: explain how to run the Native AOT smoke suite locally"
```

---

### Task 5: Pull request and required check

Pushing, opening the PR, and changing branch protection are outward-facing. Confirm with the user before each.

- [ ] **Step 1: Push and open the PR (after user confirmation)**

```bash
git push -u origin feat/aot-native-smoke
gh pr create --base develop --title "feat: Native AOT build and PR smoke test (PR 2 of 3)" --body-file <description>
```

The description must include:
- What changed (CLI/MCP configuration, the smoke suite, the `aot-smoke` job) and that no csproj sets `PublishAot`.
- The ten "Deviations from the spec" above.
- Binary sizes and the local suite timing (~20 s).
- A request for the maintainer to add `aot-smoke` as a required status check on `develop`.
- It ends with the attribution line from the session's system reminder.

- [ ] **Step 2: Wait for CI**

```bash
gh pr checks --watch
```

Expected: the three `build` matrix jobs and `aot-smoke` all pass. In the `aot-smoke` log, the test summary shows `Passed: 26` and `Skipped: 0`. If `aot-smoke` fails on the runner but passed locally, read the log before changing anything (use superpowers:systematic-debugging). The likeliest runner-only differences are toolchain packages and console-width or encoding detection under GitHub Actions. Both builds see the same environment, so any output difference is a real finding.

- [ ] **Step 3: Make `aot-smoke` required**

This is a repository-settings change, so it belongs to the user (or run it only with their explicit go-ahead). After the check has reported at least once: GitHub → Settings → Branches (or Rules) → `develop` → required status checks → add `aot-smoke`. This fulfils the spec's PR 2 exit criterion "the `aot-smoke` job is green and required".

## Exit criteria (spec, PR 2)

- `aot-smoke` is green on the PR and marked required on `develop`.
- No csproj or props file sets `PublishAot` (`git grep -n PublishAot -- '*.csproj' '*.props'` is empty).
- The existing CI matrix is green, with `Tests.Smoke.Aot` skipped there.
