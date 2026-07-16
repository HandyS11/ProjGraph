using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Lib.EntityFramework.Rendering;
using ProjGraph.Tests.Shared.Helpers;
using System.Runtime.CompilerServices;

namespace ProjGraph.Tests.Unit.EntityFramework.Golden;

/// <summary>
/// Characterization tests: every EF sample context's rendered ERD is pinned to a committed
/// golden file. Set the environment variable UPDATE_EF_GOLDENS=1 to (re)generate the goldens
/// instead of asserting against them.
/// </summary>
[Trait("Category", "Golden")]
public sealed class EfErdGoldenTests
{
    public static IEnumerable<object?[]> Cases =>
    [
        [@"erd\simple-context\EntityFramework\MyDbContext.cs", "MyDbContext", "simple-context"],
        [@"erd\complex-ecommerce\Data\MyDbContext.cs", "MyDbContext", "complex-ecommerce"],
        [FixturePath("RelationshipsContext.cs"), "RelationshipsContext", "fixture-relationships"],
        [FixturePath("OwnedAndJoinContext.cs"), "OwnedAndJoinContext", "fixture-owned-join"],
        [FixturePath("PropertyConfigContext.cs"), "PropertyConfigContext", "fixture-property-config"],
        [FixturePath("ConfigClassContext.cs"), "ConfigClassContext", "fixture-config-class"],
        [FixturePath("SeparateConfigContext.cs"), "SeparateConfigContext", "fixture-separate-config"],
        [FixturePath("BaseContext.cs"), "BaseContext", "fixture-base-dbset"],
        [FixturePath("AuditContext.cs"), "AuditContext", "fixture-base-dbset-multifile"],
        [FixturePath("ChainedOwnedContext.cs"), "ChainedOwnedContext", "fixture-chained-owned"],
        [FixturePath("VendorConfigContext.cs"), "VendorConfigContext", "fixture-config-class-owned"]
    ];

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Golden", "fixtures", fileName);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Erd_MatchesGolden(string contextPath, string? contextName, string goldenName)
    {
        var absolute = Path.IsPathFullyQualified(contextPath)
            ? contextPath
            : TestPathHelper.GetSamplePath(contextPath);
        var actual = EfGoldenRunner.RenderContext(absolute, contextName);
        EfGoldenRunner.Verify(goldenName, actual);
    }

    [Fact]
    public void SnapshotErd_MatchesGolden()
    {
        var actual = EfGoldenRunner.RenderSnapshot(
            FixturePath("JournalSnapshot.cs"), "JournalContextModelSnapshot");
        EfGoldenRunner.Verify("fixture-snapshot", actual);
    }

    [Fact]
    public void OneToOneSnapshotErd_MatchesGolden()
    {
        var actual = EfGoldenRunner.RenderSnapshot(
            FixturePath("OneToOneSnapshot.cs"), "LedgerContextModelSnapshot");
        EfGoldenRunner.Verify("fixture-onetoone-snapshot", actual);
    }

    [Fact]
    public void OwnedSnapshotErd_MatchesGolden()
    {
        var actual = EfGoldenRunner.RenderSnapshot(
            FixturePath("OwnedSnapshot.cs"), "BillingContextModelSnapshot");
        EfGoldenRunner.Verify("fixture-owned-snapshot", actual);
    }

    [Fact]
    public void OwnedModesErd_MirrorEf_MatchesGolden()
    {
        var actual = EfGoldenRunner.RenderContext(
            FixturePath("OwnedModesContext.cs"), "OwnedModesContext", ErdOwnedMode.MirrorEf);
        EfGoldenRunner.Verify("fixture-owned-modes-mirror", actual);
    }

    [Fact]
    public void OwnedModesErd_Classic_MatchesGolden()
    {
        var actual = EfGoldenRunner.RenderContext(
            FixturePath("OwnedModesContext.cs"), "OwnedModesContext", ErdOwnedMode.Classic);
        EfGoldenRunner.Verify("fixture-owned-modes-classic", actual);
    }

    [Fact]
    public void ContextAndSnapshotPaths_RenderTheSameErd_ForTheSameModel()
    {
        var fromContext = EfGoldenRunner.RenderContext(
            FixturePath("CrossPathContext.cs"), "CrossPathContext");
        var fromSnapshot = EfGoldenRunner.RenderSnapshot(
            FixturePath("CrossPathSnapshot.cs"), "CrossPathContextModelSnapshot");

        // Titles differ by construction (context name vs snapshot-derived name); compare the diagram body.
        static string Body(string mmd) => string.Join('\n',
            mmd.Split('\n').SkipWhile(l => !l.StartsWith("erDiagram", StringComparison.Ordinal)));

        Body(fromSnapshot).Should().Be(Body(fromContext),
            "the DbContext path infers table-splitting from the absence of ToTable while the snapshot " +
            "path reads an explicit one; both must resolve to the same recorded fact and render identically");
    }
}

/// <summary>
/// Shared machinery for the EF golden tests: renders a context's ERD and compares (or regenerates)
/// the committed golden file.
/// </summary>
internal static class EfGoldenRunner
{
    private static readonly bool UpdateMode =
        Environment.GetEnvironmentVariable("UPDATE_EF_GOLDENS") == "1";

    public static string RenderContext(string samplePath, string? contextName,
        ErdOwnedMode mode = ErdOwnedMode.MirrorEf)
    {
        var service = CreateService();

#pragma warning disable VSTHRD002 // Deliberate sync-over-async bridge: harness API is pinned to a synchronous
        // signature (see task brief); no SynchronizationContext deadlock risk under xUnit.
        var model = service.AnalyzeContextAsync(samplePath, contextName).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        return Render(model, mode);
    }

    public static string RenderSnapshot(string snapshotPath, string? snapshotName,
        ErdOwnedMode mode = ErdOwnedMode.MirrorEf)
    {
        var service = CreateService();

#pragma warning disable VSTHRD002 // Same deliberate sync-over-async bridge as RenderContext.
        var model = service.AnalyzeSnapshotAsync(snapshotPath, snapshotName).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        return Render(model, mode);
    }

    private static EfAnalysisService CreateService()
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        return new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));
    }

    private static string Render(EfModel model, ErdOwnedMode mode = ErdOwnedMode.MirrorEf)
        => Normalize(new MermaidErdRenderer().Render(model, new DiagramOptions(true, false, false, mode)));

    public static void Verify(string goldenName, string actual)
    {
        if (UpdateMode)
        {
            // Write straight into the committed source tree so the regenerated baseline shows up in
            // `git diff` and can be reviewed/committed — no manual copy-back from the bin output.
            var sourcePath = Path.Combine(SourceGoldenDirectory(), $"{goldenName}.mmd");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, actual);
            return;
        }

        var goldenPath = Path.Combine(GoldenDirectory(), $"{goldenName}.mmd");
        File.Exists(goldenPath).Should().BeTrue(
            $"golden '{goldenName}.mmd' must exist; run with UPDATE_EF_GOLDENS=1 to generate it");
        var expected = Normalize(File.ReadAllText(goldenPath));
        actual.Should().Be(expected,
            $"rendered ERD must match golden '{goldenName}.mmd'; if this change is intended, " +
            "regenerate with UPDATE_EF_GOLDENS=1 and review the diff");
    }

    private static string Normalize(string text) => text.ReplaceLineEndings("\n").TrimEnd('\n');

    private static string GoldenDirectory()
    {
        // Assert mode reads the goldens copied next to the test assembly (see csproj None copy metadata).
        return Path.Combine(AppContext.BaseDirectory, "Golden", "goldens");
    }

    private static string SourceGoldenDirectory([CallerFilePath] string callerFilePath = "")
    {
        // Resolved from this file's compile-time path (this file lives in Golden/), so update mode
        // writes to the committed tests/.../Golden/goldens directory rather than the bin output copy.
        return Path.Combine(Path.GetDirectoryName(callerFilePath)!, "goldens");
    }
}
