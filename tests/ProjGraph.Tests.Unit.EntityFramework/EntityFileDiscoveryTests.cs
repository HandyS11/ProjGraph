using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// Tests for <see cref="EntityFileDiscovery"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EntityFileDiscoveryTests : IDisposable
{
    private readonly EntityFileDiscovery _sut = new(new PhysicalFileSystem());
    private readonly string _tempDir;

    public EntityFileDiscoveryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "efd_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void ExtractEntityTypeNames_WithDbSetProperties_ShouldReturnEntityNames()
    {
        const string code = """
                            using Microsoft.EntityFrameworkCore;
                            public class MyContext : DbContext
                            {
                                public DbSet<Customer> Customers { get; set; }
                                public DbSet<Order> Orders { get; set; }
                                public string ConnectionString { get; set; }
                            }
                            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var contextClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        var result = _sut.ExtractEntityTypeNames(contextClass);

        result.Should().BeEquivalentTo("Customer", "Order");
    }

    [Fact]
    public void ExtractEntityTypeNames_NoDbSetProperties_ShouldReturnEmpty()
    {
        const string code = """
                            public class MyContext
                            {
                                public string Name { get; set; }
                            }
                            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var contextClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        var result = _sut.ExtractEntityTypeNames(contextClass);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractBaseClassNamesFromSyntax_WithBaseClass_ShouldExtractName()
    {
        const string code = """
                            public class BaseEntity { }
                            public class Customer : BaseEntity { }
                            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var baseClassNames = new HashSet<string>();

        _sut.ExtractBaseClassNamesFromSyntax(root, baseClassNames);

        baseClassNames.Should().Contain("BaseEntity");
    }

    [Fact]
    public void ExtractBaseClassNamesFromSyntax_WithInterface_ShouldSkipInterface()
    {
        const string code = """
                            public interface IEntity { }
                            public class Customer : IEntity { }
                            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var baseClassNames = new HashSet<string>();

        _sut.ExtractBaseClassNamesFromSyntax(root, baseClassNames);

        baseClassNames.Should().NotContain("IEntity");
    }

    [Fact]
    public void ExtractBaseClassNamesFromSyntax_WithDbContext_ShouldSkipDbContext()
    {
        const string code = """
                            public class MyContext : DbContext { }
                            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var baseClassNames = new HashSet<string>();

        _sut.ExtractBaseClassNamesFromSyntax(root, baseClassNames);

        baseClassNames.Should().NotContain("DbContext");
    }

    [Fact]
    public void ExtractBaseClassNamesFromSyntax_WithGenericBase_ShouldStripGenericArgs()
    {
        const string code = """
                            public class Customer : BaseEntity<int> { }
                            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var baseClassNames = new HashSet<string>();

        _sut.ExtractBaseClassNamesFromSyntax(root, baseClassNames);

        baseClassNames.Should().Contain("BaseEntity");
        baseClassNames.Should().NotContain("BaseEntity<int>");
    }

    [Fact]
    public void BuildSearchDirectories_ShouldIncludeContextDirectory()
    {
        var result = _sut.BuildSearchDirectories(_tempDir);

        result.Should().Contain(_tempDir);
    }

    [Fact]
    public void BuildSearchDirectories_InTempPath_ShouldNotAddParent()
    {
        // When under temp, it should only return the context directory itself
        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);

        var result = _sut.BuildSearchDirectories(subDir);

        result.Should().ContainSingle().Which.Should().Be(subDir);
    }

    [Fact]
    public void SearchForBaseClassFiles_WithMatchingFile_ShouldFindIt()
    {
        // Create a C# file named after the base class
        var filePath = Path.Combine(_tempDir, "BaseEntity.cs");
        File.WriteAllText(filePath, "public class BaseEntity { }");

        var baseClassNames = new HashSet<string>
        {
            "BaseEntity"
        };
        var searchDir = new DirectoryInfo(_tempDir);

        var result = _sut.SearchForBaseClassFiles(baseClassNames, searchDir);

        result.Should().ContainKey("BaseEntity");
        result["BaseEntity"].Should().Contain("BaseEntity.cs");
    }

    [Fact]
    public void SearchForBaseClassFiles_NoMatchingFile_ShouldReturnEmpty()
    {
        var baseClassNames = new HashSet<string>
        {
            "NonExistent"
        };
        var searchDir = new DirectoryInfo(_tempDir);

        var result = _sut.SearchForBaseClassFiles(baseClassNames, searchDir);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverEntityFilesAsync_WithMatchingEntityFile_ShouldFindFile()
    {
        const string entityCode = "public class Customer { public int Id { get; set; } }";
        var filePath = Path.Combine(_tempDir, "Customer.cs");
        await File.WriteAllTextAsync(filePath, entityCode);

        var entityTypeNames = new HashSet<string>
        {
            "Customer"
        };
        var contextFilePath = Path.Combine(_tempDir, "MyContext.cs");
        await File.WriteAllTextAsync(contextFilePath, "// context file");

        var searchDirectories = new List<string>
        {
            _tempDir
        };

        var result = await _sut.DiscoverEntityFilesAsync(searchDirectories, entityTypeNames, contextFilePath);

        result.Should().ContainKey("Customer");
    }

    [Fact]
    public async Task DiscoverEntityFilesAsync_ContextFileItself_ShouldBeExcluded()
    {
        // Put a class named "Customer" inside the context file — it should be excluded
        const string contextCode = "public class Customer { }";
        var contextFilePath = Path.Combine(_tempDir, "MyContext.cs");
        await File.WriteAllTextAsync(contextFilePath, contextCode);

        var entityTypeNames = new HashSet<string>
        {
            "Customer"
        };
        var searchDirectories = new List<string>
        {
            _tempDir
        };

        var result = await _sut.DiscoverEntityFilesAsync(searchDirectories, entityTypeNames, contextFilePath);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverEntityFilesAsync_NonExistentDirectory_ShouldReturnEmpty()
    {
        var entityTypeNames = new HashSet<string>
        {
            "Customer"
        };
        var searchDirectories = new List<string>
        {
            Path.Combine(_tempDir, "nonexistent")
        };

        var result = await _sut.DiscoverEntityFilesAsync(searchDirectories, entityTypeNames,
            Path.Combine(_tempDir, "ctx.cs"));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverConfigurationFilesAsync_WithSeparateConfigFile_ShouldFindIt()
    {
        const string configCode = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class GadgetConfiguration : IEntityTypeConfiguration<Gadget>
            {
                public void Configure(EntityTypeBuilder<Gadget> builder) { }
            }
            """;
        var configPath = Path.Combine(_tempDir, "GadgetConfiguration.cs");
        await File.WriteAllTextAsync(configPath, configCode);

        var contextFilePath = Path.Combine(_tempDir, "MyContext.cs");
        await File.WriteAllTextAsync(contextFilePath, "public class MyContext { }");

        var result = await _sut.DiscoverConfigurationFilesAsync(new List<string> { _tempDir }, contextFilePath);

        result.Should().ContainKey("GadgetConfiguration");
        result["GadgetConfiguration"].Should().Contain("GadgetConfiguration.cs");
    }

    [Fact]
    public async Task DiscoverConfigurationFilesAsync_ContextFileItself_ShouldBeExcluded()
    {
        const string contextCode = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            public class InlineConfiguration : IEntityTypeConfiguration<Gadget>
            {
                public void Configure(EntityTypeBuilder<Gadget> builder) { }
            }
            """;
        var contextFilePath = Path.Combine(_tempDir, "MyContext.cs");
        await File.WriteAllTextAsync(contextFilePath, contextCode);

        var result = await _sut.DiscoverConfigurationFilesAsync(new List<string> { _tempDir }, contextFilePath);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverConfigurationFilesAsync_NoConfigClasses_ShouldReturnEmpty()
    {
        var entityPath = Path.Combine(_tempDir, "Gadget.cs");
        await File.WriteAllTextAsync(entityPath, "public class Gadget { public int Id { get; set; } }");
        var contextFilePath = Path.Combine(_tempDir, "MyContext.cs");
        await File.WriteAllTextAsync(contextFilePath, "public class MyContext { }");

        var result = await _sut.DiscoverConfigurationFilesAsync(new List<string> { _tempDir }, contextFilePath);

        result.Should().BeEmpty();
    }
}
