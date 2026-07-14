using Microsoft.EntityFrameworkCore;
namespace Fixtures;

public class AuditContext : AuditDbContextBase
{
    public DbSet<Report> Reports { get; set; } = null!;
}

public class Report { public int Id { get; set; } public string Title { get; set; } = ""; }
