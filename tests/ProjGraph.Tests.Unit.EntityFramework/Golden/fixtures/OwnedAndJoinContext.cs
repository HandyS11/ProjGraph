using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
namespace Fixtures;

public class OwnedAndJoinContext : DbContext
{
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.OwnsOne(c => c.Address, a => a.Property(p => p.City).HasMaxLength(50));
            e.HasMany(c => c.Products).WithMany(p => p.Customers)
                .UsingEntity<Dictionary<string, object>>("CustomerProduct",
                    j => j.HasOne<Product>().WithMany().HasForeignKey("ProductId"),
                    j => j.HasOne<Customer>().WithMany().HasForeignKey("CustomerId"));
        });
    }
}

public class Customer { public int Id { get; set; } public Address Address { get; set; } = null!; public List<Product> Products { get; set; } = []; }
public class Address { public string City { get; set; } = ""; }
public class Product { public int Id { get; set; } public List<Customer> Customers { get; set; } = []; }
