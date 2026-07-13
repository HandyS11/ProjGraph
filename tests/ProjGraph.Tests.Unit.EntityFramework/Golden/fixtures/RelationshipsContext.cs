using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
namespace Fixtures;

public class RelationshipsContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; } = null!;
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<Author> Authors { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>()
            .HasOne(p => p.Blog).WithMany(b => b.Posts)
            .HasForeignKey(p => p.BlogId).IsRequired(false);

        modelBuilder.Entity<Blog>()
            .HasOne(b => b.Owner).WithMany().HasForeignKey(b => b.OwnerId);

        modelBuilder.Entity<Post>().HasMany(p => p.Authors).WithMany(a => a.Posts);
    }
}

public class Blog { public int Id { get; set; } public int? OwnerId { get; set; } public Author Owner { get; set; } = null!; public List<Post> Posts { get; set; } = []; }
public class Post { public int Id { get; set; } public int? BlogId { get; set; } public Blog Blog { get; set; } = null!; public List<Author> Authors { get; set; } = []; }
public class Author { public int Id { get; set; } public List<Post> Posts { get; set; } = []; }
