using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// For a cross-project entity (whose CLR type is unresolvable from the context file), a property's
/// type is only guessed by name convention and defaults to <c>string</c>. When the Fluent API
/// specifies an explicit SQL column type via <c>HasColumnType</c>, that column type is authoritative
/// and lets us recover a more accurate CLR type than the <c>string</c> fallback.
/// </summary>
[Trait("Category", "EntityFramework")]
public class ColumnTypeInferenceTests
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

    private async Task<ProjGraph.Core.Models.EfProperty> AnalyzePropertyAsync(
        string propertyName, string columnType)
    {
        using var temp = new TestDirectory();
        var content = $$"""
                        using Microsoft.EntityFrameworkCore;
                        namespace Test;
                        public class AppContext : DbContext
                        {
                            public DbSet<Product> Products { get; set; } = null!;
                            protected override void OnModelCreating(ModelBuilder builder)
                            {
                                builder.Entity<Product>().Property(p => p.{{propertyName}})
                                    .HasColumnType("{{columnType}}");
                            }
                        }
                        """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await _service.AnalyzeContextAsync(filePath, "AppContext");
        var product = model.Entities.Single(e => e.Name == "Product");
        return product.Properties.Single(p => p.Name == propertyName);
    }

    [Theory]
    [InlineData("decimal(18,2)", "decimal")]
    [InlineData("uniqueidentifier", "Guid")]
    [InlineData("bit", "bool")]
    [InlineData("bigint", "long")]
    [InlineData("datetime2", "DateTime")]
    public async Task HasColumnType_InfersClrTypeForUnresolvedProperty(string columnType, string expectedType)
    {
        var property = await AnalyzePropertyAsync("Amount", columnType);

        property.Type.Should().Be(expectedType);
    }

    [Fact]
    public async Task HasColumnType_Decimal_ExtractsPrecisionAndScale()
    {
        var property = await AnalyzePropertyAsync("Amount", "decimal(18,2)");

        property.Precision.Should().Be(18);
        property.Scale.Should().Be(2);
    }

    [Fact]
    public async Task HasColumnType_StringColumn_KeepsStringAndMaxLength()
    {
        var property = await AnalyzePropertyAsync("Name", "nvarchar(200)");

        property.Type.Should().Be("string");
        property.MaxLength.Should().Be(200);
    }

    [Fact]
    public async Task HasMaxLength_OnUnresolvedIdNamedProperty_InfersStringNotGuid()
    {
        // A property whose name ends in "Id" and whose CLR type is unresolvable is guessed as a Guid.
        // A max length is meaningless for a Guid column, so the constraint is proof the column is a
        // string: eShopOnWeb configures `BuyerId` with HasMaxLength(256) and it is a string, but the
        // ERD rendered the self-contradictory `Guid BuyerId "required, max:256"`.
        using var temp = new TestDirectory();
        const string content = """
                      using Microsoft.EntityFrameworkCore;
                      namespace Test;
                      public class AppContext : DbContext
                      {
                          public DbSet<Order> Orders { get; set; } = null!;
                          protected override void OnModelCreating(ModelBuilder builder)
                          {
                              builder.Entity<Order>().Property(o => o.BuyerId)
                                  .IsRequired()
                                  .HasMaxLength(256);
                          }
                      }
                      """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await _service.AnalyzeContextAsync(filePath, "AppContext");
        var property = model.Entities.Single(e => e.Name == "Order").Properties.Single(p => p.Name == "BuyerId");

        property.Type.Should().Be("string");
        property.MaxLength.Should().Be(256);
    }

    [Fact]
    public async Task HasMaxLength_AfterExplicitColumnType_KeepsColumnTypesClrType()
    {
        // HasColumnType is authoritative, so a max length chained after it must not undo the recovered
        // CLR type the way it corrects a name-guessed one.
        using var temp = new TestDirectory();
        const string content = """
                      using Microsoft.EntityFrameworkCore;
                      namespace Test;
                      public class AppContext : DbContext
                      {
                          public DbSet<Order> Orders { get; set; } = null!;
                          protected override void OnModelCreating(ModelBuilder builder)
                          {
                              builder.Entity<Order>().Property(o => o.BuyerId)
                                  .HasColumnType("uniqueidentifier")
                                  .HasMaxLength(36);
                          }
                      }
                      """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await _service.AnalyzeContextAsync(filePath, "AppContext");
        var property = model.Entities.Single(e => e.Name == "Order").Properties.Single(p => p.Name == "BuyerId");

        property.Type.Should().Be("Guid");
    }

    [Fact]
    public async Task HasMaxLength_OnResolvedGuidProperty_KeepsDeclaredType()
    {
        // The inference must not rewrite a type the CLR actually declares.
        using var temp = new TestDirectory();
        const string content = """
                      using System;
                      using Microsoft.EntityFrameworkCore;
                      namespace Test;
                      public class Order
                      {
                          public int Id { get; set; }
                          public Guid BuyerId { get; set; }
                      }
                      public class AppContext : DbContext
                      {
                          public DbSet<Order> Orders { get; set; } = null!;
                          protected override void OnModelCreating(ModelBuilder builder)
                          {
                              builder.Entity<Order>().Property(o => o.BuyerId).HasMaxLength(256);
                          }
                      }
                      """;
        var filePath = temp.CreateFile("Context.cs", content);

        var model = await _service.AnalyzeContextAsync(filePath, "AppContext");
        var property = model.Entities.Single(e => e.Name == "Order").Properties.Single(p => p.Name == "BuyerId");

        property.Type.Should().Be("Guid");
    }

    [Theory]
    [InlineData("decimal(18)")]
    [InlineData("numeric(18)")]
    [InlineData("float(24)")]
    public async Task HasColumnType_SingleArgNumericColumn_DoesNotSetMaxLength(string columnType)
    {
        // The number in a numeric column type is precision, not string length; it must never be
        // recorded as MaxLength, which is only meaningful for string columns.
        var property = await AnalyzePropertyAsync("Amount", columnType);

        property.MaxLength.Should().BeNull();
    }
}
