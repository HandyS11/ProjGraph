using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fixtures;

// Fixture for owned types whose IEntityTypeConfiguration<T> builder declares the ownership keys
// explicitly (WithOwner().HasForeignKey / HasKey), the shape a hand-written config class shares with
// generated snapshots:
//   Sender    - table-split (no ToTable), string-form keys -> StripShadowKeys must drop the FK column
//               and clear the PK marker, exactly as on the snapshot path
//   Recipient - own table (ToTable), lambda-form HasForeignKey (the Expression overload on the generic
//               OwnershipBuilder<TEntity,TDependentEntity> returned by WithOwner()) -> the FK is a
//               real, separate column that must be kept and marked
public class OwnedFkConfigContext : DbContext
{
    public DbSet<Parcel> Parcels { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfiguration(new ParcelConfiguration());
}

public class ParcelConfiguration : IEntityTypeConfiguration<Parcel>
{
    public void Configure(EntityTypeBuilder<Parcel> builder)
    {
        builder.HasKey(p => p.Id);

        builder.OwnsOne(p => p.Sender, a =>
        {
            a.WithOwner().HasForeignKey("ParcelId");
            a.HasKey("ParcelId");
            a.Property(x => x.Street).HasMaxLength(120);
        });

        builder.OwnsOne(p => p.Recipient, a =>
        {
            a.ToTable("ParcelRecipients");
            a.WithOwner().HasForeignKey(x => x.ParcelId);
            a.Property(x => x.Street).HasMaxLength(120);
        });
    }
}

public class Parcel
{
    public int Id { get; set; }
    public ParcelEndpoint Sender { get; set; } = null!;
    public ParcelEndpoint Recipient { get; set; } = null!;
}

public class ParcelEndpoint
{
    public int ParcelId { get; set; }
    public string Street { get; set; } = "";
}
