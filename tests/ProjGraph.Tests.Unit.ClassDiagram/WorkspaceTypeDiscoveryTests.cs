using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.ClassDiagram;

/// <summary>
/// Tests for <see cref="WorkspaceTypeDiscovery"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkspaceTypeDiscoveryTests
{
    [Fact]
    public async Task FindTypeDefinitionFileAsync_TypeInMultipleDirectories_ShouldPickPathSortedFirst()
    {
        // The former optimistic "common directory" pass (Models/, Entities/, ...) returned its
        // hit directly, overriding the deterministic path-sort tie-break of the root scan.
        // Resolution must be deterministic regardless of which directory contains the type.
        using var dir = new TestDirectory();
        dir.CreateFile("Models/X.cs", "namespace M; public class X { }");
        dir.CreateFile("Api/X.cs", "namespace A; public class X { }");
        var sut = new WorkspaceTypeDiscovery(new PhysicalFileSystem());

        var found = await sut.FindTypeDefinitionFileAsync("X", dir.DirectoryPath);

        found.Should().Be(Path.Combine(dir.DirectoryPath, "Api", "X.cs"));
    }
}
