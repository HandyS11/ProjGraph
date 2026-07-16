using Microsoft.EntityFrameworkCore;
namespace Fixtures;

// One model, expressed twice: here as a DbContext, and in CrossPathSnapshot.cs as the snapshot EF would
// generate for it. The cross-path test asserts both render to the same ERD — the DbContext path infers
// table-splitting from the ABSENCE of ToTable while the snapshot path reads an explicit one, so this is
// what stops the two detections from drifting apart.
public class CrossPathContext : DbContext
{
    public DbSet<Ticket> Tickets { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(e =>
        {
            e.ToTable("Tickets");
            e.Property(t => t.Code).IsRequired().HasMaxLength(32);
            e.OwnsOne(t => t.Seat, s =>
            {
                s.Property(p => p.Row).IsRequired().HasMaxLength(4);
                s.Property(p => p.Number).IsRequired();
            });
        });
    }
}

public class Ticket
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public SeatLocation Seat { get; set; } = null!;
}

public class SeatLocation
{
    public string Row { get; set; } = "";
    public int Number { get; set; }
}
