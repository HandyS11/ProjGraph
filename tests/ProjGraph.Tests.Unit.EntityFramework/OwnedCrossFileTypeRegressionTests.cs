using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;
using ProjGraph.Lib.EntityFramework.Rendering;
using ProjGraph.Tests.Shared.Helpers;

namespace ProjGraph.Tests.Unit.EntityFramework;

/// <summary>
/// End-to-end regression test for a live-found bug: <see cref="EntityFileDiscovery.ExtractEntityTypeNames"/>
/// seeds entity-file discovery only from <c>DbSet&lt;T&gt;</c> declarations, so an <c>OwnsOne</c>/<c>OwnsMany</c>
/// navigation whose CLR type lives in a file the DbSet scan never visits was never added to the compilation.
/// <c>FluentOwnedTypeWalker.ResolveOwnedType</c> then resolved the navigation property's type to an
/// <c>IErrorTypeSymbol</c> — which still satisfies <c>is INamedTypeSymbol</c> — so the owned type was captured
/// as an entity with ZERO properties instead of failing loudly. Combined with table-split inlining, that
/// zero-property owned entity silently vanished: no columns, no box, no trace it ever existed. Verified live
/// via <c>projgraph erd samples/erd/complex-ecommerce/Data/MyDbContext.cs</c>, where <c>OwnsOne(p => p.Price)</c>
/// (type <c>Money</c>, declared in a separate file) produced no <c>Price_*</c> columns at all.
/// The fixture lives in an isolated <see cref="TestDirectory"/> rather than the shared Golden/fixtures
/// directory: every owned-type fixture already there declares its owned CLR type in the SAME file as the
/// context, which is exactly the blind spot that hid this bug, and adding a same-named type to that shared
/// directory risks a duplicate-name collision merging into an unrelated fixture's symbols (EntityFileDiscovery
/// scans the whole Golden/fixtures directory for every golden context).
/// </summary>
[Trait("Category", "EntityFramework")]
public sealed class OwnedCrossFileTypeRegressionTests
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
    public async Task AnalyzeContextAsync_OwnedTypeClrTypeInSeparateFile_ColumnsCapturedAndRendered()
    {
        using var temp = new TestDirectory();

        // The context and Product live in the same file (the DbSet-seeded discovery already handles that
        // case); Money -- the OwnsOne navigation's CLR type -- is declared in a separate file the DbSet
        // scan never names, and carries no explicit property configuration at all, so its columns can only
        // reach the model through CLR seeding, which requires Money's file to have been added to the
        // compilation in the first place.
        const string contextContent = """
            using Microsoft.EntityFrameworkCore;
            namespace Test;

            public class CrossFileOwnedContext : DbContext
            {
                public DbSet<Product> Products { get; set; } = null!;

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Product>().OwnsOne(p => p.Price);
                }
            }

            public class Product
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
                public Money Price { get; set; } = null!;
            }
            """;
        const string moneyContent = """
            namespace Test;

            public record Money(decimal Amount, string Currency = "USD");
            """;

        var contextPath = temp.CreateFile("Context.cs", contextContent);
        temp.CreateFile("Money.cs", moneyContent);

        var model = await CreateService().AnalyzeContextAsync(contextPath, "CrossFileOwnedContext");

        var owned = model.Entities.SingleOrDefault(e => e.Key == "Product.Price");
        owned.Should().NotBeNull(
            "OwnsOne(p => p.Price) must capture the owned entity even though Money.cs is a separate file");
        owned!.Properties.Select(p => p.Name).Should().BeEquivalentTo(["Amount", "Currency"],
            "Money's columns can only come from CLR seeding — there is no Property() call for either — " +
            "so this fails unless Money.cs was pulled into the compilation and resolved");

        var rendered = new MermaidErdRenderer().Render(model, new DiagramOptions(false, false));
        rendered.Should().Contain("Price_Amount", "the table-split owned columns must be inlined onto Product");
        rendered.Should().Contain("Price_Currency");
    }
}
