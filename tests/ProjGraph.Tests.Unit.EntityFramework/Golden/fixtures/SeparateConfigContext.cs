using Microsoft.EntityFrameworkCore;
namespace Fixtures;

public class SeparateConfigContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfiguration(new GadgetConfiguration());
}

public class Gadget { public int Id { get; set; } public string Label { get; set; } = ""; }
