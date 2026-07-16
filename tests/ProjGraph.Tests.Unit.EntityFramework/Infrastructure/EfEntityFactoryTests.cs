using ProjGraph.Core.Models;
using ProjGraph.Lib.EntityFramework.Infrastructure;

namespace ProjGraph.Tests.Unit.EntityFramework.Infrastructure;

public sealed class EfEntityFactoryTests
{
    [Fact]
    public void CopyWith_PreservesOwnedMetadata_WhenOverridingTableName()
    {
        var source = new EfEntity
        {
            Name = "Address",
            IsOwned = true,
            OwnerEntity = "Order",
            NavigationName = "ShipToAddress",
            IsCollection = false,
            TableName = "Orders"
        };
        source.Properties.Add(new EfProperty { Name = "City", Type = "string" });

        var copy = EfEntityFactory.CopyWith(source, "ShipToAddresses");

        copy.TableName.Should().Be("ShipToAddresses");
        copy.IsOwned.Should().BeTrue("owned metadata must survive a ToTable rewrite");
        copy.OwnerEntity.Should().Be("Order");
        copy.NavigationName.Should().Be("ShipToAddress");
        copy.IsCollection.Should().BeFalse();
        copy.Properties.Should().ContainSingle(p => p.Name == "City");
    }
}
