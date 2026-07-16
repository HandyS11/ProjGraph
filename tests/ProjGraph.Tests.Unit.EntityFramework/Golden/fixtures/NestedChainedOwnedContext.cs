using Microsoft.EntityFrameworkCore;
namespace Fixtures;

// Regression fixture for nested CHAINED ownership (no builder lambda at either level):
// Entity<T>().OwnsOne(a).OwnsOne(b).Property(...). FindConfigRoots' pre-order DescendantNodes()
// traversal yields the syntactically outermost .OwnsOne(b) before the nested .OwnsOne(a), so the
// walker must process owned-type roots owner-before-owned or the inner ShipTo entity (and its
// chained Geo config) silently vanishes.
public class NestedChainedOwnedContext : DbContext
{
    public DbSet<Invoice> Invoices { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>()
            .OwnsOne(i => i.ShipTo)
            .OwnsOne(a => a.Geo)
            .Property(g => g.Latitude);
    }
}

public class Invoice
{
    public int Id { get; set; }
    public ShipToAddress ShipTo { get; set; } = null!;
}

public class ShipToAddress
{
    public string City { get; set; } = "";
    public GeoTag Geo { get; set; } = null!;
}

public class GeoTag
{
    public double Latitude { get; set; }
}
