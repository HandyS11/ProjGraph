using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Fixtures;

public class GadgetConfiguration : IEntityTypeConfiguration<Gadget>
{
    public void Configure(EntityTypeBuilder<Gadget> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Label).IsRequired().HasMaxLength(64);
    }
}
