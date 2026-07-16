using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fixtures;

// The snapshot EF would generate for CrossPathContext. Kept byte-for-byte equivalent in meaning, not form:
// the owned block carries the explicit ToTable("Tickets") + shadow key + WithOwner that `dotnet ef` emits.
[DbContext(typeof(CrossPathContext))]
public class CrossPathContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("Fixtures.Ticket", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd();
            b.Property<string>("Code").IsRequired().HasMaxLength(32);
            b.HasKey("Id");
            b.ToTable("Tickets");

            b.OwnsOne("Fixtures.SeatLocation", "Seat", b1 =>
            {
                b1.Property<int>("TicketId");
                b1.Property<string>("Row").IsRequired().HasMaxLength(4);
                b1.Property<int>("Number").IsRequired();
                b1.HasKey("TicketId");
                b1.ToTable("Tickets");
                b1.WithOwner().HasForeignKey("TicketId");
            });
        });
    }
}
