using Microsoft.EntityFrameworkCore;
namespace Fixtures;

// Regression fixture: two navigation properties on the same owner share one CLR type
// (Invoice.ShipTo and Invoice.BillTo are both InvoiceAddress). EfEntity.Key — {Owner}.{Nav} — is the
// model's identity, not the CLR Name, so both owned entities must survive analysis, including
// DeduplicateModelContent's Name-collision guard.
public class DualNavOwnedContext : DbContext
{
    public DbSet<Invoice> Invoices { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(e =>
        {
            e.OwnsOne(i => i.ShipTo);
            e.OwnsOne(i => i.BillTo);
        });
    }
}

public class Invoice
{
    public int Id { get; set; }
    public InvoiceAddress ShipTo { get; set; } = null!;
    public InvoiceAddress BillTo { get; set; } = null!;
}

public class InvoiceAddress
{
    public string City { get; set; } = "";
}
