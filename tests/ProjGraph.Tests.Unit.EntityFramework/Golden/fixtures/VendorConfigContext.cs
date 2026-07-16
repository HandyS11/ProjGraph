using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fixtures;

// Golden fixture for owned types configured inside an IEntityTypeConfiguration<T>.Configure body
// (the ApplyConfiguration / IEntityTypeConfiguration<T> path), covering both owned-type shapes:
//   HeadOffice - OwnsOne, no ToTable        -> table-split, inlines onto Vendor as Vendor_HeadOffice
//   Warehouse  - OwnsOne + chained ToTable  -> own table, a box with an identifying relationship
//
// Regression coverage for the gap where EntityConfigurationWalker never ran FluentOwnedTypeWalker,
// silently dropping owned types configured in a separate IEntityTypeConfiguration<T> class - exactly
// the shape of eShopOnWeb's Order.OwnsOne(o => o.ShipToAddress, ...), this feature's motivating case.
// The chained-ToTable form for Warehouse also exercises the two-pass FluentEntityWalker ordering: the
// ToTable call is only resolvable once FluentOwnedTypeWalker.Apply has materialized Vendor.Warehouse.
public class VendorConfigContext : DbContext
{
    public DbSet<Vendor> Vendors { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfiguration(new VendorConfiguration());
}

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).IsRequired().HasMaxLength(120);

        builder.OwnsOne(v => v.HeadOffice, a =>
        {
            a.Property(p => p.Street).IsRequired().HasMaxLength(180);
            a.Property(p => p.City).HasMaxLength(80);
        });

        builder.OwnsOne(v => v.Warehouse).Property(w => w.Code).HasMaxLength(20);
        builder.OwnsOne(v => v.Warehouse).ToTable("VendorWarehouses");
    }
}

public class Vendor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public VendorAddress HeadOffice { get; set; } = null!;
    public VendorWarehouse Warehouse { get; set; } = null!;
}

public class VendorAddress
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
}

public class VendorWarehouse
{
    public string Code { get; set; } = "";
}
