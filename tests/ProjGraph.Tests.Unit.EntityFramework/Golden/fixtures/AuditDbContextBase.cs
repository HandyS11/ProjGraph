using Microsoft.EntityFrameworkCore;
namespace Fixtures;

public abstract class AuditDbContextBase : DbContext
{
    public DbSet<AuditEntry> AuditEntries { get; set; } = null!;
}

public class AuditEntry { public int Id { get; set; } public string Action { get; set; } = ""; }
