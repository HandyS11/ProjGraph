using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
namespace Fixtures;

// Golden fixture covering every owned-type shape in one model, rendered in BOTH ERD modes:
//   ShipTo   - OwnsOne, no ToTable  -> table-split, inlines in MirrorEf
//   BillTo   - OwnsOne + ToTable    -> own table, a box in both modes
//   Lines    - OwnsMany             -> own table, a box in both modes
//   ShipTo.Geo - OwnsOne nested in an owned builder -> compounding prefixes
//
// Type names are prefixed OwnedModes* to avoid a real CS0101 collision: DualNavOwnedContext.cs
// already declares `Invoice` and `InvoiceAddress` in this same `namespace Fixtures`, and
// EntityFileDiscovery scans the whole Golden/fixtures directory for every golden context, so two
// fixtures declaring the same type name here silently merge into one corrupted symbol.
public class OwnedModesContext : DbContext
{
    public DbSet<OwnedModesInvoice> Invoices { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OwnedModesInvoice>(e =>
        {
            e.OwnsOne(i => i.ShipTo, a =>
            {
                a.Property(p => p.Street).IsRequired().HasMaxLength(180);
                a.Property(p => p.ZipCode).HasMaxLength(18);
                a.OwnsOne(p => p.Geo, g => g.Property(x => x.Latitude).HasPrecision(9, 6));
            });

            e.OwnsOne(i => i.BillTo, a =>
            {
                a.Property(p => p.Street).HasMaxLength(180);
                a.ToTable("BillingAddresses");
            });

            e.OwnsMany(i => i.Lines, l =>
            {
                l.Property(p => p.Description).IsRequired().HasMaxLength(240);
                l.Property(p => p.Amount).HasPrecision(18, 2);
            });
        });
    }
}

public class OwnedModesInvoice
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public OwnedModesAddress ShipTo { get; set; } = null!;
    public OwnedModesAddress BillTo { get; set; } = null!;
    public List<InvoiceLine> Lines { get; set; } = [];
}

public class OwnedModesAddress
{
    public string Street { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public GeoPoint Geo { get; set; } = null!;
}

public class GeoPoint
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

public class InvoiceLine
{
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}
