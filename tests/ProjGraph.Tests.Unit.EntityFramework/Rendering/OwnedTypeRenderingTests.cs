using ProjGraph.Core.Models;
using ProjGraph.Lib.Core.Abstractions;
using ProjGraph.Lib.EntityFramework.Rendering;

namespace ProjGraph.Tests.Unit.EntityFramework.Rendering;

public sealed class OwnedTypeRenderingTests
{
    private static EfModel ModelWithOwned(string ownedTable, bool isCollection)
    {
        var order = new EfEntity { Name = "Order", TableName = "Orders" };
        order.Properties.Add(new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true, IsValueType = true });

        var address = new EfEntity
        {
            Name = "Address",
            IsOwned = true,
            OwnerEntity = "Order",
            NavigationName = "ShipToAddress",
            IsCollection = isCollection,
            TableName = ownedTable
        };
        address.Properties.Add(new EfProperty
        {
            Name = "ZipCode",
            Type = "string",
            IsExplicitlyRequired = true,
            IsRequired = true,
            MaxLength = 18
        });

        var model = new EfModel { ContextName = "Ctx" };
        model.Entities.Add(order);
        model.Entities.Add(address);
        return model;
    }

    private static string Render(EfModel model) =>
        new MermaidErdRenderer().Render(model, new DiagramOptions(false, false));

    [Fact]
    public void Classic_NestedOwnedTypesUnderSameNamedOwners_GetDistinctBoxIdentifiers()
    {
        // Invoice owns ShipTo and BillTo (both CLR "Address"); EACH owns a nested "GeoPoint".
        // Qualifying a colliding label by the owner's CLR name would produce "Address_Geo" for BOTH
        // nested boxes — one Mermaid identifier for two entities, silently merged into one box.
        var invoice = new EfEntity { Name = "Invoice" };
        invoice.Properties.Add(new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true, IsValueType = true });

        var model = new EfModel { ContextName = "Ctx" };
        model.Entities.Add(invoice);

        foreach (var nav in new[] { "ShipTo", "BillTo" })
        {
            var address = new EfEntity
            {
                Name = "Address",
                Key = $"Invoice.{nav}",
                IsOwned = true,
                OwnerEntity = "Invoice",
                NavigationName = nav
            };
            address.Properties.Add(new EfProperty { Name = "Street", Type = "string" });

            var geo = new EfEntity
            {
                Name = "GeoPoint",
                Key = $"Invoice.{nav}.Geo",
                IsOwned = true,
                OwnerEntity = $"Invoice.{nav}",
                NavigationName = "Geo"
            };
            geo.Properties.Add(new EfProperty { Name = "Latitude", Type = "decimal", IsValueType = true });

            model.Entities.Add(address);
            model.Entities.Add(geo);
        }

        var output = new MermaidErdRenderer().Render(
            model, new DiagramOptions(false, false, false, ErdOwnedMode.Classic));

        output.Should().Contain("Invoice_ShipTo_Geo {");
        output.Should().Contain("Invoice_BillTo_Geo {");
        output.Should().NotContain("Address_Geo",
            "qualifying by the owner's CLR name collapses both nested boxes into one Mermaid identifier");
        output.Should().Contain("Invoice_ShipTo ||--|| Invoice_ShipTo_Geo");
        output.Should().Contain("Invoice_BillTo ||--|| Invoice_BillTo_Geo");
    }

    [Fact]
    public void MirrorEf_TableSplitOwnsOne_InlinesPrefixedColumnsOntoOwner()
    {
        var output = Render(ModelWithOwned("Orders", isCollection: false));

        output.Should().Contain("ShipToAddress_ZipCode");
        output.Should().Contain("required, max:18");
        output.Should().NotContain("Address {", "a table-split owned type must not get its own box");
        output.Should().NotContain("||--||", "an inlined owned type has no relationship line");
    }

    [Fact]
    public void MirrorEf_OwnsOneWithOwnTable_RendersBoxAndIdentifyingRelationship()
    {
        var output = Render(ModelWithOwned("ShipToAddresses", isCollection: false));

        output.Should().Contain("Address {");
        output.Should().Contain("string ZipCode", "a separate box keeps unprefixed column names");
        output.Should().Contain("Order ||--|| Address");
        output.Should().NotContain("ShipToAddress_ZipCode");
    }

    [Fact]
    public void MirrorEf_OwnsMany_AlwaysRendersBoxWithCollectionRelationship()
    {
        var output = Render(ModelWithOwned("Order_ShipToAddress", isCollection: true));

        output.Should().Contain("Address {");
        output.Should().Contain("Order ||--o{ Address");
    }

    /// <summary>
    /// Builds a model mirroring Invoice.ShipTo / Invoice.BillTo, both CLR type <c>InvoiceAddress</c>,
    /// where only ShipTo owns a nested <c>Geo</c> type table-split onto it. Matching owner-to-owned on
    /// CLR Name (instead of Key) would attach Geo to both siblings, since they share a Name.
    /// </summary>
    private static EfModel ModelWithSameNamedSiblingsOneOwningNested()
    {
        var invoice = new EfEntity { Name = "Invoice", TableName = "Invoice" };
        invoice.Properties.Add(new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true, IsValueType = true });

        var shipTo = new EfEntity
        {
            Name = "InvoiceAddress",
            Key = "Invoice.ShipTo",
            IsOwned = true,
            OwnerEntity = "Invoice",
            NavigationName = "ShipTo",
            TableName = "Invoice"
        };
        shipTo.Properties.Add(new EfProperty { Name = "Street", Type = "string" });

        var billTo = new EfEntity
        {
            Name = "InvoiceAddress",
            Key = "Invoice.BillTo",
            IsOwned = true,
            OwnerEntity = "Invoice",
            NavigationName = "BillTo",
            TableName = "Invoice"
        };
        billTo.Properties.Add(new EfProperty { Name = "Street", Type = "string" });

        var geo = new EfEntity
        {
            Name = "Geo",
            Key = "Invoice.ShipTo.Geo",
            IsOwned = true,
            OwnerEntity = "Invoice.ShipTo",
            NavigationName = "Geo",
            TableName = "Invoice"
        };
        geo.Properties.Add(new EfProperty { Name = "Latitude", Type = "double", IsValueType = true });

        var model = new EfModel { ContextName = "Ctx" };
        model.Entities.Add(invoice);
        model.Entities.Add(shipTo);
        model.Entities.Add(billTo);
        model.Entities.Add(geo);
        return model;
    }

    [Fact]
    public void MirrorEf_NestedOwnedType_InlinesOntoTrueOwnerOnlyNotSameNamedSibling()
    {
        var output = Render(ModelWithSameNamedSiblingsOneOwningNested());

        // Geo table-splits onto ShipTo, which itself table-splits onto Invoice, so the nested
        // Geo_Latitude column compounds onto Invoice's box under the ShipTo_Geo_ prefix.
        output.Should().Contain("ShipTo_Geo_Latitude",
            "Geo must be attributed to its true owner ShipTo, keyed Invoice.ShipTo");
        output.Should().NotContain("BillTo_Geo_Latitude",
            "ShipTo and BillTo share the CLR Name InvoiceAddress; matching on Name alone would " +
            "also attach Geo's columns to BillTo");
    }

    [Fact]
    public void MirrorEf_DisplayName_QualifiesCollidingBoxesAndRelationshipsReferenceSameLabels()
    {
        var order = new EfEntity { Name = "Order", TableName = "Orders" };
        order.Properties.Add(new EfProperty { Name = "Id", Type = "int", IsPrimaryKey = true, IsValueType = true });

        var shipTo = new EfEntity
        {
            Name = "Address",
            Key = "Order.ShipTo",
            IsOwned = true,
            OwnerEntity = "Order",
            NavigationName = "ShipTo",
            TableName = "ShipToAddresses"
        };
        shipTo.Properties.Add(new EfProperty { Name = "City", Type = "string" });

        var billTo = new EfEntity
        {
            Name = "Address",
            Key = "Order.BillTo",
            IsOwned = true,
            OwnerEntity = "Order",
            NavigationName = "BillTo",
            TableName = "BillToAddresses"
        };
        billTo.Properties.Add(new EfProperty { Name = "City", Type = "string" });

        var model = new EfModel { ContextName = "Ctx" };
        model.Entities.Add(order);
        model.Entities.Add(shipTo);
        model.Entities.Add(billTo);

        var output = Render(model);

        output.Should().Contain("Order_ShipTo {", "both owned boxes share the Name Address, so both qualify");
        output.Should().Contain("Order_BillTo {");
        output.Should().NotContain("Address {", "the bare, unqualified name must not appear as a box header");

        // The relationship lines must reference the exact same qualified labels as the box headers —
        // a mismatch would emit a Mermaid relationship pointing at a box that does not exist.
        output.Should().Contain("Order ||--|| Order_ShipTo");
        output.Should().Contain("Order ||--|| Order_BillTo");
    }

    [Fact]
    public void Classic_TableSplitOwnsOne_RendersBoxAndIdentifyingRelationship()
    {
        var model = ModelWithOwned("Orders", isCollection: false);

        var output = new MermaidErdRenderer().Render(
            model, new DiagramOptions(false, false, false, ErdOwnedMode.Classic));

        output.Should().Contain("Address {", "classic mode always gives an owned type its own box");
        output.Should().Contain("string ZipCode", "classic mode never prefixes columns");
        output.Should().Contain("Order ||--|| Address");
        output.Should().NotContain("ShipToAddress_ZipCode");
    }

    [Fact]
    public void Classic_OwnerDoesNotGainNavigationColumn()
    {
        var model = ModelWithOwned("Orders", isCollection: false);

        var output = new MermaidErdRenderer().Render(
            model, new DiagramOptions(false, false, false, ErdOwnedMode.Classic));

        var orderBlock = output.Split("Address {")[0];
        orderBlock.Should().NotContain("ShipToAddress",
            "the relationship line carries the navigation; the owner gets no reference column");
    }

    /// <summary>
    /// Regression test for a cycle-guard reset: <c>IsInlined</c> used to re-enter
    /// <c>HasEffectiveProperties</c> with a fresh <c>visited</c> set instead of threading the caller's set
    /// through, so a zero-property SELF-owning owned entity (its own <c>OwnerEntity</c> equal to its own
    /// <c>EffectiveKey</c>) recursed forever between the two methods and raised an uncatchable
    /// <see cref="StackOverflowException"/>, aborting the whole test run. With the guard shared correctly,
    /// the entity cannot resolve to a table-split inline (it never gains a column from itself) and must
    /// surface as its own — empty — box instead.
    /// </summary>
    [Fact]
    public void MirrorEf_ZeroPropertySelfOwningEntity_RendersOwnBoxWithoutStackOverflow()
    {
        var self = new EfEntity
        {
            Name = "Self",
            IsOwned = true,
            OwnerEntity = "Self", // EffectiveKey falls back to Name when Key is empty, so this is self-owning.
            NavigationName = "SelfNav",
            TableName = "Selves"
        };

        var model = new EfModel { ContextName = "Ctx" };
        model.Entities.Add(self);

        var output = Render(model);

        output.Should().Contain("Self {", "a zero-property self-owning entity must still surface as its own box");
        output.Should().Contain("Self ||--|| Self", "the derived self-referencing relationship still renders");
    }

    /// <summary>
    /// Regression test for the same cycle-guard reset (see
    /// <see cref="MirrorEf_ZeroPropertySelfOwningEntity_RendersOwnBoxWithoutStackOverflow"/>), but for a
    /// zero-property MUTUALLY-owning pair (A owns B, B owns A). Pre-fix this also recursed forever between
    /// <c>IsInlined</c> and <c>HasEffectiveProperties</c> and aborted the test run with a
    /// <see cref="StackOverflowException"/>.
    /// </summary>
    [Fact]
    public void MirrorEf_ZeroPropertyMutuallyOwningPair_RendersBothBoxesWithoutStackOverflow()
    {
        var a = new EfEntity { Name = "A", IsOwned = true, OwnerEntity = "B", NavigationName = "BNav" };
        var b = new EfEntity { Name = "B", IsOwned = true, OwnerEntity = "A", NavigationName = "ANav" };

        var model = new EfModel { ContextName = "Ctx" };
        model.Entities.Add(a);
        model.Entities.Add(b);

        var output = Render(model);

        output.Should().Contain("A {", "a zero-property, mutually-owning entity must still surface as its own box");
        output.Should().Contain("B {", "a zero-property, mutually-owning entity must still surface as its own box");
    }
}
