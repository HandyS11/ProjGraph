using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Edge-case coverage for <see cref="EntityFileDiscovery"/> and the file-discovery side of
/// <see cref="EfModelAnalyzer"/>: search boundaries (filesystem root, build-output directories, recursion
/// depth), <c>DbSet&lt;T&gt;</c> type arguments that are not entity names, and owned navigations whose CLR
/// type is only reachable through a collection element type.
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class EfDiscoveryCoverageTests : IDisposable
{
    private readonly EntityFileDiscovery _sut = new(new PhysicalFileSystem());
    private readonly string _tempDir;

    public EfDiscoveryCoverageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "efdcov_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private static ClassDeclarationSyntax FirstClass(string code)
    {
        return CSharpSyntaxTree.ParseText(code).GetRoot()
            .DescendantNodes().OfType<ClassDeclarationSyntax>().First();
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

    [Fact]
    public void BuildSearchDirectories_AtFilesystemRoot_ShouldReturnOnlyThatDirectory()
    {
        // The root has no parent; the search scope must degrade to the root itself rather than
        // dereferencing a null parent.
        var root = Path.GetPathRoot(Path.GetFullPath(_tempDir))!;

        var result = _sut.BuildSearchDirectories(root);

        result.Should().ContainSingle().Which.Should().Be(root);
    }

    [Fact]
    public void SearchForBaseClassFiles_NoBaseClassNames_ShouldReturnEmptyWithoutSearching()
    {
        // Nothing to look for means the recursion must not start at all — an entity hierarchy with no
        // base types must not trigger a full tree walk.
        File.WriteAllText(Path.Combine(_tempDir, "BaseEntity.cs"), "public class BaseEntity { }");

        var result = _sut.SearchForBaseClassFiles([], new DirectoryInfo(_tempDir));

        result.Should().BeEmpty();
    }

    [Fact]
    public void SearchForBaseClassFiles_FileUnderBuildOutput_ShouldBeSkipped()
    {
        // A copy of the source under bin/ is a build artifact; matching it would put a stale duplicate
        // declaration into the compilation.
        var binDir = Path.Combine(_tempDir, "bin");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "BaseEntity.cs"), "public class BaseEntity { }");

        var result = _sut.SearchForBaseClassFiles(["BaseEntity"], new DirectoryInfo(_tempDir));

        result.Should().BeEmpty();
    }

    [Fact]
    public void SearchForBaseClassFiles_BeyondMaxSearchDepth_ShouldNotDescend()
    {
        // The recursion is depth-limited to keep the scan bounded on deep repositories; a base class
        // buried deeper than the limit is simply not found.
        var deepDir = _tempDir;
        for (var i = 0; i < 12; i++)
        {
            deepDir = Path.Combine(deepDir, $"d{i}");
        }

        Directory.CreateDirectory(deepDir);
        File.WriteAllText(Path.Combine(deepDir, "BaseEntity.cs"), "public class BaseEntity { }");

        var result = _sut.SearchForBaseClassFiles(["BaseEntity"], new DirectoryInfo(_tempDir));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverEntityFilesAsync_EntityUnderBuildOutput_ShouldBeSkipped()
    {
        var objDir = Path.Combine(_tempDir, "obj");
        Directory.CreateDirectory(objDir);
        await File.WriteAllTextAsync(Path.Combine(objDir, "Customer.cs"),
            "public class Customer { public int Id { get; set; } }");

        var contextFilePath = Path.Combine(_tempDir, "MyContext.cs");
        await File.WriteAllTextAsync(contextFilePath, "// context file");

        var result = await _sut.DiscoverEntityFilesAsync([_tempDir], ["Customer"], contextFilePath);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverConfigurationFilesAsync_ConfigUnderBuildOutput_ShouldBeSkipped()
    {
        const string configCode = """
                                  using Microsoft.EntityFrameworkCore;
                                  using Microsoft.EntityFrameworkCore.Metadata.Builders;
                                  public class GadgetConfiguration : IEntityTypeConfiguration<Gadget>
                                  {
                                      public void Configure(EntityTypeBuilder<Gadget> builder) { }
                                  }
                                  """;
        var binDir = Path.Combine(_tempDir, "bin");
        Directory.CreateDirectory(binDir);
        await File.WriteAllTextAsync(Path.Combine(binDir, "GadgetConfiguration.cs"), configCode);

        var contextFilePath = Path.Combine(_tempDir, "MyContext.cs");
        await File.WriteAllTextAsync(contextFilePath, "public class MyContext { }");

        var result = await _sut.DiscoverConfigurationFilesAsync([_tempDir], contextFilePath);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverConfigurationFilesAsync_InterfaceDerivingFromTheConfigurationInterface_ShouldBeIgnored()
    {
        // An interface that extends IEntityTypeConfiguration of T declares no Configure body to walk,
        // registering it as a config class would hand the walker a member-less type.
        const string code = """
                            using Microsoft.EntityFrameworkCore;
                            public interface IGadgetConfiguration : IEntityTypeConfiguration<Gadget> { }
                            """;
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "IGadgetConfiguration.cs"), code);

        var contextFilePath = Path.Combine(_tempDir, "MyContext.cs");
        await File.WriteAllTextAsync(contextFilePath, "public class MyContext { }");

        var result = await _sut.DiscoverConfigurationFilesAsync([_tempDir], contextFilePath);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractEntityTypeNames_GenericPropertyThatIsNotADbSet_ShouldBeIgnored()
    {
        const string code = """
                            using System.Collections.Generic;
                            using Microsoft.EntityFrameworkCore;
                            public class MyContext : DbContext
                            {
                                public DbSet<Blog> Blogs { get; set; }
                                public List<Blog> Cache { get; set; }
                                public Lazy<Blog> Deferred { get; set; }
                            }
                            """;

        var result = _sut.ExtractEntityTypeNames(FirstClass(code));

        result.Should().BeEquivalentTo("Blog");
    }

    [Fact]
    public void ExtractEntityTypeNames_PredefinedTypeArgument_ShouldFallBackToItsKeyword()
    {
        // A DbSet over a keyword type is not a real entity, but the extraction must still produce a
        // stable name rather than throwing on the unexpected type-syntax kind.
        const string code = """
                            using Microsoft.EntityFrameworkCore;
                            public class MyContext : DbContext
                            {
                                public DbSet<int> Counters { get; set; }
                            }
                            """;

        var result = _sut.ExtractEntityTypeNames(FirstClass(code));

        result.Should().BeEquivalentTo("int");
    }

    [Fact]
    public async Task AnalyzeContextAsync_OwnsManyOverAnArrayNavigationInASeparateFile_ShouldCaptureColumns()
    {
        // OwnsMany's CLR type hides behind the navigation's element type. For an array-typed navigation
        // that element type is an ArrayTypeSyntax element, not a generic argument — if it is not unwrapped
        // the owned type's file is never found and the owned entity materializes with zero columns.
        using var temp = new TestDirectory();

        const string contextContent = """
            using Microsoft.EntityFrameworkCore;
            namespace Test;

            public class ArrayOwnedContext : DbContext
            {
                public DbSet<Customer> Customers { get; set; } = null!;

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Customer>().OwnsMany(c => c.Addresses);
                }
            }

            public class Customer
            {
                public int Id { get; set; }
                public Address[] Addresses { get; set; } = null!;
            }
            """;
        const string addressContent = """
            namespace Test;

            public class Address
            {
                public string Street { get; set; } = "";
                public string City { get; set; } = "";
            }
            """;

        var contextPath = temp.CreateFile("Context.cs", contextContent);
        temp.CreateFile("Address.cs", addressContent);

        var model = await CreateService().AnalyzeContextAsync(contextPath, "ArrayOwnedContext");

        var owned = model.Entities.SingleOrDefault(e => e.Key == "Customer.Addresses");
        owned.Should().NotBeNull("OwnsMany over an array navigation must still capture the owned type");
        owned!.IsCollection.Should().BeTrue();
        owned.Properties.Select(p => p.Name).Should().Contain("Street").And.Contain("City");
    }

}
