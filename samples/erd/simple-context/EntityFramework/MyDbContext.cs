using Microsoft.EntityFrameworkCore;

namespace EntityFramework;

public class MyDbContext : DbContext
{
    public DbSet<Author> Authors { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Publisher> Publishers { get; set; }
    public DbSet<Review> Reviews { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Author configuration
        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Bio).HasMaxLength(1000);
        });

        // Publisher configuration
        modelBuilder.Entity<Publisher>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Country).HasMaxLength(100);
        });

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Book configuration
        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(300);
            entity.Property(e => e.ISBN).HasMaxLength(13);

            // One-to-Many: Publisher -> Books
            entity.HasOne(e => e.Publisher)
                .WithMany(p => p.Books)
                .HasForeignKey(e => e.PublisherId)
                .OnDelete(DeleteBehavior.Restrict);

            // Many-to-Many: Books <-> Authors
            entity.HasMany(e => e.Authors)
                .WithMany(a => a.Books)
                .UsingEntity<Dictionary<string, object>>(
                    "BookAuthors",
                    j => j.HasOne<Author>().WithMany().HasForeignKey("AuthorId"),
                    j => j.HasOne<Book>().WithMany().HasForeignKey("BookId"));

            // Many-to-Many: Books <-> Categories
            entity.HasMany(e => e.Categories)
                .WithMany(c => c.Books)
                .UsingEntity<Dictionary<string, object>>(
                    "BookCategories",
                    j => j.HasOne<Category>().WithMany().HasForeignKey("CategoryId"),
                    j => j.HasOne<Book>().WithMany().HasForeignKey("BookId"));
        });

        // Review configuration
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Rating).IsRequired();
            entity.Property(e => e.Comment).HasMaxLength(2000);

            // One-to-Many: Book -> Reviews
            entity.HasOne(e => e.Book)
                .WithMany(b => b.Reviews)
                .HasForeignKey(e => e.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public DateTime? BirthDate { get; set; }

    public ICollection<Book> Books { get; set; } = [];
}

public class Publisher
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public DateTime? FoundedDate { get; set; }

    public ICollection<Book> Books { get; set; } = [];
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Book> Books { get; set; } = [];
}

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ISBN { get; set; }
    public DateTime PublishedDate { get; set; }
    public int PageCount { get; set; }

    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;

    public ICollection<Author> Authors { get; set; } = [];
    public ICollection<Category> Categories { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
}

public class Review
{
    public int Id { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime ReviewDate { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
}