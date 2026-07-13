using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Lib.EntityFramework.Rendering;
using ProjGraph.Tests.Shared.Helpers;

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
        [@"erd\complex-ecommerce\Data\MyDbContext.cs", "MyDbContext", "complex-ecommerce"]
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Erd_MatchesGolden(string sampleRelativePath, string? contextName, string goldenName)
    {
        var actual = EfGoldenRunner.RenderContext(TestPathHelper.GetSamplePath(sampleRelativePath), contextName);
        EfGoldenRunner.Verify(goldenName, actual);
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

    public static string RenderContext(string samplePath, string? contextName)
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        var service = new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));

#pragma warning disable VSTHRD002 // Deliberate sync-over-async bridge: harness API is pinned to a synchronous
        // signature (see task brief); no SynchronizationContext deadlock risk under xUnit.
        var model = service.AnalyzeContextAsync(samplePath, contextName).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        var rendered = new MermaidErdRenderer().Render(model, new DiagramOptions(true, false));
        return Normalize(rendered);
    }

    public static void Verify(string goldenName, string actual)
    {
        var goldenPath = Path.Combine(GoldenDirectory(), $"{goldenName}.mmd");

        if (UpdateMode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, actual);
            return;
        }

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
        // Golden files are copied next to the test assembly (see csproj content include).
        return Path.Combine(AppContext.BaseDirectory, "Golden", "goldens");
    }
}
