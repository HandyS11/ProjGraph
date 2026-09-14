# Native AOT PR 1: Library Preparation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove every Native AOT blocker from the shared libraries and the MCP server's JSON setup, while the shipped JIT tools keep byte-identical output.

**Architecture:** Characterization tests and an MCP surface snapshot land first, pinned against today's behavior. The parsers then move off Microsoft.Build: `ProjectParser` reads XML with `System.Xml.Linq`, and `SlnParser` uses `Microsoft.VisualStudio.SolutionPersistence`. `CompilationFactory` switches to embedded reference assemblies. The MCP server moves to source-generated JSON metadata. Finally the AOT analyzers are turned on for every library. Nothing about packaging or publishing changes.

**Tech Stack:** .NET 10 (SDK per `global.json`), C#, xUnit 2.9 + FluentAssertions 8, Roslyn 5.9, ModelContextProtocol 2.2.0, Microsoft.VisualStudio.SolutionPersistence 1.0.52.

**Spec:** `docs/superpowers/specs/2026-09-14-native-aot-design.md` (section "PR 1: library preparation"). Read it before starting.

## Global Constraints

- `TreatWarningsAsErrors=true`, `AnalysisMode=All`, and `EnforceCodeStyleInBuild=true` apply to every project, tests included. Sonar, Roslynator, VS Threading, and CA analyzers all run during build. Any warning is a build failure.
- XML documentation is required on all public APIs (`GenerateDocumentationFile=true`).
- CI runs `dotnet format ProjGraph.slnx --no-restore --verify-no-changes`, which must pass.
- Package versions are central: add or remove them only in `Directory.Packages.props`; `PackageReference` items carry no `Version`.
- No change to any command, option, MCP tool, prompt, resource, or output format (spec Non-goals).
- The characterization tests from Tasks 1–2 and the MCP snapshot from Task 3 must pass **before** and **after** every production change, and their expected values must never be edited to make a later task pass.
- Don't regenerate the committed samples, and don't fix bugs the characterization tests record. List them in the PR description instead.
- No csproj sets `PublishAot` in this PR.
- Microsoft.VisualStudio.SolutionPersistence is pinned at `1.0.52`.

## Deviations from the spec (intentional; mention them in the PR description)

1. **Snapshot location.** The `tools/list` schema snapshot is in `Tests.Integration.Mcp` rather than `Tests.Contract`, so it runs against the real server executable over stdio, reusing the launcher `McpTransportTests` already has. It also covers `prompts/list`, because `WithPrompts` changes too.
2. **Reflection-based JSON is disabled.** `ProjGraph.Mcp` sets `JsonSerializerIsReflectionEnabledByDefault=false`. Without it, the JIT build silently falls back to reflection, and the snapshot can't detect a type missing from `McpJsonContext` or options that were never passed. This was verified: removing the options from `WithTools` makes the real-exe tests fail only when the switch is set.
3. **MSBuild load-time validations are replicated.** `ProjectParser` rejects the same structural errors `ProjectRootElement.Open` rejected, and the characterization tests pin all of them: a root other than `<Project>`, a foreign XML namespace, and an item outside `<Target>` without a non-empty `Include`/`Update`/`Remove`. `InvalidDataException` (thrown for these) and `UnauthorizedAccessException` also map to `ParsingException`.
4. **Explicit GUID allow-list.** The `.sln` filter is a project-type GUID allow-list. It reproduces `KnownToBeMSBuildFormat`, which is decided by GUID and not by extension. A throwaway probe against Microsoft.Build 18.9.6 verified the list.
5. **Test-first commits.** The characterization tests and the snapshot are the PR's first three commits, not a single first commit.
6. **Docs.** `ARCHITECTURE.md` never mentions Microsoft.Build, so it is left alone. `src/ProjGraph.Lib.Core/README.md` does mention it and is updated instead.

## Known divergences from Microsoft.Build (not tested; list them in the PR description)

The characterization suite pins every behavior below that both implementations share. These edge cases differ and are accepted:

- A `.sln` `Project(...)` block with no `EndProject` made Microsoft.Build throw. SolutionPersistence accepts it.
- A `.sln` project path with `.`/`..` segments in the middle (`src\.\A\..\A\A.csproj`) was returned un-normalized. It is now normalized with `Path.GetFullPath`, which points to the same file.
- The C++ project-type GUID with a legacy `.vcproj` path made Microsoft.Build throw for the whole solution. The path is now returned, and that one project then fails to parse the way any non-MSBuild file does.
- A property element with child elements (`<TargetFramework><X>y</X></TargetFramework>`) returned the inner XML. It now returns the concatenated text (`y`).
- Other `ProjectRootElement.Open` structural checks not listed in deviation 3 aren't replicated. Those files now parse leniently.

## File Structure

| File | Change | Responsibility |
| --- | --- | --- |
| `tests/ProjGraph.Tests.Unit.Core/Parsers/ProjectParserCharacterizationTests.cs` | Create | Pins `ProjectParser` XML reading behavior |
| `tests/ProjGraph.Tests.Unit.Core/Parsers/SlnParserCharacterizationTests.cs` | Create | Pins `.sln` project selection, order, and paths |
| `tests/ProjGraph.Tests.Unit.Core/Parsers/Fixtures/characterization.sln` | Create | Committed `.sln` fixture covering every project-type branch |
| `tests/ProjGraph.Tests.Unit.Core/ProjGraph.Tests.Unit.Core.csproj` | Modify | Copies `Parsers/Fixtures/**` to the test output |
| `tests/ProjGraph.Tests.Integration.Mcp/Helpers/McpServerProcess.cs` | Create | Launches the real MCP exe and connects a stdio client (moved out of `McpTransportTests`) |
| `tests/ProjGraph.Tests.Integration.Mcp/McpTransportTests.cs` | Modify | Uses `McpServerProcess` |
| `tests/ProjGraph.Tests.Integration.Mcp/McpSurfaceSnapshotTests.cs` | Create | Compares `tools/list` + `prompts/list` with the snapshot |
| `tests/ProjGraph.Tests.Integration.Mcp/Snapshots/mcp-surface.json` | Create (generated) | The committed surface snapshot |
| `src/ProjGraph.Lib.Core/Parsers/ProjectParser.cs` | Replace | Project/props parsing on `System.Xml.Linq` |
| `src/ProjGraph.Lib.Core/Parsers/SlnParser.cs` | Replace | `.sln` parsing on SolutionPersistence |
| `src/ProjGraph.Lib.Core/Infrastructure/CompilationFactory.cs` | Replace | Roslyn references from embedded resources |
| `src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj` | Modify | Package swap, reference-assembly embedding with a build guard, `IsAotCompatible` |
| `tests/ProjGraph.Tests.Unit.Core/CompilationFactoryCoverageTests.cs` | Modify | Embedded reference set tests |
| `src/ProjGraph.Mcp/McpJsonContext.cs` | Create | Source-generated `JsonSerializerContext` |
| `src/ProjGraph.Mcp/Program.cs` | Modify | Passes the source-generated options to `WithTools`/`WithPrompts` |
| `src/ProjGraph.Mcp/ProjGraphTools.cs` | Modify | Stats serialization through `McpJsonContext` |
| `src/ProjGraph.Mcp/ProjGraph.Mcp.csproj` | Modify | `JsonSerializerIsReflectionEnabledByDefault=false` |
| `Directory.Packages.props` | Modify | Adds SolutionPersistence, removes Microsoft.Build and Workspaces.MSBuild |
| `src/ProjGraph.Lib.ClassDiagram/ProjGraph.Lib.ClassDiagram.csproj` | Modify | Removes Workspaces.MSBuild, sets `IsAotCompatible` |
| `src/ProjGraph.Core/…`, `Lib.Dependencies`, `Lib.EntityFramework`, `Lib` csproj files | Modify | `IsAotCompatible` |
| `CLAUDE.md`, `src/ProjGraph.Lib.Core/README.md` | Modify | Describe the new parsing stack |
| `tests/ProjGraph.Tests.Unit.Core/ProjectParserCoverageTests.cs` | Modify | Drops a stale comment about MSBuild's cache |

---

### Task 1: Characterize `ProjectParser`

Tests only. They record what the current Microsoft.Build-backed parser does, so they must **pass on the current code**. A failure means the expectation is wrong, not the parser. Every expected value below was checked against Microsoft.Build 18.9.6.

**Files:**
- Create: `tests/ProjGraph.Tests.Unit.Core/Parsers/ProjectParserCharacterizationTests.cs`

**Interfaces:**
- Consumes: `ProjectParser(IFileSystem)`, whose `Parse(string)` returns `(Project Project, IEnumerable<string> ProjectReferences, IEnumerable<PackageReference> PackageReferences)`; `PhysicalFileSystem`; `TestDirectory.CreateFile(string relativePath, string content)` (creates subdirectories and returns the full path); `PackageReference(string Name, string Version)` record; `ProjectType` enum; `ParsingException`.
- Produces: nothing consumed by later tasks. Tasks 4 and 6 re-run this class.

- [ ] **Step 1: Create a feature branch**

Branch from the branch that contains the spec (so the spec and this plan travel with the PR):

```bash
git switch docs/native-aot-design
git switch -c feat/aot-library-prep
```

- [ ] **Step 2: Write the characterization tests**

Create `tests/ProjGraph.Tests.Unit.Core/Parsers/ProjectParserCharacterizationTests.cs`:

```csharp
using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.Core.Parsers;

/// <summary>
/// Pins how <see cref="ProjectParser"/> reads project and props XML. The expected values were
/// recorded from the Microsoft.Build-backed implementation, so they document existing behaviour
/// (including quirks such as untrimmed values and first-in-document-order wins) rather than MSBuild
/// evaluation semantics. They must pass unchanged on the System.Xml.Linq implementation.
/// </summary>
[Trait("Category", "Core")]
public sealed class ProjectParserCharacterizationTests : IDisposable
{
    private readonly TestDirectory _temp = new();
    private readonly ProjectParser _parser = new(new PhysicalFileSystem());

    public void Dispose()
    {
        _temp.Dispose();
    }

    [Fact]
    public void Parse_LegacyMsBuildNamespace_ShouldReadPropertiesAndItems()
    {
        var path = _temp.CreateFile("legacy/Legacy.csproj",
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <TargetFramework>net48</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\A\A.csproj" />
                <PackageReference Include="P1" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var (project, references, packages) = _parser.Parse(path);

        project.Framework.Should().Be("net48");
        project.Type.Should().Be(ProjectType.Executable);
        references.Should().Equal(@"..\A\A.csproj");
        packages.Should().Equal(new PackageReference("P1", "1.0.0"));
    }

    [Fact]
    public void Parse_VersionMetadata_ShouldReadAttributeOrChildElementCaseSensitively()
    {
        var path = _temp.CreateFile("version/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Attribute" Version="1.0.0" />
                <PackageReference Include="Child">
                  <Version>2.0.0</Version>
                </PackageReference>
                <PackageReference Include="LowerAttribute" version="3.0.0" />
                <PackageReference Include="LowerChild">
                  <version>4.0.0</version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        var (_, _, packages) = _parser.Parse(path);

        packages.Should().Equal(
            new PackageReference("Attribute", "1.0.0"),
            new PackageReference("Child", "2.0.0"),
            new PackageReference("LowerAttribute", "unknown"),
            new PackageReference("LowerChild", "unknown"));
    }

    [Fact]
    public void Parse_UpdateAndRemoveItems_ShouldBeReturnedWithAnEmptyName()
    {
        var path = _temp.CreateFile("update/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Update="Updated" Version="1.0.0" />
                <PackageReference Remove="Removed" />
                <PackageReference Include="Included" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """);

        var (_, _, packages) = _parser.Parse(path);

        packages.Should().Equal(
            new PackageReference("", "1.0.0"),
            new PackageReference("", "unknown"),
            new PackageReference("Included", "2.0.0"));
    }

    [Fact]
    public void Parse_NameCasing_ShouldMatchPropertiesCaseInsensitivelyAndItemTypesCaseSensitively()
    {
        var path = _temp.CreateFile("casing/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <targetFRAMEWORK>net10.0</targetFRAMEWORK>
                <outputtype>exe</outputtype>
                <istestproject>TRUE</istestproject>
              </PropertyGroup>
              <ItemGroup>
                <projectreference Include="Lower.csproj" />
                <packagereference Include="Lower" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var (project, references, packages) = _parser.Parse(path);

        project.Framework.Should().Be("net10.0");
        project.Type.Should().Be(ProjectType.Test);
        references.Should().BeEmpty();
        packages.Should().BeEmpty();
    }

    [Fact]
    public void Parse_PropertyValueText_ShouldBeUntrimmedWithCommentsRemovedAndEntitiesDecoded()
    {
        var path = _temp.CreateFile("text/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>  net10.0  </TargetFramework>
                <OutputType>
                </OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="A&amp;B" Version="1.0.0" />
                <PackageReference Include="Commented">
                  <Version><!-- pinned -->2.0.0</Version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        var (project, _, packages) = _parser.Parse(path);

        project.Framework.Should().Be("  net10.0  ");
        project.Type.Should().Be(ProjectType.Library);
        packages.Should().Equal(
            new PackageReference("A&B", "1.0.0"),
            new PackageReference("Commented", "2.0.0"));
    }

    [Fact]
    public void Parse_PropertiesAndItemsInsideTarget_ShouldCountInDocumentOrder()
    {
        var path = _temp.CreateFile("target/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <Target Name="Custom">
                <PropertyGroup>
                  <TargetFramework>net6.0</TargetFramework>
                </PropertyGroup>
                <ItemGroup>
                  <ProjectReference Include="InTarget.csproj" />
                  <PackageReference>
                    <Version>1.0.0</Version>
                  </PackageReference>
                </ItemGroup>
              </Target>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var (project, references, packages) = _parser.Parse(path);

        project.Framework.Should().Be("net6.0");
        references.Should().Equal("InTarget.csproj");
        packages.Should().Equal(new PackageReference("", "1.0.0"));
    }

    [Fact]
    public void Parse_ChooseWhenOtherwise_ShouldReadEveryBranch()
    {
        var path = _temp.CreateFile("choose/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <Choose>
                <When Condition="'$(Configuration)' == 'Debug'">
                  <PropertyGroup>
                    <TargetFramework>net7.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="When.csproj" />
                  </ItemGroup>
                </When>
                <Otherwise>
                  <ItemGroup>
                    <ProjectReference Include="Otherwise.csproj" />
                  </ItemGroup>
                </Otherwise>
              </Choose>
            </Project>
            """);

        var (project, references, _) = _parser.Parse(path);

        project.Framework.Should().Be("net7.0");
        references.Should().Equal("When.csproj", "Otherwise.csproj");
    }

    [Fact]
    public void Parse_SamePropertyInSeveralPropertyGroups_FirstInDocumentOrderShouldWinRegardlessOfCondition()
    {
        var path = _temp.CreateFile("multi/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup Condition="'$(Configuration)' == 'Never'">
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <PropertyGroup>
                <TargetFramework Condition="false">net9.0</TargetFramework>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var (project, _, _) = _parser.Parse(path);

        project.Framework.Should().Be("net8.0");
    }

    [Fact]
    public void Parse_DirectoryBuildPropsAtTwoLevels_ShouldMergeDifferentPropertiesNearestFirst()
    {
        _temp.CreateFile("repo/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net6.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """);
        _temp.CreateFile("repo/src/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        var path = _temp.CreateFile("repo/src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var (project, _, _) = _parser.Parse(path);

        project.Framework.Should().Be("net10.0");
        project.Type.Should().Be(ProjectType.Executable);
    }

    [Fact]
    public void Parse_DirectoryPackagesProps_ShouldMatchNamesCaseInsensitivelyAndKeepWalkingUp()
    {
        _temp.CreateFile("cpm/Directory.Packages.props",
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="outer.package">
                  <Version>2.0.0</Version>
                </PackageVersion>
              </ItemGroup>
            </Project>
            """);
        _temp.CreateFile("cpm/src/Directory.Packages.props",
            """
            <Project>
              <ItemGroup>
                <PackageVersion Include="serilog" Version="4.1.0" />
              </ItemGroup>
            </Project>
            """);
        var path = _temp.CreateFile("cpm/src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" />
                <PackageReference Include="Outer.Package" />
              </ItemGroup>
            </Project>
            """);

        var (_, _, packages) = _parser.Parse(path);

        packages.Should().Equal(
            new PackageReference("Serilog", "4.1.0"),
            new PackageReference("Outer.Package", "2.0.0"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<Project><PropertyGroup></Project>")]
    [InlineData("<NotAProject><PropertyGroup /></NotAProject>")]
    [InlineData("<Project xmlns=\"http://example.com/other\" />")]
    [InlineData("<Project><ItemGroup><PackageReference /></ItemGroup></Project>")]
    [InlineData("<Project><ItemGroup><ProjectReference Include=\"\" /></ItemGroup></Project>")]
    [InlineData("<Project><Choose><When Condition=\"true\"><ItemGroup><PackageReference /></ItemGroup></When></Choose></Project>")]
    public void Parse_InvalidProjectXml_ShouldThrowParsingException(string content)
    {
        var path = _temp.CreateFile($"invalid-{Guid.NewGuid():N}/App.csproj", content);

        var act = () => _parser.Parse(path);

        act.Should().Throw<ParsingException>().WithMessage($"*{path}*");
    }

    [Fact]
    public void Parse_InvalidDirectoryBuildProps_ShouldBeSkippedWhileTheWalkContinues()
    {
        _temp.CreateFile("skip/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        _temp.CreateFile("skip/src/Directory.Build.props", "<NotAProject />");
        var path = _temp.CreateFile("skip/src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var (project, _, _) = _parser.Parse(path);

        project.Framework.Should().Be("net10.0");
    }
}
```

- [ ] **Step 3: Run them against the current Microsoft.Build implementation**

Run: `dotnet test tests/ProjGraph.Tests.Unit.Core --filter "FullyQualifiedName~ProjectParserCharacterizationTests"`
Expected: PASS, 18 tests (11 facts + 7 theory rows). If one fails, the current parser disagrees with the recorded value: stop and report it. Don't change the parser or edit the value to match your own reading of MSBuild.

- [ ] **Step 4: Commit**

```bash
git add tests/ProjGraph.Tests.Unit.Core/Parsers/ProjectParserCharacterizationTests.cs
git commit -m "test(core): characterize ProjectParser XML reading before dropping Microsoft.Build"
```

---

### Task 2: Characterize `SlnParser`

Tests and a committed fixture only, and they must pass on the current code. The fixture has one project per MSBuild classification branch. The 14 expected paths are exactly what `SolutionFile.ProjectsInOrder` classifies as `KnownToBeMSBuildFormat`. Classification goes by project-type GUID: `UnknownType` is skipped even though it is a `.csproj`.

**Files:**
- Create: `tests/ProjGraph.Tests.Unit.Core/Parsers/Fixtures/characterization.sln`
- Create: `tests/ProjGraph.Tests.Unit.Core/Parsers/SlnParserCharacterizationTests.cs`
- Modify: `tests/ProjGraph.Tests.Unit.Core/ProjGraph.Tests.Unit.Core.csproj`

**Interfaces:**
- Consumes: `SlnParser(IFileSystem)`, whose `GetProjectPaths(string)` returns `IEnumerable<string>` (and `[]` when the file is missing); `TestDirectory`; `ParsingException`.
- Produces: the fixture file, copied to `<test output>/Parsers/Fixtures/characterization.sln`. Task 5 re-runs this class.

- [ ] **Step 1: Add the fixture**

Create `tests/ProjGraph.Tests.Unit.Core/Parsers/Fixtures/characterization.sln` with exactly this content (it starts with an empty line, as Visual Studio writes it):

```text

Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "CsSdk", "src\CsSdk\CsSdk.csproj", "{00000001-0000-0000-0000-000000000000}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "CsClassic", "src\CsClassic\CsClassic.csproj", "{00000002-0000-0000-0000-000000000000}"
EndProject
Project("{fae04ec0-301f-11d3-bf4b-00c04f79efbc}") = "CsLowerGuid", "src\CsLowerGuid\CsLowerGuid.csproj", "{00000003-0000-0000-0000-000000000000}"
EndProject
Project("{F184B08F-C81C-45F6-A57F-5ABD9991F28F}") = "VbClassic", "src\VbClassic\VbClassic.vbproj", "{00000004-0000-0000-0000-000000000000}"
EndProject
Project("{778DAE3C-4631-46EA-AA77-85C1314464D9}") = "VbSdk", "src\VbSdk\VbSdk.vbproj", "{00000005-0000-0000-0000-000000000000}"
EndProject
Project("{F2A71F9B-5D33-465A-A702-920D77279786}") = "FsClassic", "src\FsClassic\FsClassic.fsproj", "{00000006-0000-0000-0000-000000000000}"
EndProject
Project("{6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705}") = "FsSdk", "src\FsSdk\FsSdk.fsproj", "{00000007-0000-0000-0000-000000000000}"
EndProject
Project("{13B669BE-BB05-4DDF-9536-439F39A36129}") = "Cps", "src\Cps\Cps.msbuildproj", "{00000008-0000-0000-0000-000000000000}"
EndProject
Project("{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}") = "Cpp", "src\Cpp\Cpp.vcxproj", "{00000009-0000-0000-0000-000000000000}"
EndProject
Project("{C8D11400-126E-41CD-887F-60BD40844F9E}") = "Database", "src\Database\Database.dbproj", "{0000000A-0000-0000-0000-000000000000}"
EndProject
Project("{E6FDF86B-F3D1-11D4-8576-0002A516ECE8}") = "JSharp", "src\JSharp\JSharp.vjsproj", "{0000000B-0000-0000-0000-000000000000}"
EndProject
Project("{BBD0F5D1-1CC4-42FD-BA4C-A96779C64378}") = "Synergy", "src\Synergy\Synergy.synproj", "{0000000C-0000-0000-0000-000000000000}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Outside", "..\Outside\Outside.csproj", "{0000000D-0000-0000-0000-000000000000}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Nested", "src\Nested\Nested.csproj", "{0000000E-0000-0000-0000-000000000000}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Solution Items", "Solution Items", "{0000000F-0000-0000-0000-000000000000}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Inner Folder", "Inner Folder", "{00000010-0000-0000-0000-000000000000}"
EndProject
Project("{D954291E-2A0B-460D-934E-DC6B0785DB48}") = "Shared", "src\Shared\Shared.shproj", "{00000011-0000-0000-0000-000000000000}"
EndProject
Project("{E24C65DC-7377-472B-9ABA-BC803B73C61A}") = "Website", "http://localhost:8080/", "{00000012-0000-0000-0000-000000000000}"
EndProject
Project("{2CFEAB61-6A3B-4EB8-B523-560B4BEEF521}") = "WebDeployment", "src\WebDeployment\WebDeployment.wdproj", "{00000013-0000-0000-0000-000000000000}"
EndProject
Project("{11111111-2222-3333-4444-555555555555}") = "UnknownType", "src\UnknownType\UnknownType.csproj", "{00000014-0000-0000-0000-000000000000}"
EndProject
Project("{E53339B2-1760-4266-BCC7-CA923CBCF16C}") = "Docker", "docker-compose.dcproj", "{00000015-0000-0000-0000-000000000000}"
EndProject
Project("{00D1A9C2-B5F0-4AF3-8072-F6C62B433612}") = "SqlProj", "src\SqlProj\SqlProj.sqlproj", "{00000016-0000-0000-0000-000000000000}"
EndProject
Project("{54A90642-561A-4BB1-A94E-469ADEE60C69}") = "JavaScript", "src\JavaScript\JavaScript.esproj", "{00000017-0000-0000-0000-000000000000}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{00000010-0000-0000-0000-000000000000} = {0000000F-0000-0000-0000-000000000000}
		{0000000E-0000-0000-0000-000000000000} = {00000010-0000-0000-0000-000000000000}
	EndGlobalSection
EndGlobal
```

- [ ] **Step 2: Copy fixtures to the test output**

In `tests/ProjGraph.Tests.Unit.Core/ProjGraph.Tests.Unit.Core.csproj`, add this `ItemGroup` just before the existing `ProjectReference` `ItemGroup`. It must be `Content Include`: `.sln` files aren't in the default `None` glob, so `None Update` copies nothing.

```xml
  <ItemGroup>
    <Content Include="Parsers\Fixtures\**" CopyToOutputDirectory="PreserveNewest"/>
  </ItemGroup>

```

- [ ] **Step 3: Write the characterization tests**

Create `tests/ProjGraph.Tests.Unit.Core/Parsers/SlnParserCharacterizationTests.cs`:

```csharp
using ProjGraph.Core.Exceptions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.Core.Parsers;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.Core.Parsers;

/// <summary>
/// Pins which solution entries <see cref="SlnParser"/> returns and how their paths are resolved.
/// The expected values were recorded from Microsoft.Build's <c>SolutionFile</c>
/// (<c>KnownToBeMSBuildFormat</c> projects only) and must pass unchanged on the
/// SolutionPersistence implementation.
/// </summary>
[Trait("Category", "Core")]
public sealed class SlnParserCharacterizationTests
{
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Parsers", "Fixtures");

    private readonly SlnParser _parser = new(new PhysicalFileSystem());

    private static string Expected(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(FixtureDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void GetProjectPaths_ShouldReturnOnlyMsBuildFormatProjectsInFileOrder()
    {
        var paths = _parser.GetProjectPaths(Path.Combine(FixtureDirectory, "characterization.sln")).ToList();

        paths.Should().Equal(
            Expected("src/CsSdk/CsSdk.csproj"),
            Expected("src/CsClassic/CsClassic.csproj"),
            Expected("src/CsLowerGuid/CsLowerGuid.csproj"),
            Expected("src/VbClassic/VbClassic.vbproj"),
            Expected("src/VbSdk/VbSdk.vbproj"),
            Expected("src/FsClassic/FsClassic.fsproj"),
            Expected("src/FsSdk/FsSdk.fsproj"),
            Expected("src/Cps/Cps.msbuildproj"),
            Expected("src/Cpp/Cpp.vcxproj"),
            Expected("src/Database/Database.dbproj"),
            Expected("src/JSharp/JSharp.vjsproj"),
            Expected("src/Synergy/Synergy.synproj"),
            Expected("../Outside/Outside.csproj"),
            Expected("src/Nested/Nested.csproj"));
    }

    [Fact]
    public void GetProjectPaths_RelativeSolutionPath_ShouldStillReturnAbsoluteProjectPaths()
    {
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(),
            Path.Combine(FixtureDirectory, "characterization.sln"));

        var paths = _parser.GetProjectPaths(relative).ToList();

        paths.Should().HaveCount(14).And.OnlyContain(p => Path.IsPathFullyQualified(p));
        paths[0].Should().Be(Expected("src/CsSdk/CsSdk.csproj"));
    }

    [Theory]
    [InlineData("This is not a valid solution file {{{")]
    [InlineData("")]
    [InlineData("\nMicrosoft Visual Studio Solution File, Format Version 12.00\nProject(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Broken\"\nEndProject\n")]
    public void GetProjectPaths_MalformedSolution_ShouldThrowParsingException(string content)
    {
        using var temp = new TestDirectory();
        var path = temp.CreateFile("Broken.sln", content);

        var act = () => _parser.GetProjectPaths(path).ToList();

        act.Should().Throw<ParsingException>().WithMessage($"*{path}*");
    }

    [Fact]
    public void GetProjectPaths_HeaderOnlySolution_ShouldReturnEmpty()
    {
        using var temp = new TestDirectory();
        var path = temp.CreateFile("Empty.sln", "\nMicrosoft Visual Studio Solution File, Format Version 12.00\n");

        _parser.GetProjectPaths(path).Should().BeEmpty();
    }
}
```

- [ ] **Step 4: Run them against the current Microsoft.Build implementation**

Run: `dotnet test tests/ProjGraph.Tests.Unit.Core --filter "FullyQualifiedName~SlnParserCharacterizationTests"`
Expected: PASS, 6 tests. If `GetProjectPaths_ShouldReturnOnlyMsBuildFormatProjectsInFileOrder` reports "found empty collection", the fixture wasn't copied: check Step 2.

- [ ] **Step 5: Commit**

```bash
git add tests/ProjGraph.Tests.Unit.Core/Parsers/Fixtures/characterization.sln \
        tests/ProjGraph.Tests.Unit.Core/Parsers/SlnParserCharacterizationTests.cs \
        tests/ProjGraph.Tests.Unit.Core/ProjGraph.Tests.Unit.Core.csproj
git commit -m "test(core): characterize SlnParser project selection with a committed .sln fixture"
```

---

### Task 3: Snapshot the MCP tool and prompt surface

This captures what the **current** server advertises, before Task 8 changes its JSON setup. The test writes the snapshot file when it's missing and fails once on purpose. The second run must pass.

**Files:**
- Create: `tests/ProjGraph.Tests.Integration.Mcp/Helpers/McpServerProcess.cs`
- Modify: `tests/ProjGraph.Tests.Integration.Mcp/McpTransportTests.cs`
- Create: `tests/ProjGraph.Tests.Integration.Mcp/McpSurfaceSnapshotTests.cs`
- Create (generated): `tests/ProjGraph.Tests.Integration.Mcp/Snapshots/mcp-surface.json`

**Interfaces:**
- Consumes: `TestPathHelper.GetRootPath(string)`; ModelContextProtocol client API (`StdioClientTransport`, `McpClient.CreateAsync`, `ListToolsAsync`, `ListPromptsAsync`, `McpClientTool.ProtocolTool.InputSchema` (a `JsonElement`), and `McpClientPrompt.ProtocolPrompt.Arguments`).
- Produces: `internal static class McpServerProcess` with `public static Task<McpClient> ConnectAsync()`. The snapshot is re-checked in Tasks 8 and 10.

- [ ] **Step 1: Move the server launcher into a helper**

Create `tests/ProjGraph.Tests.Integration.Mcp/Helpers/McpServerProcess.cs`. The body moves unchanged from `McpTransportTests`.

```csharp
using ModelContextProtocol.Client;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Integration.Mcp.Helpers;

/// <summary>
/// Launches the real MCP server executable from ProjGraph.Mcp's own build output and connects a
/// <see cref="McpClient"/> to it over the stdio transport.
/// </summary>
internal static class McpServerProcess
{
    /// <summary>
    /// Connects a client to the server apphost in ProjGraph.Mcp's own build output (guaranteed
    /// up to date by the ProjectReference). ProjGraph.Mcp is a self-contained exe, so its build
    /// lands in a RID subdirectory and must be launched via its apphost — the DLL that the
    /// ProjectReference copies into the test output has no runtime next to it and cannot start.
    /// The client deliberately advertises no capabilities — in particular no workspace roots.
    /// </summary>
    /// <returns>A connected client; disposing it stops the server process.</returns>
    public static async Task<McpClient> ConnectAsync()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ProjGraph e2e",
            Command = LocateServerExecutable()
        });

        return await McpClient.CreateAsync(transport);
    }

    private static string LocateServerExecutable()
    {
        // .../tests/ProjGraph.Tests.Integration.Mcp/bin/{Configuration}/{tfm}/
        var testOutput = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var binRoot = TestPathHelper.GetRootPath(Path.Combine(
            "src", "ProjGraph.Mcp", "bin", testOutput.Parent!.Name, testOutput.Name));
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
}
```

- [ ] **Step 2: Point `McpTransportTests` at the helper**

In `tests/ProjGraph.Tests.Integration.Mcp/McpTransportTests.cs`:
1. Delete the `ConnectAsync()` method, its XML doc comment, and the `LocateServerExecutable()` method (everything between the class's opening brace and `private static string JoinText`).
2. Replace both `await using var client = await ConnectAsync();` lines with `await using var client = await McpServerProcess.ConnectAsync();`.
3. Replace the `using` block at the top of the file with:

```csharp
using ModelContextProtocol.Protocol;
using ProjGraph.Tests.Integration.Mcp.Helpers;
```

- [ ] **Step 3: Write the snapshot test**

Create `tests/ProjGraph.Tests.Integration.Mcp/McpSurfaceSnapshotTests.cs`:

```csharp
using ProjGraph.Tests.Integration.Mcp.Helpers;
using ProjGraph.Tests.Shared.Helpers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProjGraph.Tests.Integration.Mcp;

/// <summary>
/// Pins the tool and prompt surface the real server executable advertises: names, descriptions,
/// tool input schemas, and prompt arguments. It guards serializer changes, such as moving
/// registration onto source-generated JSON metadata, that would silently change what clients see.
/// </summary>
public sealed class McpSurfaceSnapshotTests
{
    private const string SnapshotRelativePath = "tests/ProjGraph.Tests.Integration.Mcp/Snapshots/mcp-surface.json";

    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    [Fact]
    public async Task ToolsAndPrompts_ShouldMatchTheCommittedSnapshot()
    {
        await using var client = await McpServerProcess.ConnectAsync();
        var tools = await client.ListToolsAsync();
        var prompts = await client.ListPromptsAsync();

        var actual = new JsonObject
        {
            ["tools"] = new JsonArray([
                .. tools.OrderBy(tool => tool.Name, StringComparer.Ordinal).Select(tool => new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["inputSchema"] = JsonNode.Parse(tool.ProtocolTool.InputSchema.GetRawText())
                })
            ]),
            ["prompts"] = new JsonArray([
                .. prompts.OrderBy(prompt => prompt.Name, StringComparer.Ordinal).Select(prompt => new JsonObject
                {
                    ["name"] = prompt.Name,
                    ["description"] = prompt.Description,
                    ["arguments"] = new JsonArray([
                        .. (prompt.ProtocolPrompt.Arguments ?? []).Select(argument => new JsonObject
                        {
                            ["name"] = argument.Name,
                            ["description"] = argument.Description,
                            ["required"] = argument.Required
                        })
                    ])
                })
            ])
        };

        var snapshotPath = TestPathHelper.GetRootPath(SnapshotRelativePath);
        if (!File.Exists(snapshotPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            await File.WriteAllTextAsync(snapshotPath, actual.ToJsonString(IndentedOptions) + "\n");
            Assert.Fail($"Snapshot created at {snapshotPath}. Review and commit it, then re-run.");
        }

        var expected = JsonNode.Parse(await File.ReadAllTextAsync(snapshotPath));
        JsonNode.DeepEquals(expected, actual).Should().BeTrue(
            $"the advertised MCP surface must match {SnapshotRelativePath}; actual:\n{actual.ToJsonString(IndentedOptions)}");
    }
}
```

- [ ] **Step 4: Run it to generate the snapshot**

Run: `dotnet test tests/ProjGraph.Tests.Integration.Mcp --filter "FullyQualifiedName~McpSurfaceSnapshotTests|FullyQualifiedName~McpTransportTests"`
Expected: `McpSurfaceSnapshotTests` FAILS with `Snapshot created at …/Snapshots/mcp-surface.json. Review and commit it, then re-run.`, and both `McpTransportTests` PASS.

- [ ] **Step 5: Review the generated snapshot**

Open `tests/ProjGraph.Tests.Integration.Mcp/Snapshots/mcp-surface.json` and check that:
- `tools` lists `get_class_diagram`, `get_erd`, `get_project_graph`, and `get_project_stats`.
- `get_class_diagram.inputSchema.properties.options.properties` has camelCase keys: `maxDepth`, `includeInheritance`, `includeDependencies`, `includeProperties`, and `includeFunctions`, each with a `description` and a `default`.
- `prompts` lists `architecture_review`, `class_structure_review`, `database_schema_review`, and `dependency_analysis`.

- [ ] **Step 6: Re-run and confirm it passes**

Run: `dotnet test tests/ProjGraph.Tests.Integration.Mcp --filter "FullyQualifiedName~McpSurfaceSnapshotTests|FullyQualifiedName~McpTransportTests"`
Expected: PASS, 3 tests.

- [ ] **Step 7: Commit**

```bash
git add tests/ProjGraph.Tests.Integration.Mcp/Helpers/McpServerProcess.cs \
        tests/ProjGraph.Tests.Integration.Mcp/McpTransportTests.cs \
        tests/ProjGraph.Tests.Integration.Mcp/McpSurfaceSnapshotTests.cs \
        tests/ProjGraph.Tests.Integration.Mcp/Snapshots/mcp-surface.json
git commit -m "test(mcp): snapshot the advertised tool schemas and prompts before the JSON change"
```

---

### Task 4: `ProjectParser` on `System.Xml.Linq`

**Files:**
- Replace: `src/ProjGraph.Lib.Core/Parsers/ProjectParser.cs`

**Interfaces:**
- Consumes: `IFileSystem.ReadAllText(string)`, `IFileSystem.FileExists(string)`, and `IFileSystem.GetCurrentDirectory()`.
- Produces: no public API change. `ProjectParser(IFileSystem)` and `Parse(string)` keep their signatures. `Microsoft.Build` is still referenced after this task, but nothing in `ProjectParser` uses it any more.

Behavior rules the new code implements (each is pinned by Task 1 or by the existing `ProjectParserTests`/`ProjectParserCoverageTests`):
- **Reading.** All three read sites go through `LoadXml(path)`, which uses `IFileSystem.ReadAllText` and `XDocument.Parse`.
- **Structure.** The root must be `Project` with no namespace or the MSBuild 2003 namespace. An item whose parent is an `ItemGroup` that isn't inside a `Target` needs at least one of `Include`/`Update`/`Remove`, and none of them may be empty. Anything else throws `InvalidDataException`.
- **Properties.** A property is any element whose parent is a `PropertyGroup`, wherever that group is (`Target` and `Choose/When` included). Names match case-insensitively, the first match in document order wins, and null or whitespace counts as undefined. Values are not trimmed.
- **Items.** An item is an element whose parent is an `ItemGroup` and whose local name equals the item type, case-sensitively. `Include` defaults to `""`. `Version` metadata comes from an attribute or a child element, and its name is case-sensitive.
- **Errors.** `XmlException`, `IOException`, `UnauthorizedAccessException`, and `InvalidDataException` become `ParsingException` for the project file. For props files they are swallowed and the walk up the directory tree continues.

- [ ] **Step 1: Replace the file**

Replace the entire contents of `src/ProjGraph.Lib.Core/Parsers/ProjectParser.cs` with:

```csharp
using ProjGraph.Core.Exceptions;
using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ProjGraph.Lib.Core.Parsers;

/// <summary>
/// Provides functionality to parse project files and extract project details and references.
/// </summary>
/// <param name="fileSystem">The file system abstraction for file operations.</param>
public sealed class ProjectParser(IFileSystem fileSystem) : IProjectParser
{
    /// <summary>The legacy MSBuild 2003 XML namespace, still used by non-SDK-style projects.</summary>
    private const string MsBuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

    /// <summary>The item operations of which an item outside a <c>Target</c> must define one.</summary>
    private static readonly XName[] ItemOperations = ["Include", "Update", "Remove"];

    /// <summary>
    /// Parses the specified project file and extracts project details and its references.
    /// </summary>
    /// <param name="projectPath">The file path to the project file to be parsed.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    /// <item>
    /// <description>The parsed <see cref="Project"/> object with details such as ID, name, path, framework, and type.</description>
    /// </item>
    /// <item>
    /// <description>A collection of project references as strings.</description>
    /// </item>
    /// <item>
    /// <description>A collection of NuGet package references as <see cref="PackageReference"/> objects.</description>
    /// </item>
    /// </list>
    /// </returns>
    /// <exception cref="ParsingException">Thrown when the project file cannot be parsed.</exception>
    public (Project Project, IEnumerable<string> ProjectReferences, IEnumerable<PackageReference> PackageReferences)
        Parse(string projectPath)
    {
        XDocument root;
        try
        {
            root = LoadXml(projectPath);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException
                                       or InvalidDataException)
        {
            throw new ParsingException($"Failed to parse project file: {projectPath}", ex);
        }

        var name = Path.GetFileNameWithoutExtension(projectPath);
        var relativePath = Path.GetRelativePath(fileSystem.GetCurrentDirectory(), projectPath);

        // Fast extraction of properties from the project itself (a null-or-whitespace value counts
        // as "not defined" so it falls back to Directory.Build.props / "unknown").
        var ownFramework = GetPropertyValue(root, "TargetFramework") ?? GetPropertyValue(root, "TargetFrameworks");
        var ownOutputType = GetPropertyValue(root, "OutputType");
        var ownIsTestProject = GetPropertyValue(root, "IsTestProject");

        // Repos commonly set these centrally in Directory.Build.props; fall back to it (a single
        // walk up the tree) only for the values the project does not define locally.
        Dictionary<string, string>? inherited = null;
        if (ownFramework is null || ownOutputType is null || ownIsTestProject is null)
        {
            inherited = ResolveInheritedProperties(projectPath,
                ["TargetFramework", "TargetFrameworks", "OutputType", "IsTestProject"]);
        }

        var framework = ownFramework
                        ?? inherited?.GetValueOrDefault("TargetFramework")
                        ?? inherited?.GetValueOrDefault("TargetFrameworks")
                        ?? "unknown";
        var outputType = ownOutputType ?? inherited?.GetValueOrDefault("OutputType") ?? "";
        var isTestProject = ownIsTestProject ?? inherited?.GetValueOrDefault("IsTestProject");

        var type = outputType.Contains("Exe", StringComparison.OrdinalIgnoreCase)
            ? ProjectType.Executable
            : ProjectType.Library;

        if (name.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(isTestProject, "true", StringComparison.OrdinalIgnoreCase))
        {
            type = ProjectType.Test;
        }

        var id = GenerateDeterministicId(projectPath);
        var project = new Project(id, name, projectPath, relativePath, framework, type);

        var projectReferences = GetItems(root, "ProjectReference")
            .Select(GetInclude)
            .ToList();

        var packageReferences = GetItems(root, "PackageReference")
            .Select(item =>
            {
                var include = GetInclude(item);
                var version = GetMetadataValue(item, "Version");
                if (string.IsNullOrEmpty(version))
                {
                    version = ResolveCentralPackageVersion(projectPath, include);
                }

                return new PackageReference(include, version ?? "unknown");
            })
            .ToList();

        return (project, projectReferences, packageReferences);
    }

    /// <summary>
    /// Reads and parses an MSBuild XML file through the file-system abstraction, rejecting the
    /// structural errors MSBuild itself rejects: a root element other than <c>Project</c>, a
    /// namespace other than none or the MSBuild 2003 namespace, and an item outside a
    /// <c>Target</c> without a non-empty <c>Include</c>, <c>Update</c>, or <c>Remove</c>.
    /// </summary>
    /// <param name="path">The project or props file path.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="XmlException">Thrown when the file is not well-formed XML.</exception>
    /// <exception cref="InvalidDataException">Thrown when the XML is not a valid MSBuild project.</exception>
    private XDocument LoadXml(string path)
    {
        var document = XDocument.Parse(fileSystem.ReadAllText(path));
        var root = document.Root!;
        if (root.Name.LocalName != "Project" ||
            root.Name.NamespaceName is not ("" or MsBuildNamespace))
        {
            throw new InvalidDataException($"'{path}' is not an MSBuild project: unexpected root element {root.Name}.");
        }

        var invalidItem = document.Descendants()
            .Where(e => e.Parent?.Name.LocalName == "ItemGroup" && !e.Ancestors().Any(a => a.Name.LocalName == "Target"))
            .FirstOrDefault(e =>
            {
                var operations = ItemOperations.Select(e.Attribute).OfType<XAttribute>().ToList();
                return operations.Count == 0 || operations.Exists(a => a.Value.Length == 0);
            });
        if (invalidItem is not null)
        {
            throw new InvalidDataException(
                $"'{path}' is not a valid MSBuild project: <{invalidItem.Name.LocalName}> needs a non-empty Include, Update, or Remove.");
        }

        return document;
    }

    /// <summary>
    /// Reads a single MSBuild property value using a case-insensitive name match (MSBuild property
    /// names are case-insensitive). A property is any element whose parent is a
    /// <c>PropertyGroup</c>, wherever that group sits (including inside <c>Target</c> and
    /// <c>Choose</c>), and the first one in document order wins. A null-or-whitespace value is
    /// treated as undefined and returns <see langword="null"/>.
    /// </summary>
    /// <param name="document">The project or props document to read from.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property value, or <see langword="null"/> when unset or whitespace.</returns>
    private static string? GetPropertyValue(XDocument document, string name)
    {
        var value = document.Descendants()
            .FirstOrDefault(e => e.Parent?.Name.LocalName == "PropertyGroup" &&
                                 string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Returns the items of the given type: elements whose parent is an <c>ItemGroup</c> and whose
    /// name matches <paramref name="itemType"/> exactly (item types are matched case-sensitively).
    /// </summary>
    /// <param name="document">The project or props document to read from.</param>
    /// <param name="itemType">The item type, e.g. <c>PackageReference</c>.</param>
    /// <returns>The matching item elements in document order.</returns>
    private static IEnumerable<XElement> GetItems(XDocument document, string itemType)
    {
        return document.Descendants()
            .Where(e => e.Parent?.Name.LocalName == "ItemGroup" && e.Name.LocalName == itemType);
    }

    /// <summary>
    /// Returns the item's <c>Include</c> attribute, or an empty string for <c>Update</c>/<c>Remove</c> items.
    /// </summary>
    /// <param name="item">The item element.</param>
    /// <returns>The include value.</returns>
    private static string GetInclude(XElement item)
    {
        return item.Attribute("Include")?.Value ?? "";
    }

    /// <summary>
    /// Reads item metadata expressed either as an attribute or as a child element. Metadata names
    /// are matched case-sensitively.
    /// </summary>
    /// <param name="item">The item element.</param>
    /// <param name="name">The metadata name, e.g. <c>Version</c>.</param>
    /// <returns>The metadata value, or <see langword="null"/> when absent.</returns>
    private static string? GetMetadataValue(XElement item, string name)
    {
        return item.Attribute(name)?.Value
               ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
    }

    /// <summary>
    /// Resolves MSBuild properties inherited from <c>Directory.Build.props</c> for a project that
    /// does not define them locally. Walks up the directory tree from the project file, nearest
    /// first, recording the first value found for each requested property.
    /// </summary>
    /// <param name="projectPath">The path to the project file.</param>
    /// <param name="names">The property names to resolve.</param>
    /// <returns>A map of the resolved property names to their inherited values.</returns>
    private Dictionary<string, string> ResolveInheritedProperties(
        string projectPath,
        IReadOnlyCollection<string> names)
    {
        // Property names are matched case-insensitively (MSBuild semantics).
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var directory = Path.GetDirectoryName(Path.GetFullPath(projectPath));

        while (directory is not null && result.Count < names.Count)
        {
            MergeInheritedProperties(Path.Combine(directory, "Directory.Build.props"), names, result);
            directory = Path.GetDirectoryName(directory);
        }

        return result;
    }

    /// <summary>
    /// Merges the requested properties found in a single <c>Directory.Build.props</c> file into
    /// <paramref name="result"/>, keeping the nearest (first-seen) value for each name. A missing or
    /// unreadable props file contributes nothing.
    /// </summary>
    /// <param name="propsFile">The <c>Directory.Build.props</c> path to read.</param>
    /// <param name="names">The property names still being resolved.</param>
    /// <param name="result">The accumulator of resolved property values, augmented in place.</param>
    private void MergeInheritedProperties(
        string propsFile,
        IReadOnlyCollection<string> names,
        Dictionary<string, string> result)
    {
        if (!fileSystem.FileExists(propsFile))
        {
            return;
        }

        XDocument propsRoot;
        try
        {
            propsRoot = LoadXml(propsFile);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException
                                       or InvalidDataException)
        {
            // If we can't read the props file, continue searching up.
            return;
        }

        foreach (var propertyName in names)
        {
            if (result.ContainsKey(propertyName))
            {
                continue;
            }

            var value = GetPropertyValue(propsRoot, propertyName);
            if (value is not null)
            {
                result[propertyName] = value;
            }
        }
    }

    /// <summary>
    /// Resolves the version of a NuGet package from a <c>Directory.Packages.props</c> file
    /// when Central Package Management is used (i.e., no Version attribute on the PackageReference).
    /// Walks up the directory tree from the project file until a matching props file is found.
    /// </summary>
    /// <param name="projectPath">The path to the project file.</param>
    /// <param name="packageName">The package name to look up.</param>
    /// <returns>The resolved version string, or <see langword="null"/> if not found.</returns>
    private string? ResolveCentralPackageVersion(string projectPath, string packageName)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(projectPath));

        while (directory is not null)
        {
            var propsFile = Path.Combine(directory, "Directory.Packages.props");
            if (fileSystem.FileExists(propsFile))
            {
                try
                {
                    var propsRoot = LoadXml(propsFile);
                    var packageVersion = GetItems(propsRoot, "PackageVersion")
                        .FirstOrDefault(i => GetInclude(i).Equals(packageName, StringComparison.OrdinalIgnoreCase));
                    var version = packageVersion is null ? null : GetMetadataValue(packageVersion, "Version");

                    if (!string.IsNullOrEmpty(version))
                    {
                        return version;
                    }
                }
                catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException
                                               or InvalidDataException)
                {
                    // If we can't read the props file, continue searching up
                }
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    /// <summary>
    /// Generates a deterministic GUID from the normalized absolute path of the project file.
    /// Parsing the same project twice will always yield the same ID.
    /// </summary>
    /// <param name="projectPath">The file path of the project.</param>
    /// <returns>A deterministic <see cref="Guid"/> derived from the normalized path.</returns>
    private static Guid GenerateDeterministicId(string projectPath)
    {
        var normalizedPath = NormalizePath(projectPath);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// Normalizes a file path for use in deterministic ID generation.
    /// On case-insensitive file systems (Windows/macOS), the path is case-folded.
    /// On case-sensitive file systems (Linux), the exact case is preserved to avoid collisions.
    /// Directory separators are normalized to forward slashes on all platforms.
    /// </summary>
    /// <param name="path">The file path to normalize.</param>
    /// <returns>The normalized path string.</returns>
    private static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path).Replace('\\', '/');

        // Only case-fold on case-insensitive file systems (Windows and macOS)
        // Linux file systems are typically case-sensitive, so preserve exact case
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? fullPath.ToUpperInvariant()
            : fullPath;
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/ProjGraph.Lib.Core`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 3: Run every parser test**

Run: `dotnet test tests/ProjGraph.Tests.Unit.Core --filter "FullyQualifiedName~ProjectParser"`
Expected: PASS. This covers `ProjectParserCharacterizationTests`, `ProjectParserTests`, and `ProjectParserCoverageTests`, 55 tests in total.

- [ ] **Step 4: Run the suites that parse real projects**

Run: `dotnet test tests/ProjGraph.Tests.Unit.Dependencies && dotnet test tests/ProjGraph.Tests.Integration.Cli`
Expected: PASS (80 and 82 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ProjGraph.Lib.Core/Parsers/ProjectParser.cs
git commit -m "refactor(core): read project and props files with System.Xml.Linq instead of Microsoft.Build"
```

---

### Task 5: `SlnParser` on SolutionPersistence

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj`
- Replace: `src/ProjGraph.Lib.Core/Parsers/SlnParser.cs`

**Interfaces:**
- Consumes: `IFileSystem.FileExists`, `GetFullPath`, `ReadAllText`, `GetDirectoryName`, and `Combine`; `SolutionSerializers.SlnFileV12.OpenAsync(Stream, CancellationToken)` returns `Task<SolutionModel>`; `SolutionModel.SolutionProjects` is a list of `SolutionProjectModel` with `Guid TypeId` and `string FilePath` (forward slashes, relative to the solution); `SolutionException`.
- Produces: no public API change. `GetProjectPaths(string)` now returns a materialized `List<string>`.

- [ ] **Step 1: Add the package version**

In `Directory.Packages.props`, add this line between the `Microsoft.NET.Test.Sdk` and `Microsoft.VisualStudio.Threading.Analyzers` entries:

```xml
    <PackageVersion Include="Microsoft.VisualStudio.SolutionPersistence" Version="1.0.52"/>
```

- [ ] **Step 2: Reference it from Lib.Core**

In `src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj`, add this line after `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions"/>`:

```xml
    <PackageReference Include="Microsoft.VisualStudio.SolutionPersistence"/>
```

- [ ] **Step 3: Replace the parser**

Replace the entire contents of `src/ProjGraph.Lib.Core/Parsers/SlnParser.cs` with the code below. Two analyzer workarounds in it are required, not stylistic:
- `OpenSolution` keeps the `VSTHRD002` suppression.
- The `MemoryStream` isn't disposed, because `using` around the bridged call trips `CA2025`.

```csharp
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using ProjGraph.Core.Exceptions;
using ProjGraph.Lib.Core.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ProjGraph.Lib.Core.Parsers;

/// <summary>
/// Provides functionality to parse solution files and extract project paths.
/// </summary>
/// <param name="fileSystem">The file system abstraction for file operations.</param>
public sealed class SlnParser(IFileSystem fileSystem) : ISlnParser
{
    /// <summary>
    /// The project type GUIDs that MSBuild's <c>SolutionFile</c> classifies as
    /// <c>KnownToBeMSBuildFormat</c>: C#, Visual Basic, and F# (classic and SDK-style), the generic
    /// CPS project, C++, and the legacy database, J#, and Synergy project types. Every other type
    /// (solution folders, shared, website, and unrecognised projects) is skipped.
    /// </summary>
    private static readonly HashSet<Guid> MsBuildProjectTypes =
    [
        new("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"), // C#
        new("9A19103F-16F7-4668-BE54-9A1E7A4F7556"), // C# (SDK-style)
        new("F184B08F-C81C-45F6-A57F-5ABD9991F28F"), // Visual Basic
        new("778DAE3C-4631-46EA-AA77-85C1314464D9"), // Visual Basic (SDK-style)
        new("F2A71F9B-5D33-465A-A702-920D77279786"), // F#
        new("6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705"), // F# (SDK-style)
        new("13B669BE-BB05-4DDF-9536-439F39A36129"), // CPS (generic SDK-style)
        new("8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942"), // C++
        new("C8D11400-126E-41CD-887F-60BD40844F9E"), // Database
        new("E6FDF86B-F3D1-11D4-8576-0002A516ECE8"), // J#
        new("BBD0F5D1-1CC4-42FD-BA4C-A96779C64378") // Synergy
    ];

    /// <summary>
    /// Retrieves the paths of all projects in the specified solution file.
    /// </summary>
    /// <param name="path">The file path to the solution file.</param>
    /// <returns>
    /// An enumerable collection of project file paths contained in the solution.
    /// If the solution file does not exist, an empty collection is returned.
    /// </returns>
    /// <exception cref="ParsingException">Thrown when the solution file cannot be parsed.</exception>
    public IEnumerable<string> GetProjectPaths(string path)
    {
        if (!fileSystem.FileExists(path))
        {
            return [];
        }

        var fullPath = fileSystem.GetFullPath(path);

        SolutionModel solution;
        try
        {
            solution = OpenSolution(fileSystem.ReadAllText(fullPath));
        }
        catch (Exception ex) when (ex is SolutionException or IOException or UnauthorizedAccessException)
        {
            throw new ParsingException($"Failed to read or parse .sln file: {path}", ex);
        }

        var solutionDirectory = fileSystem.GetDirectoryName(fullPath) ?? "";
        return solution.SolutionProjects
            .Where(p => MsBuildProjectTypes.Contains(p.TypeId))
            .Select(p => fileSystem.GetFullPath(fileSystem.Combine(solutionDirectory,
                p.FilePath.Replace('\\', Path.DirectorySeparatorChar))))
            .ToList();
    }

    /// <summary>
    /// Parses solution text with the <c>.sln</c> serializer.
    /// </summary>
    /// <param name="content">The solution file text.</param>
    /// <returns>The parsed solution model.</returns>
    [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits",
        Justification = "The serializer reads an in-memory stream, so the wait never blocks on I/O, and " +
                        "neither the CLI nor the MCP server calls this on a thread with a synchronization " +
                        "context. ISlnParser stays synchronous until the planned CancellationToken work.")]
    private static SolutionModel OpenSolution(string content)
    {
        // Not disposed: a MemoryStream over a byte array holds no unmanaged resources, and disposing it
        // around the bridged call trips CA2025 even though GetResult completes the read first.
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content), writable: false);
        return SolutionSerializers.SlnFileV12.OpenAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build src/ProjGraph.Lib.Core`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 5: Run every solution-parser test**

Run: `dotnet test tests/ProjGraph.Tests.Unit.Core --filter "FullyQualifiedName~SlnParser"`
Expected: PASS. This covers `SlnParserCharacterizationTests` and `SlnParserTests`, 10 tests in total.

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj src/ProjGraph.Lib.Core/Parsers/SlnParser.cs
git commit -m "refactor(core): parse .sln files with Microsoft.VisualStudio.SolutionPersistence"
```

---

### Task 6: Remove Microsoft.Build and Workspaces.MSBuild

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj`
- Modify: `src/ProjGraph.Lib.ClassDiagram/ProjGraph.Lib.ClassDiagram.csproj`
- Modify: `CLAUDE.md`
- Modify: `src/ProjGraph.Lib.Core/README.md`
- Modify: `tests/ProjGraph.Tests.Unit.Core/ProjectParserCoverageTests.cs`

**Interfaces:**
- Consumes: Tasks 4 and 5 (nothing imports `Microsoft.Build.*` any more).
- Produces: a dependency graph with no Microsoft.Build assemblies and no `BuildHost-*` publish folders.

- [ ] **Step 1: Delete the package references**

- In `src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj`, delete `<PackageReference Include="Microsoft.Build"/>`.
- In `src/ProjGraph.Lib.ClassDiagram/ProjGraph.Lib.ClassDiagram.csproj`, delete this whole block, including the blank line after it:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild"/>
  </ItemGroup>

```

- In `Directory.Packages.props`, delete these two lines:

```xml
    <PackageVersion Include="Microsoft.Build" Version="18.9.6"/>
    <PackageVersion Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="5.9.0"/>
```

- [ ] **Step 2: Verify nothing references them**

Run: `git grep -n "Microsoft\.Build\b\|Workspaces\.MSBuild" -- src tests Directory.Packages.props`
Expected: no output.

- [ ] **Step 3: Update the docs**

In `CLAUDE.md`, replace this `ProjGraph.Lib.Dependencies` bullet:

```markdown
- **`ProjGraph.Lib.Dependencies`** — Builds dependency graphs from solution/project files by parsing them directly via `Microsoft.Build.Construction` (the in-house `SlnParser`/`SlnxParser`/`ProjectParser` in `Lib.Core`); computes stats using `TarjanSccAlgorithm` for cycle detection.
```

with:

```markdown
- **`ProjGraph.Lib.Dependencies`** — Builds dependency graphs from solution/project files by parsing them directly with the in-house parsers in `Lib.Core` (`SlnParser` on `Microsoft.VisualStudio.SolutionPersistence`, `SlnxParser` and `ProjectParser` on `System.Xml.Linq`; nothing is evaluated); computes stats using `TarjanSccAlgorithm` for cycle detection.
```

In `src/ProjGraph.Lib.Core/README.md`, replace:

```markdown
- **Solution & project parsers** for `.sln`, `.slnx`, and `.csproj` files via MSBuild
```

with:

```markdown
- **Solution & project parsers** for `.sln` (via Microsoft.VisualStudio.SolutionPersistence), `.slnx`, and `.csproj` files (via System.Xml.Linq); project files are read, never evaluated
```

In `tests/ProjGraph.Tests.Unit.Core/ProjectParserCoverageTests.cs`, replace the stale summary on `WriteFile`:

```csharp
    /// <summary>
    /// Writes a project file inside a uniquely named subdirectory so that MSBuild's global
    /// <c>ProjectRootElement</c> cache never serves a sibling test's file for the same path.
    /// </summary>
```

with:

```csharp
    /// <summary>
    /// Writes a file inside this test's own temporary directory.
    /// </summary>
```

- [ ] **Step 4: Build and test the solution**

Run: `dotnet build ProjGraph.slnx && dotnet test ProjGraph.slnx`
Expected: Build succeeded with 0 warnings, and every test project PASSES.

- [ ] **Step 5: Confirm the publish output is free of MSBuild**

Run: `dotnet publish src/ProjGraph.Cli -c Release -o /tmp/pg-cli-publish && ls /tmp/pg-cli-publish | grep -iE "BuildHost|Microsoft\.Build"`
Expected: no output (`grep` exits 1). `Microsoft.VisualStudio.SolutionPersistence.dll` is present.

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj \
        src/ProjGraph.Lib.ClassDiagram/ProjGraph.Lib.ClassDiagram.csproj CLAUDE.md \
        src/ProjGraph.Lib.Core/README.md tests/ProjGraph.Tests.Unit.Core/ProjectParserCoverageTests.cs
git commit -m "chore(deps): drop Microsoft.Build and the unused Workspaces.MSBuild package"
```

---

### Task 7: `CompilationFactory` uses embedded reference assemblies

**Files:**
- Modify: `tests/ProjGraph.Tests.Unit.Core/CompilationFactoryCoverageTests.cs`
- Modify: `src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj`
- Replace: `src/ProjGraph.Lib.Core/Infrastructure/CompilationFactory.cs`

**Interfaces:**
- Consumes: `MetadataReference.CreateFromStream(Stream, MetadataReferenceProperties, DocumentationProvider?, string? filePath)`. `filePath` becomes the reference's `Display`.
- Produces: `CompilationFactory.CreateCompilation(IEnumerable<SyntaxTree>)` (unchanged signature). Its references are `refs/netstandard.dll`, `refs/System.Collections.dll`, `refs/System.ComponentModel.Annotations.dll`, and `refs/System.Runtime.dll`, plus EF Core when it's loaded from a file. Task 9 adds an attribute to `TryAddEntityFrameworkCoreReference`.

- [ ] **Step 1: Write the failing tests**

In `tests/ProjGraph.Tests.Unit.Core/CompilationFactoryCoverageTests.cs`, add these two tests at the end of the class, before its closing `}`. Pass the expected list as a collection expression: with separate string arguments, FluentAssertions reads the second one as a `because` format string, and CA2241 fails the build.

```csharp
    [Fact]
    public void CreateCompilation_ShouldReferenceTheEmbeddedReferenceAssemblies()
    {
        // The BCL references are embedded in Lib.Core rather than read from Assembly.Location, which
        // is empty under Native AOT. JIT runs must use the same set so the unit tests exercise it.
        var compilation = _sut.CreateCompilation([]);
        var displays = compilation.References.Select(r => r.Display ?? "").ToList();

        displays.Should().Contain([
            "refs/netstandard.dll",
            "refs/System.Collections.dll",
            "refs/System.ComponentModel.Annotations.dll",
            "refs/System.Runtime.dll"
        ]);
        displays.Should().OnlyContain(d =>
            d.StartsWith("refs/", StringComparison.Ordinal) ||
            d.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateCompilation_CollectionsAndDataAnnotations_ShouldBindWithoutErrorTypes()
    {
        var tree = CSharpSyntaxTree.ParseText(
            """
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations;
            namespace Test;
            public class Order
            {
                [Required]
                public string Name { get; set; } = "";
                public List<string> Lines { get; set; } = [];
                public IEnumerable<int> Quantities { get; set; } = [];
            }
            """);

        var compilation = _sut.CreateCompilation([tree]);
        var order = compilation.GetTypeByMetadataName("Test.Order")!;

        compilation.GetDiagnostics().Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        order.GetMembers().OfType<IPropertySymbol>().Should().OnlyContain(p => p.Type.TypeKind != TypeKind.Error);
        order.GetMembers("Name").Single().GetAttributes().Should().ContainSingle()
            .Which.AttributeClass!.TypeKind.Should().NotBe(TypeKind.Error);
    }
```

- [ ] **Step 2: Run them to verify the first one fails**

Run: `dotnet test tests/ProjGraph.Tests.Unit.Core --filter "FullyQualifiedName~CompilationFactoryCoverageTests"`
Expected: `CreateCompilation_ShouldReferenceTheEmbeddedReferenceAssemblies` FAILS, because the references are still file paths such as `/usr/share/dotnet/shared/…/System.Private.CoreLib.dll`. `CreateCompilation_CollectionsAndDataAnnotations_ShouldBindWithoutErrorTypes` passes: it's a regression guard for the switch.

- [ ] **Step 3: Embed the reference assemblies with a build guard**

In `src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj`, insert this after the "Include README and icon in package" `ItemGroup` and before the `ProjectReference` `ItemGroup`:

```xml
  <!-- Reference assemblies that CompilationFactory hands to Roslyn. They are embedded rather than
       read from the runtime directory because Assembly.Location is empty under Native AOT. -->
  <PropertyGroup>
    <ReferenceAssemblyDirectory>$(NetCoreTargetingPackRoot)/Microsoft.NETCore.App.Ref/$(BundledNETCoreAppPackageVersion)/ref/$(TargetFramework)/</ReferenceAssemblyDirectory>
  </PropertyGroup>

  <ItemGroup>
    <EmbeddedReferenceAssembly Include="System.Runtime.dll;System.Collections.dll;System.ComponentModel.Annotations.dll;netstandard.dll"/>
    <EmbeddedResource Include="@(EmbeddedReferenceAssembly->'$(ReferenceAssemblyDirectory)%(Identity)')"
                      LogicalName="refs/%(Filename)%(Extension)"
                      Visible="false"/>
  </ItemGroup>

  <Target Name="VerifyEmbeddedReferenceAssemblies" BeforeTargets="PrepareForBuild">
    <Error Condition="!Exists('$(ReferenceAssemblyDirectory)%(EmbeddedReferenceAssembly.Identity)')"
           Text="Reference assembly '%(EmbeddedReferenceAssembly.Identity)' was not found in '$(ReferenceAssemblyDirectory)'. Install the .NET $(BundledNETCoreAppPackageVersion) targeting pack (it ships with the SDK)."/>
  </Target>
```

- [ ] **Step 4: Replace the factory**

Replace the entire contents of `src/ProjGraph.Lib.Core/Infrastructure/CompilationFactory.cs` with:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProjGraph.Lib.Core.Abstractions;
using System.Reflection;

namespace ProjGraph.Lib.Core.Infrastructure;

/// <summary>
/// Provides a factory for creating Roslyn <see cref="Compilation"/> objects.
/// </summary>
public sealed class CompilationFactory : ICompilationFactory
{
    /// <summary>
    /// The manifest resource name prefix under which the reference assemblies are embedded.
    /// </summary>
    private const string ReferenceResourcePrefix = "refs/";

    /// <summary>
    /// The BCL/EF metadata reference set, built once and reused across compilations: it is
    /// immutable for the process lifetime and building it copies several embedded assemblies.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<MetadataReference>> CachedReferences =
        new(BuildMetadataReferences);

    /// <summary>
    /// Creates a new Roslyn <see cref="CSharpCompilation"/> object using the provided syntax trees and necessary metadata references.
    /// </summary>
    /// <param name="syntaxTrees">A collection of <see cref="SyntaxTree"/> objects to include in the compilation.</param>
    /// <returns>
    /// A <see cref="CSharpCompilation"/> object that represents the compiled code from the provided syntax trees.
    /// </returns>
    /// <remarks>
    /// This method generates a new C# compilation named "AdHoc" by adding the provided syntax trees and
    /// a set of metadata references built using the <see cref="BuildMetadataReferences"/> method.
    /// </remarks>
    public Compilation CreateCompilation(IEnumerable<SyntaxTree> syntaxTrees)
    {
        return CreateCSharpCompilation(syntaxTrees);
    }

    /// <summary>
    /// Creates a new Roslyn <see cref="CSharpCompilation"/> object.
    /// </summary>
    /// <param name="syntaxTrees">The syntax trees to compile.</param>
    private static CSharpCompilation CreateCSharpCompilation(IEnumerable<SyntaxTree> syntaxTrees)
    {
        var references = CachedReferences.Value;
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithNullableContextOptions(NullableContextOptions.Enable);

        return CSharpCompilation.Create("AdHoc")
            .WithOptions(options)
            .AddReferences(references)
            .AddSyntaxTrees(syntaxTrees);
    }

    /// <summary>
    /// Builds a list of metadata references required for Roslyn compilation.
    /// </summary>
    /// <returns>A list of <see cref="MetadataReference"/> objects representing the necessary references.</returns>
    /// <remarks>
    /// The BCL references come from the reference assemblies embedded in this library under the
    /// <c>refs/</c> resource prefix (<c>System.Runtime</c>, <c>System.Collections</c>,
    /// <c>System.ComponentModel.Annotations</c>, and <c>netstandard</c>), so the reference set is
    /// the same whether the host runs on the JIT runtime or as a Native AOT executable, where
    /// <see cref="Assembly.Location"/> is always empty. <c>Microsoft.EntityFrameworkCore</c> is
    /// added when it is loaded from a file.
    /// </remarks>
    private static List<MetadataReference> BuildMetadataReferences()
    {
        var assembly = typeof(CompilationFactory).Assembly;
        var references = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ReferenceResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => CreateEmbeddedReference(assembly, name))
            .ToList<MetadataReference>();

        TryAddEntityFrameworkCoreReference(references);

        return references;
    }

    /// <summary>
    /// Creates a metadata reference from an embedded reference assembly.
    /// </summary>
    /// <param name="assembly">The assembly that embeds the resource.</param>
    /// <param name="resourceName">The manifest resource name, which also becomes the reference's display name.</param>
    /// <returns>The metadata reference.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource cannot be opened.</exception>
    private static PortableExecutableReference CreateEmbeddedReference(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded reference assembly '{resourceName}' could not be opened.");
        return MetadataReference.CreateFromStream(stream, filePath: resourceName);
    }

    /// <summary>
    /// Attempts to add a metadata reference for the Microsoft.EntityFrameworkCore assembly to the provided list of references.
    /// </summary>
    /// <param name="references">The list of metadata references to which the Entity Framework Core reference will be added.</param>
    /// <remarks>
    /// The assembly is skipped when its <see cref="Assembly.Location"/> is empty, which is always
    /// the case under Native AOT, instead of passing an empty path to
    /// <see cref="MetadataReference.CreateFromFile(string, MetadataReferenceProperties, DocumentationProvider?)"/>.
    /// </remarks>
    private static void TryAddEntityFrameworkCoreReference(List<MetadataReference> references)
    {
        try
        {
            var efCoreAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Microsoft.EntityFrameworkCore");

            if (efCoreAssembly is { Location.Length: > 0 })
            {
                references.Add(MetadataReference.CreateFromFile(efCoreAssembly.Location));
            }
        }
        catch (FileNotFoundException)
        {
            // Not critical if not found
        }
    }
}
```

- [ ] **Step 5: Run the factory tests**

Run: `dotnet test tests/ProjGraph.Tests.Unit.Core --filter "FullyQualifiedName~CompilationFactory"`
Expected: PASS, 13 tests.

- [ ] **Step 6: Check that the build guard fires**

Run: `dotnet build src/ProjGraph.Lib.Core -p:ReferenceAssemblyDirectory=/nonexistent/ref/`
Expected: FAIL with the single error `Reference assembly 'System.Runtime.dll' was not found in '/nonexistent/ref/'. Install the .NET <version> targeting pack (it ships with the SDK).` The `Error` task stops at the first missing file. Then run `dotnet build src/ProjGraph.Lib.Core` again and expect success, because the next build re-evaluates without the override.

- [ ] **Step 7: Run the Roslyn-based suites**

Run: `dotnet test tests/ProjGraph.Tests.Unit.ClassDiagram && dotnet test tests/ProjGraph.Tests.Unit.EntityFramework && dotnet test tests/ProjGraph.Tests.Integration.Cli`
Expected: PASS (122, 434, and 82 tests).

- [ ] **Step 8: Commit**

```bash
git add tests/ProjGraph.Tests.Unit.Core/CompilationFactoryCoverageTests.cs \
        src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj src/ProjGraph.Lib.Core/Infrastructure/CompilationFactory.cs
git commit -m "feat(core): build Roslyn references from embedded reference assemblies"
```

---

### Task 8: MCP server on source-generated JSON

**Files:**
- Modify: `src/ProjGraph.Mcp/ProjGraph.Mcp.csproj`
- Create: `src/ProjGraph.Mcp/McpJsonContext.cs`
- Modify: `src/ProjGraph.Mcp/Program.cs`
- Modify: `src/ProjGraph.Mcp/ProjGraphTools.cs`

**Interfaces:**
- Consumes: `McpJsonUtilities.DefaultOptions` (namespace `ModelContextProtocol`); `IMcpServerBuilder.WithTools<T>(JsonSerializerOptions?)` and `WithPrompts<T>(JsonSerializerOptions?)`; `AnalysisOptions` (`ProjGraph.Lib.ClassDiagram.Application`); `SolutionStats` (`ProjGraph.Core.Models`); `ChatMessage` (`Microsoft.Extensions.AI`).
- Produces: `internal sealed partial class McpJsonContext : JsonSerializerContext`, with `McpJsonContext.Default.SolutionStats` (`JsonTypeInfo<SolutionStats>`) and `McpJsonContext.Default.Options`. PR 2 depends on this context.

- [ ] **Step 1: Make the JIT build serialize like Native AOT**

In `src/ProjGraph.Mcp/ProjGraph.Mcp.csproj`, add this inside the first `PropertyGroup`, just before the `<!-- Set recommended package metadata -->` comment:

```xml
    <!-- Serialize only through source-generated metadata, as Native AOT does, so a type missing from
         McpJsonContext fails on the JIT build too instead of silently falling back to reflection. -->
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>

```

- [ ] **Step 2: Run the real-exe tests to verify they fail**

Run: `dotnet test tests/ProjGraph.Tests.Integration.Mcp --filter "FullyQualifiedName~McpSurfaceSnapshotTests|FullyQualifiedName~McpTransportTests"`
Expected: FAIL, 3 tests. The server exits during startup (`ClientTransportClosedException: MCP server process exited unexpectedly`), and the stderr tail reports missing `JsonTypeInfo` metadata. The in-process MCP tests are unaffected, because they use the test host's runtime config.

- [ ] **Step 3: Add the context**

Create `src/ProjGraph.Mcp/McpJsonContext.cs`. Keep it free of static members: a static initializer here can run before the generated `Default` is assigned and throw `TypeInitializationException`, which happened while this plan was being validated.

```csharp
using Microsoft.Extensions.AI;
using ProjGraph.Core.Models;
using ProjGraph.Lib.ClassDiagram.Application;
using System.Text.Json.Serialization;

namespace ProjGraph.Mcp;

/// <summary>
/// Source-generated JSON metadata for the types the MCP server serializes itself or exposes through
/// tool parameters and prompt results. Native AOT has no reflection-based serializer, so every such
/// type must be listed here.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AnalysisOptions))]
[JsonSerializable(typeof(SolutionStats))]
[JsonSerializable(typeof(IEnumerable<ChatMessage>))]
internal sealed partial class McpJsonContext : JsonSerializerContext;
```

- [ ] **Step 4: Register it in `Program`**

In `src/ProjGraph.Mcp/Program.cs`:

1. Add `using ModelContextProtocol;` above `using ModelContextProtocol.Protocol;`, and `using System.Text.Json;` below `using System.Reflection;`.
2. Insert this directly above `builder.Services.AddMcpServer(options =>`:

```csharp
        // Tool and prompt registration uses the SDK's default options with the server's own
        // source-generated metadata consulted first, so it needs no reflection-based serializer.
        var jsonOptions = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        jsonOptions.TypeInfoResolverChain.Insert(0, McpJsonContext.Default);

```

3. Replace:

```csharp
            .WithTools<ProjGraphTools>()
            .WithPrompts<ProjGraphPrompts>()
```

with:

```csharp
            .WithTools<ProjGraphTools>(jsonOptions)
            .WithPrompts<ProjGraphPrompts>(jsonOptions)
```

- [ ] **Step 5: Serialize stats through the context**

In `src/ProjGraph.Mcp/ProjGraphTools.cs`:

1. Delete the reflection-based options field and the blank line after it:

```csharp
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

```

2. In `SerializeStatsWithWarnings`, replace:

```csharp
        var node = JsonSerializer.SerializeToNode(stats, JsonSerializerOptions)?.AsObject();
        if (node is null)
        {
            return JsonSerializer.Serialize(stats, JsonSerializerOptions);
        }
```

with:

```csharp
        var node = JsonSerializer.SerializeToNode(stats, McpJsonContext.Default.SolutionStats)?.AsObject();
        if (node is null)
        {
            return JsonSerializer.Serialize(stats, McpJsonContext.Default.SolutionStats);
        }
```

3. Replace `return node.ToJsonString(JsonSerializerOptions);` with `return node.ToJsonString(McpJsonContext.Default.Options);`.

Keep `using System.Text.Json;`, because `JsonSerializer` is still used.

- [ ] **Step 6: Build**

Run: `dotnet build src/ProjGraph.Mcp`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 7: Run the real-exe tests to verify they pass, with the snapshot unchanged**

Run: `dotnet test tests/ProjGraph.Tests.Integration.Mcp --filter "FullyQualifiedName~McpSurfaceSnapshotTests|FullyQualifiedName~McpTransportTests"`
Expected: PASS, 3 tests. `git status` must show `Snapshots/mcp-surface.json` unmodified. If the snapshot test fails, the schema changed: stop and report the diff. Don't regenerate the snapshot.

- [ ] **Step 8: Run the MCP and contract suites**

Run: `dotnet test tests/ProjGraph.Tests.Integration.Mcp && dotnet test tests/ProjGraph.Tests.Contract`
Expected: PASS (149 and 67 tests). `McpWarningsTests` and `McpProjectGraphTests` cover the stats JSON shape.

- [ ] **Step 9: Commit**

```bash
git add src/ProjGraph.Mcp/ProjGraph.Mcp.csproj src/ProjGraph.Mcp/McpJsonContext.cs \
        src/ProjGraph.Mcp/Program.cs src/ProjGraph.Mcp/ProjGraphTools.cs
git commit -m "feat(mcp): register tools and prompts with source-generated JSON metadata"
```

---

### Task 9: AOT analyzers on first-party libraries

**Files:**
- Modify: `src/ProjGraph.Core/ProjGraph.Core.csproj`
- Modify: `src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj`
- Modify: `src/ProjGraph.Lib.Dependencies/ProjGraph.Lib.Dependencies.csproj`
- Modify: `src/ProjGraph.Lib.ClassDiagram/ProjGraph.Lib.ClassDiagram.csproj`
- Modify: `src/ProjGraph.Lib.EntityFramework/ProjGraph.Lib.EntityFramework.csproj`
- Modify: `src/ProjGraph.Lib/ProjGraph.Lib.csproj`
- Modify: `src/ProjGraph.Lib.Core/Infrastructure/CompilationFactory.cs`

**Interfaces:**
- Consumes: Task 7's `TryAddEntityFrameworkCoreReference`, whose `Location.Length` guard is the runtime check the suppression cites.
- Produces: libraries that stay analyzer-clean for trimming, AOT, and single-file under warnings-as-errors.

- [ ] **Step 1: Turn on the analyzers**

In each of the six csproj files above, insert this block directly after the first `</PropertyGroup>` (the package-metadata group):

```xml

  <!-- Native AOT: turns on the trim, AOT, and single-file analyzers for this library -->
  <PropertyGroup>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>
```

- [ ] **Step 2: Build to see the expected failure**

Run: `dotnet build ProjGraph.slnx`
Expected: FAIL with exactly two `IL3000` errors ("'System.Reflection.Assembly.Location.get' always returns an empty string for assemblies embedded in a single-file app"). Both are in `CompilationFactory.cs`, inside `TryAddEntityFrameworkCoreReference`. A spike of today's code found no other IL2xxx/IL3xxx warnings in first-party libraries. If a different one appears, fix it in code, and suppress only with a justification that names the runtime guard.

- [ ] **Step 3: Suppress with the runtime guard as justification**

In `src/ProjGraph.Lib.Core/Infrastructure/CompilationFactory.cs`:
1. Add `using System.Diagnostics.CodeAnalysis;` above `using System.Reflection;`.
2. Add this attribute directly above `private static void TryAddEntityFrameworkCoreReference(List<MetadataReference> references)`:

```csharp
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file",
        Justification = "Location is only used when it is non-empty; the Location.Length guard skips assemblies " +
                        "without a file (single-file and Native AOT) instead of passing an empty path to CreateFromFile.")]
```

- [ ] **Step 4: Build**

Run: `dotnet build ProjGraph.slnx`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/ProjGraph.Core/ProjGraph.Core.csproj src/ProjGraph.Lib.Core/ProjGraph.Lib.Core.csproj \
        src/ProjGraph.Lib.Dependencies/ProjGraph.Lib.Dependencies.csproj \
        src/ProjGraph.Lib.ClassDiagram/ProjGraph.Lib.ClassDiagram.csproj \
        src/ProjGraph.Lib.EntityFramework/ProjGraph.Lib.EntityFramework.csproj \
        src/ProjGraph.Lib/ProjGraph.Lib.csproj src/ProjGraph.Lib.Core/Infrastructure/CompilationFactory.cs
git commit -m "build: enable AOT compatibility analyzers on Core and all Lib projects"
```

---

### Task 10: Full verification and sample parity

No production changes. This task produces the evidence for the exit criteria and the PR description.

**Files:** none in the repo. The parity script lives in `/tmp`.

**Interfaces:**
- Consumes: the branch after Task 9, and `develop` as the baseline.
- Produces: exit-criteria evidence plus text for the PR description.

- [ ] **Step 1: Clean Release build, format check, and the full test run (the CI commands)**

Run:

```bash
dotnet restore ProjGraph.slnx
dotnet build ProjGraph.slnx --no-restore --configuration Release
dotnet format ProjGraph.slnx --no-restore --verify-no-changes
dotnet test ProjGraph.slnx --no-build --configuration Release
```

Expected: 0 warnings and 0 errors, format exits 0, and every test project PASSES (8 test assemblies, about 1,158 tests).

- [ ] **Step 2: Confirm no Microsoft.Build remains anywhere in the graph**

Run: `dotnet list ProjGraph.slnx package --include-transitive | grep -iE "Microsoft\.Build( |$)|Workspaces\.MSBuild"`
Expected: no output.

- [ ] **Step 3: Build a baseline CLI from `develop`**

```bash
git worktree add /tmp/projgraph-baseline develop
dotnet build /tmp/projgraph-baseline/src/ProjGraph.Cli -c Release
```

Expected: Build succeeded. Step 1 already built the branch's Release CLI.

- [ ] **Step 4: Write the parity script**

Create `/tmp/sample-parity.sh`:

```bash
#!/usr/bin/env bash
# Usage: sample-parity.sh <baseline-repo-root> <candidate-repo-root>
# Runs every regenerate-samples.sh invocation (plus the whole-src class diagram and a .sln
# visualize) with both Release CLI builds from the candidate root and diffs the outputs.
set -u
BASE="$1"; CAND="$2"; OUT="$(mktemp -d)"; FAIL=0
cd "$CAND"
run() { # name, args...
  local name="$1"; shift
  for side in base cand; do
    local root; [ "$side" = base ] && root="$BASE" || root="$CAND"
    dotnet "$root/src/ProjGraph.Cli/bin/Release/net10.0/ProjGraph.Cli.dll" "$@" > "$OUT/$name.$side.txt" 2>&1
    echo "exit=$?" >> "$OUT/$name.$side.txt"
  done
  # The stats table prints a wall-clock "Analysis time" row; drop it and collapse padding.
  if [ "$name" = stats ]; then
    for side in base cand; do grep -v "Analysis time" "$OUT/$name.$side.txt" | tr -s ' ' > "$OUT/$name.$side.norm"; mv "$OUT/$name.$side.norm" "$OUT/$name.$side.txt"; done
  fi
  if cmp -s "$OUT/$name.base.txt" "$OUT/$name.cand.txt"; then echo "IDENTICAL  $name"; else echo "DIFFERENT  $name"; FAIL=1; diff "$OUT/$name.base.txt" "$OUT/$name.cand.txt" | head -20; fi
}
run erd-complex        erd ./samples/erd/complex-ecommerce/Data/MyDbContext.cs
run erd-simple         erd ./samples/erd/simple-context/EntityFramework/MyDbContext.cs
run class-patterns     classdiagram ./samples/classdiagram/design-patterns/Domain/Order.cs --inheritance --dependencies --depth 2 --properties true --functions true
run class-complex      classdiagram ./samples/classdiagram/complex-hierarchy/Domain/Models/CEO.cs --inheritance --dependencies --depth 5 --properties true --functions true
run class-simple       classdiagram ./samples/classdiagram/simple-hierarchy/Models/Admin.cs --inheritance --dependencies --depth 2 --properties true --functions true
run class-src          classdiagram ./src --inheritance --dependencies
run visualize-modular  visualize ./samples/visualize/modular-architecture/ModularArchitecture.slnx --format mermaid
run visualize-simple   visualize ./samples/visualize/simple-dependencies/simple-dependencies.slnx --format mermaid
run visualize-repo     visualize ./ProjGraph.slnx --format tree
# A legacy .sln over the modular-architecture projects, since every committed sample is .slnx.
dotnet new sln --format sln -n Legacy -o "$OUT/legacy" > /dev/null
dotnet sln "$OUT/legacy/Legacy.sln" add $(find ./samples/visualize/modular-architecture -name '*.csproj' | sort) > /dev/null
run visualize-sln      visualize "$OUT/legacy/Legacy.sln" --format mermaid
run stats              stats ./samples/visualize/modular-architecture/ModularArchitecture.slnx
echo "outputs in $OUT"; exit $FAIL
```

Then run `chmod +x /tmp/sample-parity.sh`.

- [ ] **Step 5: Run parity**

Run: `/tmp/sample-parity.sh /tmp/projgraph-baseline "$(git rev-parse --show-toplevel)"`
Expected: 11 lines, all `IDENTICAL`, exit code 0. The invocations are `erd` ×2, `classdiagram` ×3, `classdiagram ./src --inheritance --dependencies`, `visualize` on both `.slnx` samples, `visualize ./ProjGraph.slnx --format tree`, `visualize` on a generated legacy `.sln`, and `stats`. If any line says `DIFFERENT`, stop and report the diff it prints.

- [ ] **Step 6: Clean up the baseline**

Run: `git worktree remove /tmp/projgraph-baseline`

- [ ] **Step 7: Draft the PR description sections**

Keep this text for the PR (don't commit it):
- **Exit criteria (spec, PR 1):**
  - Suites green, including the characterization tests (Tasks 1–2).
  - No Microsoft.Build or Workspaces.MSBuild references (Step 2).
  - Embedded reference set in use (Task 7 tests).
  - `tools/list` schemas unchanged (Task 3 snapshot, which passes untouched after Task 8).
  - Sample parity: paste Step 5's 11 `IDENTICAL` lines.
- **Deviations from the spec:** copy the six items from the top of this plan.
- **Known divergences from Microsoft.Build:** copy the list from the top of this plan.
- **Characterization quirks recorded but not fixed** (spec Non-goals):
  - Properties inside `<Target>` count, and the first in document order wins even over a later top-level value.
  - `Condition` is ignored.
  - Property values aren't trimmed.
  - `Update`/`Remove` package items appear with an empty name.
  - Lowercase item types and lowercase `Version` metadata are ignored.
- **Follow-up for PR 2's plan:** the spec's MCP smoke case says `options.inheritance = true`. The real parameter is `options.includeInheritance` (see `Snapshots/mcp-surface.json`).
