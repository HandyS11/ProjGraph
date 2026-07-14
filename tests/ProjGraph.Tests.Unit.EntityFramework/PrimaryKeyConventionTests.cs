using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// EF Core marks a property named <c>Id</c> or <c>{EntityName}Id</c> as the primary key by
/// convention when no key is configured explicitly. For a cross-project entity (whose CLR type is
/// unresolvable from the context file alone) the properties come only from Fluent API config, so the
/// convention must still be applied at model-build time.
/// </summary>
[Trait("Category", "EntityFramework")]
public class PrimaryKeyConventionTests
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
    public async Task AnalyzeContextAsync_UnresolvableEntity_MarksIdAsPrimaryKeyByConvention()
    {
        using var temp = new TestDirectory();
        // Product is never declared in this compilation (simulating a cross-project entity), so its
        // members are only known through the Fluent API references below.
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppContext : DbContext
                               {
                                   public DbSet<Product> Products { get; set; } = null!;
                                   protected override void OnModelCreating(ModelBuilder builder)
                                   {
                                       builder.Entity<Product>().Property(p => p.Id);
                                       builder.Entity<Product>().Property(p => p.Name).HasMaxLength(50);
                                   }
                               }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await _service.AnalyzeContextAsync(filePath, "AppContext");

        var product = model.Entities.Should().ContainSingle(e => e.Name == "Product").Which;
        product.Properties.Should().Contain(p => p.Name == "Id" && p.IsPrimaryKey);
    }

    [Fact]
    public async Task AnalyzeContextAsync_UnresolvableEntity_MarksEntityIdAsPrimaryKeyByConvention()
    {
        using var temp = new TestDirectory();
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppContext : DbContext
                               {
                                   public DbSet<Product> Products { get; set; } = null!;
                                   protected override void OnModelCreating(ModelBuilder builder)
                                   {
                                       builder.Entity<Product>().Property(p => p.ProductId);
                                       builder.Entity<Product>().Property(p => p.Name);
                                   }
                               }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await _service.AnalyzeContextAsync(filePath, "AppContext");

        var product = model.Entities.Should().ContainSingle(e => e.Name == "Product").Which;
        product.Properties.Should().Contain(p => p.Name == "ProductId" && p.IsPrimaryKey);
    }

    [Fact]
    public async Task AnalyzeContextAsync_ExplicitHasKey_DoesNotApplyConventionToId()
    {
        using var temp = new TestDirectory();
        // When a key is configured explicitly the convention must not additionally mark Id as a key.
        const string content = """
                               using Microsoft.EntityFrameworkCore;
                               namespace Test;
                               public class AppContext : DbContext
                               {
                                   public DbSet<Product> Products { get; set; } = null!;
                                   protected override void OnModelCreating(ModelBuilder builder)
                                   {
                                       builder.Entity<Product>().HasKey(p => p.Sku);
                                       builder.Entity<Product>().Property(p => p.Id);
                                       builder.Entity<Product>().Property(p => p.Name);
                                   }
                               }
                               """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await _service.AnalyzeContextAsync(filePath, "AppContext");

        var product = model.Entities.Should().ContainSingle(e => e.Name == "Product").Which;
        product.Properties.Should().Contain(p => p.Name == "Sku" && p.IsPrimaryKey);
        product.Properties.Should().Contain(p => p.Name == "Id" && !p.IsPrimaryKey);
    }
}
