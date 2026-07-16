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
            Name = "ZipCode", Type = "string", IsExplicitlyRequired = true, IsRequired = true, MaxLength = 18
        });

        var model = new EfModel { ContextName = "Ctx" };
        model.Entities.Add(order);
        model.Entities.Add(address);
        return model;
    }

    private static string Render(EfModel model) =>
        new MermaidErdRenderer().Render(model, new DiagramOptions(false, false));

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
}
