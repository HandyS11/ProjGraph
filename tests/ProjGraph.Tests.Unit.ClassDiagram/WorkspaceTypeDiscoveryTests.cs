using NSubstitute;
using ProjGraph.Lib.ClassDiagram.Infrastructure;
using ProjGraph.Lib.Core.Abstractions;
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

    [Fact]
    public async Task FindTypeDefinitionFileAsync_EnumerationThrowsMidScan_ShouldKeepPartialResults()
    {
        // A directory deleted mid-scan (or a symlink cycle) surfaces IOException from the lazy
        // enumeration itself, not from a file read. That must degrade to a partial scan, not
        // abort the whole analysis. The start directory must really exist because the workspace
        // root walk uses the physical file system.
        using var dir = new TestDirectory();
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EnumerationOptions>())
            .Returns(_ => OneFileThenThrow("/ws/A.cs"));
        fileSystem.EnumerateDirectories(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EnumerationOptions>())
            .Returns([]);
        fileSystem.ReadAllTextAsync("/ws/A.cs").Returns("namespace W; public class Target { }");
        var sut = new WorkspaceTypeDiscovery(fileSystem);

        var found = await sut.FindTypeDefinitionFileAsync("Target", dir.DirectoryPath);

        found.Should().Be("/ws/A.cs");
    }

    private static IEnumerable<string> OneFileThenThrow(string file)
    {
        yield return file;
        throw new IOException("directory removed during enumeration");
    }
}
