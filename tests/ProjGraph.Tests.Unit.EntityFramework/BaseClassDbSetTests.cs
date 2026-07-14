using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Slice 5: entity discovery must climb the DbContext's base-type chain so that
/// <c>DbSet&lt;T&gt;</c> properties declared on a base context are surfaced.
/// </summary>
[Trait("Category", "EntityFramework")]
public class BaseClassDbSetTests
{
    private readonly EfAnalysisService _service = CreateService();

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

    [Fact]
    public async Task AnalyzeContextAsync_DiscoversDbSetDeclaredOnBaseContext()
    {
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public abstract class BaseDbContext : DbContext
                               {
                                   public DbSet<Note> Notes { get; set; } = null!;
                               }
                               public class AppContext : BaseDbContext
                               {
                                   public DbSet<Tag> Tags { get; set; } = null!;
                               }
                               public class Note { public int Id { get; set; } public string Text { get; set; } = ""; }
                               public class Tag { public int Id { get; set; } public string Label { get; set; } = ""; }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await _service.AnalyzeContextAsync(filePath, "AppContext");

        string[] expected = ["Note", "Tag"];
        model.Entities.Select(e => e.Name).Should().BeEquivalentTo(expected);
        var note = model.Entities.First(e => e.Name == "Note");
        note.Properties.Should().Contain(p => p.Name == "Id" && p.IsPrimaryKey);
        note.Properties.Should().Contain(p => p.Name == "Text");
    }

    [Fact]
    public async Task AnalyzeContextAsync_DiscoversDbSetAcrossMultiLevelBaseChain()
    {
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public abstract class RootDbContext : DbContext
                               {
                                   public DbSet<Note> Notes { get; set; } = null!;
                               }
                               public abstract class MiddleDbContext : RootDbContext
                               {
                                   public DbSet<Label> Labels { get; set; } = null!;
                               }
                               public class AppContext : MiddleDbContext
                               {
                                   public DbSet<Tag> Tags { get; set; } = null!;
                               }
                               public class Note { public int Id { get; set; } }
                               public class Label { public int Id { get; set; } }
                               public class Tag { public int Id { get; set; } }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await _service.AnalyzeContextAsync(filePath, "AppContext");

        string[] expected = ["Note", "Label", "Tag"];
        model.Entities.Select(e => e.Name).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task AnalyzeContextAsync_DerivedDbSetWinsOverBaseRedeclaration()
    {
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public abstract class BaseDbContext : DbContext
                               {
                                   public virtual DbSet<Widget> Widgets { get; set; } = null!;
                               }
                               public class AppContext : BaseDbContext
                               {
                                   public override DbSet<Widget> Widgets { get; set; } = null!;
                               }
                               public class Widget { public int Id { get; set; } public string Name { get; set; } = ""; }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await _service.AnalyzeContextAsync(filePath, "AppContext");

        model.Entities.Should().ContainSingle(e => e.Name == "Widget");
    }

    [Fact]
    public async Task AnalyzeContextAsync_DiscoversBaseEntityDeclaredInSeparateFile()
    {
        using var temp = new TestDirectory();
        temp.CreateFile("BaseAppDbContext.cs", """
                                               using Microsoft.EntityFrameworkCore;
                                               namespace Test;
                                               public abstract class BaseAppDbContext : DbContext
                                               {
                                                   public DbSet<Note> Notes { get; set; } = null!;
                                               }
                                               """);
        temp.CreateFile("Note.cs", """
                                   namespace Test;
                                   public class Note { public int Id { get; set; } public string Text { get; set; } = ""; }
                                   """);
        var contextPath = temp.CreateFile("AppContext.cs", """
                                                            using Microsoft.EntityFrameworkCore;
                                                            namespace Test;
                                                            public class AppContext : BaseAppDbContext
                                                            {
                                                                public DbSet<Tag> Tags { get; set; } = null!;
                                                            }
                                                            public class Tag { public int Id { get; set; } }
                                                            """);

        var model = await _service.AnalyzeContextAsync(contextPath, "AppContext");

        string[] expected = ["Note", "Tag"];
        model.Entities.Select(e => e.Name).Should().BeEquivalentTo(expected);
        var note = model.Entities.First(e => e.Name == "Note");
        note.Properties.Should().Contain(p => p.Name == "Text");
    }

    [Fact]
    public async Task AnalyzeContextAsync_DiscoversDbSetsAcrossBaseChainInSeparateFiles()
    {
        using var temp = new TestDirectory();
        temp.CreateFile("RootDbContext.cs", """
                                            using Microsoft.EntityFrameworkCore;
                                            namespace Test;
                                            public abstract class RootDbContext : DbContext
                                            {
                                                public DbSet<Note> Notes { get; set; } = null!;
                                            }
                                            public class Note { public int Id { get; set; } public string Text { get; set; } = ""; }
                                            """);
        temp.CreateFile("MiddleDbContext.cs", """
                                              using Microsoft.EntityFrameworkCore;
                                              namespace Test;
                                              public abstract class MiddleDbContext : RootDbContext
                                              {
                                                  public DbSet<Label> Labels { get; set; } = null!;
                                              }
                                              public class Label { public int Id { get; set; } }
                                              """);
        var contextPath = temp.CreateFile("AppContext.cs", """
                                                            using Microsoft.EntityFrameworkCore;
                                                            namespace Test;
                                                            public class AppContext : MiddleDbContext
                                                            {
                                                                public DbSet<Tag> Tags { get; set; } = null!;
                                                            }
                                                            public class Tag { public int Id { get; set; } }
                                                            """);

        var model = await _service.AnalyzeContextAsync(contextPath, "AppContext");

        string[] expected = ["Note", "Label", "Tag"];
        model.Entities.Select(e => e.Name).Should().BeEquivalentTo(expected);
        var note = model.Entities.First(e => e.Name == "Note");
        note.Properties.Should().Contain(p => p.Name == "Text");
    }

    [Fact]
    public async Task AnalyzeContextAsync_UnreadableBaseContextFile_DegradesInsteadOfThrowing()
    {
        using var temp = new TestDirectory();
        var basePath = temp.CreateFile("BaseAppDbContext.cs", """
                                                              using Microsoft.EntityFrameworkCore;
                                                              namespace Test;
                                                              public abstract class BaseAppDbContext : DbContext
                                                              {
                                                                  public DbSet<Note> Notes { get; set; } = null!;
                                                              }
                                                              public class Note { public int Id { get; set; } }
                                                              """);
        var contextPath = temp.CreateFile("AppContext.cs", """
                                                            using Microsoft.EntityFrameworkCore;
                                                            namespace Test;
                                                            public class AppContext : BaseAppDbContext
                                                            {
                                                                public DbSet<Tag> Tags { get; set; } = null!;
                                                            }
                                                            public class Tag { public int Id { get; set; } }
                                                            """);

        var fs = new ThrowOnReadFileSystem(basePath);
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        var service = new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));

        var model = await service.AnalyzeContextAsync(contextPath, "AppContext");

        // The locked base file degrades to name-only knowledge; the entry context's own
        // entities must still come through instead of the whole analysis throwing.
        model.Entities.Should().Contain(e => e.Name == "Tag");
    }

    /// <summary>
    /// Delegates to the real file system but throws <see cref="IOException"/> when reading one
    /// specific file, simulating a locked or inaccessible base-context source file.
    /// </summary>
    /// <param name="throwOnPath">The file path whose reads should fail with <see cref="IOException"/>.</param>
    private sealed class ThrowOnReadFileSystem(string throwOnPath) : IFileSystem
    {
        private readonly PhysicalFileSystem _inner = new();

        public bool FileExists(string path) => _inner.FileExists(path);

        public string ReadAllText(string path) => ThrowIfBlocked(path);

        public string GetFullPath(string path) => _inner.GetFullPath(path);

        public string? GetDirectoryName(string path) => _inner.GetDirectoryName(path);

        public string GetExtension(string path) => _inner.GetExtension(path);

        public string Combine(params string[] paths) => _inner.Combine(paths);

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(ThrowIfBlocked(path));

        public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default) =>
            _inner.WriteAllTextAsync(path, contents, cancellationToken);

        public void CreateDirectory(string path) => _inner.CreateDirectory(path);

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public string[] GetDirectories(string path) => _inner.GetDirectories(path);

        public string[] GetFiles(string path, string searchPattern) => _inner.GetFiles(path, searchPattern);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions options) =>
            _inner.EnumerateFiles(path, searchPattern, options);

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions options) =>
            _inner.EnumerateDirectories(path, searchPattern, options);

        public string GetCurrentDirectory() => _inner.GetCurrentDirectory();

        private string ThrowIfBlocked(string path) =>
            string.Equals(Path.GetFullPath(path), Path.GetFullPath(throwOnPath), StringComparison.Ordinal)
                ? throw new IOException($"Simulated lock on '{path}'.")
                : _inner.ReadAllText(path);
    }

    [Fact]
    public async Task AnalyzeContextAsync_ContextWithoutUserBase_IsUnaffected()
    {
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppContext : DbContext
                               {
                                   public DbSet<Tag> Tags { get; set; } = null!;
                               }
                               public class Tag { public int Id { get; set; } }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await _service.AnalyzeContextAsync(filePath, "AppContext");

        model.Entities.Should().ContainSingle(e => e.Name == "Tag");
    }
}
