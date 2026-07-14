using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
namespace Fixtures;

// Golden fixture for the chained owned-type form (OwnsOne without a builder lambda) and an EF 8+
// primitive collection. The chained Property/ToTable calls configure the owned PostalAddress, so no
// City column may leak onto Shopper; the List<string> Tags must survive as a scalar column.
public class ChainedOwnedContext : DbContext
{
    public DbSet<Shopper> Shoppers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shopper>()
            .OwnsOne(c => c.Address)
            .Property(a => a.City).HasMaxLength(50);

        modelBuilder.Entity<Shopper>()
            .OwnsOne(c => c.Address)
            .ToTable("ShopperAddresses");

        modelBuilder.Entity<Shopper>()
            .Property(c => c.Name).IsRequired().HasMaxLength(120);
    }
}

public class Shopper
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public PostalAddress Address { get; set; } = null!;
}

public class PostalAddress
{
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
}
