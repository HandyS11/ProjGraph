using Microsoft.EntityFrameworkCore;
namespace Fixtures;

// One model, expressed twice: here as a DbContext, and in CrossPathSnapshot.cs as the snapshot EF would
// generate for it. The cross-path test asserts both render to the same ERD — the DbContext path infers
// table-splitting from the ABSENCE of ToTable while the snapshot path reads an explicit one, so this is
// what stops the two detections from drifting apart.
//
// SCOPE: this model is deliberately TABLE-SPLIT ONLY (Seat is OwnsOne with no ToTable) — that is the one
// owned-type shape where the two paths' inputs carry the same information and agreement is a real
// invariant. Do NOT extend this fixture with an own-table owned type (OwnsMany, or OwnsOne+ToTable)
// expecting the same agreement: a generated ModelSnapshot always declares EF's conventional shadow
// key/FK columns for an own-table owned type (see OwnedSnapshot.cs's ReceiptNote), while a hand-written
// DbContext's OnModelCreating never states them — there is nothing in that C# source to read. The two
// paths legitimately diverge there; it is information asymmetry, not a bug to fix.
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
