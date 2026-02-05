using FluentAssertions;
using ProjGraph.Lib.Application.Services;
using ProjGraph.Tests.Unit.Helpers;

namespace ProjGraph.Tests.Unit.Services.ClassAnalysis;

public sealed class ClassAnalysisDepthTests : IDisposable
{
    private readonly TestDirectory _temp = new();
    private readonly string _tempRoot;
    private readonly ClassAnalysisService _service = new();

    public ClassAnalysisDepthTests()
    {
        _tempRoot = _temp.DirectoryPath;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _temp.Dispose();
        }
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithDepthLimit_DoesNotExceedDepth()
    {
        // Root -> A -> B -> C
        var fileA = Path.Combine(_tempRoot, "A.cs");
        var fileB = Path.Combine(_tempRoot, "B.cs");
        var fileC = Path.Combine(_tempRoot, "C.cs");

        await File.WriteAllTextAsync(fileA, "public class A : B {}");
        await File.WriteAllTextAsync(fileB, "public class B : C {}");
        await File.WriteAllTextAsync(fileC, "public class C {}");

        // Depth 1: Should find A and B, but not C
        var result = await _service.AnalyzeFileAsync(fileA);

        result.Types.Should().Contain(t => t.Name == "A");
        result.Types.Should().Contain(t => t.Name == "B");
        result.Types.Should().NotContain(t => t.Name == "C");

        // C should be marked as external since it's at depth 2 (discovered from B but B is depth 1)
        // Wait, if B is at depth 1, its base C is at depth 2.
    }
}