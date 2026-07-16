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

        var shipTo = model.Entities.Should().ContainSingle(e => e.Key == "NestedInvoice.ShipTo").Subject;
        shipTo.OwnerEntity.Should().Be("NestedInvoice");
        shipTo.NavigationName.Should().Be("ShipTo");

        var geo = model.Entities.Should().ContainSingle(e => e.Key == "NestedInvoice.ShipTo.Geo").Subject;
        geo.Name.Should().Be("GeoTag");
        geo.OwnerEntity.Should().Be("NestedInvoice.ShipTo",
            "the owner key must be the ShipTo owned entity's EffectiveKey, not the NestedInvoice root");
        geo.NavigationName.Should().Be("Geo");
        geo.Properties.Should().Contain(p => p.Name == "Latitude",
            "the chained Property call after the nested OwnsOne configures Geo, not the owner");

        model.Entities.Should().NotContain(e => e.Name == "NestedInvoice" && e.Properties.Any(p => p.Name == "Latitude"),
            "nested owned config must never leak onto the root entity");
    }

    [Fact]
    public void OwnsMany_CapturesCollectionOwnedTypeOnItsOwnTable()
    {
        var model = Analyze("OwnedModesContext.cs", "OwnedModesContext");

        var lines = model.Entities.Single(e => e.NavigationName == "Lines");
        lines.Name.Should().Be("InvoiceLine");
        lines.IsOwned.Should().BeTrue();
        lines.IsCollection.Should().BeTrue();
        lines.OwnerEntity.Should().Be("OwnedModesInvoice");
        lines.TableName.Should().Be("OwnedModesInvoice_Lines",
            "an owned collection never shares the owner's table; EF's default is {{OwnerTable}}_{{Nav}}");
        lines.Properties.Should().ContainSingle(p => p.Name == "Amount")
            .Which.Precision.Should().Be(18);
    }

    [Fact]
    public void OwnsOne_WithToTable_GetsItsOwnTable()
    {
        var model = Analyze("OwnedModesContext.cs", "OwnedModesContext");

        var billTo = model.Entities.Single(e => e.NavigationName == "BillTo");
        billTo.TableName.Should().Be("BillingAddresses");
        billTo.IsCollection.Should().BeFalse();
    }

    [Fact]
    public void OwnsOne_WithoutToTable_SharesOwnerTable()
    {
        var model = Analyze("OwnedModesContext.cs", "OwnedModesContext");

        var shipTo = model.Entities.Single(e => e.NavigationName == "ShipTo");
        shipTo.TableName.Should().Be("OwnedModesInvoice", "table-splitting maps the owned type to the owner's table");
        shipTo.Properties.Should().ContainSingle(p => p.Name == "Street")
            .Which.MaxLength.Should().Be(180);
    }

    [Fact]
    public void OwnsOne_NestedInOwnedBuilder_IsOwnedByTheOwnedType()
    {
        var model = Analyze("OwnedModesContext.cs", "OwnedModesContext");

        var geo = model.Entities.Single(e => e.NavigationName == "Geo");
        geo.IsOwned.Should().BeTrue();
        geo.OwnerEntity.Should().Be("OwnedModesInvoice.ShipTo",
            "nested ownership chains through the owned type's KEY, not its CLR name — ShipTo and BillTo " +
            "are both OwnedModesAddress, so a name-keyed owner would attach Geo to both");
        geo.Properties.Should().ContainSingle(p => p.Name == "Latitude")
            .Which.Precision.Should().Be(9);
    }

    [Fact]
    public void OwnsOne_StringLiteralInLaterArgument_DoesNotMisreadItAsTheOwnedTypeName()
    {
        // Regression for ResolveOwnedType's snapshot-form detection: it must key off the FIRST ARGUMENT
        // itself being a string literal (the snapshot's OwnsOne("Ns.Type", "nav", ...) shape), not merely
        // the presence of a string literal anywhere in the argument list. A looser check would misread
        // the "AddressTable" literal at argument position 1 as the owned type's name, fail symbol
        // resolution, and degrade to a bare entity — Country is only seeded via CLR reflection on the
        // correctly-resolved owned type, so its presence proves the lambda form still wins.
        var model = Analyze("OwnsOneArgOrderContext.cs", "OwnsOneArgOrderContext");

        var address = model.Entities.Should().ContainSingle(e => e.IsOwned).Subject;
        address.Name.Should().Be("GizmoAddress");
        address.Properties.Should().Contain(p => p.Name == "Country",
            "Country is only seeded via CLR reflection on the resolved owned type; if the string literal " +
            "at argument position 1 were misread as the type name, symbol resolution would fail and the " +
            "owned entity would degrade to a bare entity containing only the explicitly configured City column");
        address.Properties.Should().ContainSingle(p => p.Name == "City").Which.MaxLength.Should().Be(50);
    }

    [Fact]
    public void ConfigClass_OwnsOne_WithoutToTable_InlinesOntoOwnerTable()
    {
        // Regression for the gap where EntityConfigurationWalker never ran FluentOwnedTypeWalker:
        // owned types configured inside an IEntityTypeConfiguration<T>.Configure body (rather than
        // OnModelCreating directly) were silently dropped - exactly eShopOnWeb's
        // Order.OwnsOne(o => o.ShipToAddress, ...) shape, this feature's motivating case.
        var model = Analyze("VendorConfigContext.cs", "VendorConfigContext");

        var headOffice = model.Entities.Should().ContainSingle(e => e.NavigationName == "HeadOffice").Subject;
        headOffice.Key.Should().Be("Vendor.HeadOffice");
        headOffice.OwnerEntity.Should().Be("Vendor");
        headOffice.IsCollection.Should().BeFalse();
        headOffice.TableName.Should().Be("Vendor",
            "OwnsOne without ToTable table-splits onto the owner's effective table even when configured " +
            "from a separate IEntityTypeConfiguration<T> class");
        headOffice.Properties.Should().ContainSingle(p => p.Name == "Street")
            .Which.MaxLength.Should().Be(180);
        headOffice.Properties.Should().ContainSingle(p => p.Name == "City")
            .Which.MaxLength.Should().Be(80);
    }

    [Fact]
    public void ConfigClass_OwnsOne_WithChainedToTable_GetsItsOwnTable()
    {
        // Warehouse's ToTable is chained onto the OwnsOne call itself (no builder lambda), so it is
        // only resolvable once FluentOwnedTypeWalker.Apply has materialized Vendor.Warehouse -
        // exercising the same two-pass FluentEntityWalker ordering the DbContext/snapshot paths rely on.
        var model = Analyze("VendorConfigContext.cs", "VendorConfigContext");

        var warehouse = model.Entities.Should().ContainSingle(e => e.NavigationName == "Warehouse").Subject;
        warehouse.Key.Should().Be("Vendor.Warehouse");
        warehouse.OwnerEntity.Should().Be("Vendor");
        warehouse.IsCollection.Should().BeFalse();
        warehouse.TableName.Should().Be("VendorWarehouses",
            "the chained ToTable configures the owned Warehouse type, not the owner");
        warehouse.Properties.Should().ContainSingle(p => p.Name == "Code")
            .Which.MaxLength.Should().Be(20);
    }

    [Fact]
    public void ConfigClass_OwnedTypes_DoNotLeakOntoOwner()
    {
        var model = Analyze("VendorConfigContext.cs", "VendorConfigContext");

        var vendor = model.Entities.Single(e => e.Name == "Vendor");
        vendor.Properties.Should().NotContain(p => p.Name == "Street" || p.Name == "City" || p.Name == "Code",
            "nested owned config from a config class must never leak onto the owner");
        vendor.Properties.Should().NotContain(p => p.Name == "HeadOffice" || p.Name == "Warehouse",
            "owned navigations are not columns");
    }

    internal static EfModel AnalyzeSnapshot(string fileName, string snapshotName)
    {
        var fs = new PhysicalFileSystem();
        var analyzer = new EfModelAnalyzer(new CompilationFactory(), fs, new EntityFileDiscovery(fs));
        var service = new EfAnalysisService(
            new AnalyzeContextUseCase(analyzer),
            new DiscoverContextsUseCase(analyzer, fs),
            new AnalyzeSnapshotUseCase(analyzer),
            new DiscoverSnapshotsUseCase(analyzer, fs));
#pragma warning disable VSTHRD002 // Deliberate sync-over-async bridge, as in EfGoldenRunner.
        return service.AnalyzeSnapshotAsync(FixturePath(fileName), snapshotName).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    [Fact]
    public void Snapshot_OwnsOne_CapturesOwnedTypeFromStringLiteralForm()
    {
        var model = AnalyzeSnapshot("OwnedSnapshot.cs", "BillingContextModelSnapshot");

        var shipTo = model.Entities.Single(e => e.NavigationName == "ShipToAddress");
        shipTo.Name.Should().Be("ReceiptAddress");
        shipTo.OwnerEntity.Should().Be("Receipt");
        shipTo.IsCollection.Should().BeFalse();
        shipTo.TableName.Should().Be("Receipts", "the snapshot's explicit ToTable matches the owner's");
        shipTo.Properties.Should().ContainSingle(p => p.Name == "City").Which.MaxLength.Should().Be(100);
    }

    [Fact]
    public void Snapshot_OwnedShadowKey_IsNotRecordedAsPrimaryKey()
    {
        var model = AnalyzeSnapshot("OwnedSnapshot.cs", "BillingContextModelSnapshot");

        var shipTo = model.Entities.Single(e => e.NavigationName == "ShipToAddress");
        shipTo.Properties.Should().NotContain(p => p.IsPrimaryKey,
            "an owned type's shadow key is an EF implementation detail, not a modelled column");
    }

    [Fact]
    public void Snapshot_TableSplitOwnedType_DropsTheOwnerForeignKeyColumn()
    {
        var model = AnalyzeSnapshot("OwnedSnapshot.cs", "BillingContextModelSnapshot");

        var shipTo = model.Entities.Single(e => e.NavigationName == "ShipToAddress");
        shipTo.Properties.Should().NotContain(p => p.Name == "ReceiptId",
            "a table-split owned type's FK is the owner's own PK column re-projected, not an extra " +
            "column; the DbContext path cannot see it at all, so keeping it would break cross-path parity");
        shipTo.Properties.Should().Contain(p => p.Name == "City");
    }

    [Fact]
    public void Snapshot_OwnsMany_CapturesCollectionOnItsOwnTableAndKeepsItsForeignKey()
    {
        var model = AnalyzeSnapshot("OwnedSnapshot.cs", "BillingContextModelSnapshot");

        var notes = model.Entities.Single(e => e.NavigationName == "Notes");
        notes.IsCollection.Should().BeTrue();
        notes.TableName.Should().Be("ReceiptNotes");
        notes.Properties.Should().ContainSingle(p => p.Name == "ReceiptId")
            .Which.IsForeignKey.Should().BeTrue(
                "an owned type on its own table has a real, separate FK column back to the owner");
    }
}
