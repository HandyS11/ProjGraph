using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Lib.EntityFramework.Rendering;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// End-to-end regression test for a live-found bug: <c>EfModelAnalyzer.CacheClassDeclarations</c> (the
/// pre-pass that resolves an <c>OwnsOne</c>/<c>OwnsMany</c> navigation's CLR type so its file can be pulled
/// into the compilation) filtered on <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax"/>
/// only, while <see cref="EntityFileDiscovery"/>'s own scan was already widened to
/// <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax"/> to find <c>record</c>-declared
/// types. The mismatch meant ANY <c>record</c>-declared owner — including a top-level <c>DbSet</c> entity
/// declared in the same file as the context — was invisible to the pre-pass: its navigation property could
/// never be found, so a cross-file owned type's file was never discovered, and the owned entity fell back to
/// the spec's zero-property "empty box" safety net instead of rendering its real columns.
/// The fixture lives in an isolated <see cref="TestDirectory"/> rather than the shared Golden/fixtures
/// directory, matching <c>OwnedCrossFileTypeRegressionTests</c>: a same-named type added to that shared
/// directory risks a duplicate-name collision merging into an unrelated fixture's symbols
/// (<see cref="EntityFileDiscovery"/> scans the whole Golden/fixtures directory for every golden context).
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class OwnedRecordOwnerRegressionTests
{
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
    public async Task AnalyzeContextAsync_RecordDeclaredOwner_CrossFileOwnedTypeColumnsCapturedAndRendered()
    {
        using var temp = new TestDirectory();

        // Product -- the OwnsOne navigation's owner and the DbSet root entity -- is declared as a `record`
        // (with ordinary body-declared properties, not a positional parameter list) in the SAME file as the
        // context. Money -- the navigation's CLR type -- is declared in a separate file the DbSet scan
        // never names, so its columns can only reach the model if the record-declared owner's Price
        // property was found by the pre-pass in the first place.
        const string contextContent = """
            using Microsoft.EntityFrameworkCore;
            namespace Test;

            public class RecordOwnerContext : DbContext
            {
                public DbSet<Product> Products { get; set; } = null!;

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Product>().OwnsOne(p => p.Price);
                }
            }

            public record Product
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
                public Money Price { get; set; } = null!;
            }
            """;
        const string moneyContent = """
            namespace Test;

            public class Money
            {
                public decimal Amount { get; set; }
                public string Currency { get; set; } = "USD";
            }
            """;

        var contextPath = temp.CreateFile("Context.cs", contextContent);
        temp.CreateFile("Money.cs", moneyContent);

        var model = await CreateService().AnalyzeContextAsync(contextPath, "RecordOwnerContext");

        var owned = model.Entities.SingleOrDefault(e => e.Key == "Product.Price");
        owned.Should().NotBeNull(
            "OwnsOne(p => p.Price) must capture the owned entity even though Product is record-declared " +
            "and Money.cs is a separate file");
        owned.Properties.Select(p => p.Name).Should().BeEquivalentTo(["Amount", "Currency"],
            "the pre-pass must find Price on the record-declared Product to discover Money.cs and pull it " +
            "into the compilation; without that, Money resolves to an error type and the entity is captured " +
            "with zero properties");

        var rendered = new MermaidErdRenderer().Render(model, new DiagramOptions(false, false));
        rendered.Should().Contain("Price_Amount", "the table-split owned columns must be inlined onto Product");
        rendered.Should().Contain("Price_Currency");
        rendered.Should().NotContain("Price {\n  }",
            "a record-declared owner must not fall back to the zero-property empty-box safety net");
    }

    [Fact]
    public async Task AnalyzeContextAsync_PositionalRecordOwner_CrossFileOwnedRecordColumnsCapturedAndRendered()
    {
        using var temp = new TestDirectory();

        // Product is a POSITIONAL record: its Price navigation is a primary-constructor parameter, not a
        // PropertyDeclarationSyntax member, so the pre-pass's member-based nav lookup missed it entirely.
        // Money.cs then stayed out of the compilation, the navigation resolved to an error type, and the
        // owned entity degraded to the spec's empty-box floor. Regression for the parameter-list nav
        // lookup (issue #163, item 1); Money is itself a positional record, per the issue's fixture shape.
        const string contextContent = """
            using Microsoft.EntityFrameworkCore;
            namespace Test;

            public class PositionalRecordContext : DbContext
            {
                public DbSet<Product> Products { get; set; } = null!;

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Product>().OwnsOne(p => p.Price);
                }
            }

            public record Product(int Id, Money Price);
            """;
        const string moneyContent = """
            namespace Test;

            public record Money(decimal Amount, string Currency);
            """;

        var contextPath = temp.CreateFile("Context.cs", contextContent);
        temp.CreateFile("Money.cs", moneyContent);

        var model = await CreateService().AnalyzeContextAsync(contextPath, "PositionalRecordContext");

        var owned = model.Entities.SingleOrDefault(e => e.Key == "Product.Price");
        owned.Should().NotBeNull(
            "OwnsOne(p => p.Price) must capture the owned entity even though Price is declared as a " +
            "primary-constructor parameter rather than a property member");
        owned.Properties.Select(p => p.Name).Should().BeEquivalentTo(["Amount", "Currency"],
            "the pre-pass must find the Price parameter on the positional record to discover Money.cs; " +
            "without that, Money resolves to an error type and the owned entity renders as an empty box");

        var rendered = new MermaidErdRenderer().Render(model, new DiagramOptions(false, false));
        rendered.Should().Contain("Price_Amount", "the table-split owned columns must be inlined onto Product");
        rendered.Should().Contain("Price_Currency");
    }
}
