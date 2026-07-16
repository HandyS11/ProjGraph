using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fixtures;

// Golden fixture for the snapshot path's owned-type shape: OwnsOne("Type", "nav", b1 => {...}) with the
// explicit ToTable, WithOwner/HasForeignKey and shadow key that `dotnet ef` always generates.
// ShipToAddress maps to the owner's table (table-splitting); Notes maps to its own (OwnsMany).
[DbContext(typeof(BillingContext))]
public class BillingContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("Fixtures.Receipt", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd();
            b.Property<string>("Reference").IsRequired().HasMaxLength(64);
            b.HasKey("Id");
            b.ToTable("Receipts");

            b.OwnsOne("Fixtures.ReceiptAddress", "ShipToAddress", b1 =>
            {
                b1.Property<int>("ReceiptId");
                b1.Property<string>("City").IsRequired().HasMaxLength(100);
                b1.Property<string>("ZipCode").HasMaxLength(18);
                b1.HasKey("ReceiptId");
                b1.ToTable("Receipts");
                b1.WithOwner().HasForeignKey("ReceiptId");
            });

            b.OwnsMany("Fixtures.ReceiptNote", "Notes", b1 =>
            {
                b1.Property<int>("Id").ValueGeneratedOnAdd();
                b1.Property<int>("ReceiptId");
                b1.Property<string>("Text").HasMaxLength(500);
                b1.HasKey("Id");
                b1.ToTable("ReceiptNotes");
                b1.WithOwner().HasForeignKey("ReceiptId");
            });
        });
    }
}
