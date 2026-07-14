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
}
