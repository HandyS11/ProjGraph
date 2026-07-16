using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fixtures;

// Regression fixture: an owned type captured directly in OnModelCreating (the context path), whose
// OWNER's ToTable is applied later by a SEPARATE IEntityTypeConfiguration<T> class rather than inline
// in the same OnModelCreating body. FluentOwnedTypeWalker.ResolveTables must run only ONCE, after
// EntityConfigurationWalker.Apply has folded MerchantTableConfig's ToTable in - resolving it any
// earlier (before the config class is folded in) permanently freezes HeadOffice on Merchant's stale,
// pre-ToTable default, since ResolveTables is idempotent-by-skip and a later pass cannot correct it.
public class MerchantSplitContext : DbContext
{
    public DbSet<Merchant> Merchants { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Merchant>().OwnsOne(m => m.HeadOffice);
        modelBuilder.ApplyConfiguration(new MerchantTableConfig());
    }
}

public class MerchantTableConfig : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
        => builder.ToTable("Merchants2");
}

public class Merchant
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public MerchantOffice HeadOffice { get; set; } = null!;
}

public class MerchantOffice
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
}
