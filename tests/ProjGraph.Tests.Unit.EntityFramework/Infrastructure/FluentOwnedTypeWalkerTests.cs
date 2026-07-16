using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Infrastructure;
using ProjGraph.Lib.EntityFramework.Application;
using ProjGraph.Lib.EntityFramework.Application.UseCases;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework.Infrastructure;

public sealed class FluentOwnedTypeWalkerTests
{
    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Golden", "fixtures", fileName);

    internal static EfModel Analyze(string fileName, string contextName)
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        var service = new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));
#pragma warning disable VSTHRD002 // Deliberate sync-over-async bridge, as in EfGoldenRunner.
        return service.AnalyzeContextAsync(FixturePath(fileName), contextName).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    [Fact]
    public void OwnsOne_LambdaForm_CapturesOwnedEntityWithOwnerMetadata()
    {
        var model = Analyze("OwnedAndJoinContext.cs", "OwnedAndJoinContext");

        var address = model.Entities.Should().ContainSingle(e => e.IsOwned).Subject;
        address.Name.Should().Be("Address");
        address.OwnerEntity.Should().Be("Customer");
        address.NavigationName.Should().Be("Address");
        address.IsCollection.Should().BeFalse();
        address.TableName.Should().Be("Customer",
            "OwnsOne without ToTable is table-splitting, so it maps to the owner's effective table");
    }

    [Fact]
    public void OwnsOne_LambdaForm_AppliesNestedPropertyConfigurationToOwnedNotOwner()
    {
        var model = Analyze("OwnedAndJoinContext.cs", "OwnedAndJoinContext");

        var address = model.Entities.Single(e => e.IsOwned);
        address.Properties.Should().ContainSingle(p => p.Name == "City")
            .Which.MaxLength.Should().Be(50);

        var customer = model.Entities.Single(e => e.Name == "Customer");
        customer.Properties.Should().NotContain(p => p.Name == "City",
            "nested owned config must never leak onto the owner");
        customer.Properties.Should().NotContain(p => p.Name == "Address",
            "the owned navigation is not a column");
    }

    [Fact]
    public void OwnsOne_ChainedForm_MergesRepeatedCallsIntoOneOwnedEntity()
    {
        var model = Analyze("ChainedOwnedContext.cs", "ChainedOwnedContext");

        var owned = model.Entities.Should().ContainSingle(e => e.IsOwned).Subject;
        owned.Name.Should().Be("PostalAddress");
        owned.OwnerEntity.Should().Be("Shopper");
        owned.NavigationName.Should().Be("Address");
        owned.TableName.Should().Be("ShopperAddresses",
            "the chained ToTable configures the owned type, not the owner");
        owned.Properties.Should().ContainSingle(p => p.Name == "City")
            .Which.MaxLength.Should().Be(50);
    }

    [Fact]
    public void OwnsOne_ChainedForm_DoesNotLeakOntoOwner()
    {
        var model = Analyze("ChainedOwnedContext.cs", "ChainedOwnedContext");

        var shopper = model.Entities.Single(e => e.Name == "Shopper");
        shopper.Properties.Should().NotContain(p => p.Name == "City");
        shopper.TableName.Should().BeEmpty("ToTable(\"ShopperAddresses\") targets the owned type");
        shopper.Properties.Should().Contain(p => p.Name == "Tags",
            "the EF 8+ primitive collection must survive as a scalar column");
    }

    [Fact]
    public void TwoOwnedNavigationsSharingClrName_BothSurviveDeduplication()
    {
        // Regression for the Name-keyed DeduplicateModelContent bug: Invoice.ShipTo and Invoice.BillTo
        // are both InvoiceAddress. Grouping by Name (instead of EffectiveKey) would collapse them into
        // one, silently discarding the second.
        var model = Analyze("DualNavOwnedContext.cs", "DualNavOwnedContext");

        var owned = model.Entities.Where(e => e.IsOwned).ToList();
        owned.Should().HaveCount(2, "both Invoice.ShipTo and Invoice.BillTo must survive analysis");

        var shipTo = owned.Should().ContainSingle(e => e.Key == "Invoice.ShipTo").Subject;
        shipTo.Name.Should().Be("InvoiceAddress");
        shipTo.OwnerEntity.Should().Be("Invoice");
        shipTo.NavigationName.Should().Be("ShipTo");

        var billTo = owned.Should().ContainSingle(e => e.Key == "Invoice.BillTo").Subject;
        billTo.Name.Should().Be("InvoiceAddress");
        billTo.OwnerEntity.Should().Be("Invoice");
        billTo.NavigationName.Should().Be("BillTo");
    }

    [Fact]
    public void OwnsOne_NestedChainedForm_CapturesInnerOwnedEntityWithChainedConfig()
    {
        // Regression for the pre-order FindConfigRoots ordering bug: in
        // Entity<T>().OwnsOne(a).OwnsOne(b).Property(...), the syntactically outermost node is the
        // .OwnsOne(b) call (.OwnsOne(a) is nested inside it as its receiver), so an unordered walk
        // resolves b's owner key to a's {Owner}.{Nav} before a itself has been captured, silently
        // dropping b and its chained Property config.
        var model = Analyze("NestedChainedOwnedContext.cs", "NestedChainedOwnedContext");

        var shipTo = model.Entities.Should().ContainSingle(e => e.Key == "Invoice.ShipTo").Subject;
        shipTo.OwnerEntity.Should().Be("Invoice");
        shipTo.NavigationName.Should().Be("ShipTo");

        var geo = model.Entities.Should().ContainSingle(e => e.Key == "Invoice.ShipTo.Geo").Subject;
        geo.Name.Should().Be("GeoTag");
        geo.OwnerEntity.Should().Be("Invoice.ShipTo",
            "the owner key must be the ShipTo owned entity's EffectiveKey, not the Invoice root");
        geo.NavigationName.Should().Be("Geo");
        geo.Properties.Should().Contain(p => p.Name == "Latitude",
            "the chained Property call after the nested OwnsOne configures Geo, not the owner");

        model.Entities.Should().NotContain(e => e.Name == "Invoice" && e.Properties.Any(p => p.Name == "Latitude"),
            "nested owned config must never leak onto the root entity");
    }
}
