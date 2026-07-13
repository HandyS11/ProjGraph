using Microsoft.EntityFrameworkCore;
namespace Fixtures;

public class PropertyConfigContext : DbContext
{
    public DbSet<Account> Accounts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Name).IsRequired().HasMaxLength(200);
            e.Property(a => a.Balance).HasPrecision(18, 2).HasDefaultValue(0);
            e.Property(a => a.Code).HasColumnType("char(8)");
        });
}

public class Account { public int Id { get; set; } public string Name { get; set; } = ""; public decimal Balance { get; set; } public string Code { get; set; } = ""; }
