using Microsoft.EntityFrameworkCore;
namespace Fixtures;

// Regression fixture for FluentOwnedTypeWalker.ResolveOwnedType's snapshot-form detection: it must key
// off the FIRST ARGUMENT itself being a string literal, not merely the presence of a string literal
// anywhere in the argument list. This call's first argument is a navigation lambda (the DbContext form),
// with an unrelated string literal ("AddressTable") at argument position 1 — a shape that does not
// correspond to any real EF Core OwnsOne overload, but exercises the walker's argument-position
// discipline defensively: an overly loose "any literal + Count >= 2" check would misread "AddressTable"
// as the owned type's name, fail symbol resolution, and silently degrade to a bare (unseeded) entity.
public class OwnsOneArgOrderContext : DbContext
{
    public DbSet<Gizmo> Gizmos { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Gizmo>()
            .OwnsOne(g => g.Address, "AddressTable", b => b.Property(p => p.City).HasMaxLength(50));
    }
}

public class Gizmo
{
    public int Id { get; set; }
    public GizmoAddress Address { get; set; } = null!;
}

public class GizmoAddress
{
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
}
