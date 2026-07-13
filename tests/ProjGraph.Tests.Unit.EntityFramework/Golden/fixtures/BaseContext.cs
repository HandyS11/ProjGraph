using Microsoft.EntityFrameworkCore;
namespace Fixtures;

public abstract class BaseDbContext : DbContext
{
    public DbSet<Note> Notes { get; set; } = null!;
}

public class BaseContext : BaseDbContext
{
    public DbSet<Tag> Tags { get; set; } = null!;
}

public class Note { public int Id { get; set; } public string Text { get; set; } = ""; }
public class Tag { public int Id { get; set; } public string Label { get; set; } = ""; }
