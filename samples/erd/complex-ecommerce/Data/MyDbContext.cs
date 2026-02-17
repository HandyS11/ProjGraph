using ComplexEcommerce.Domain;
using Microsoft.EntityFrameworkCore;

namespace ComplexEcommerce.Data;

public class MyDbContext(DbContextOptions<MyDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<DigitalProduct> DigitalProducts => Set<DigitalProduct>();
    public DbSet<PhysicalProduct> PhysicalProducts => Set<PhysicalProduct>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CreditCardPayment> CreditCardPayments => Set<CreditCardPayment>();
    public DbSet<PayPalPayment> PayPalPayments => Set<PayPalPayment>();
    public DbSet<ShoppingCart> ShoppingCarts => Set<ShoppingCart>();
    public DbSet<ShoppingCartItem> ShoppingCartItems => Set<ShoppingCartItem>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1. TPH (Table Per Hierarchy) for products
        modelBuilder.Entity<Product>()
            .HasDiscriminator<string>("ProductType")
            .HasValue<DigitalProduct>("digital")
            .HasValue<PhysicalProduct>("physical");
            
        modelBuilder.Entity<Product>().OwnsOne(p => p.Price);
            
        // 2. Recursive Relationships
        modelBuilder.Entity<Category>()
            .HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.NoAction);
            
        // 3. Many-to-Many: Product <-> Supplier
        modelBuilder.Entity<ProductSupplier>()
            .HasKey(ps => new { ps.ProductId, ps.SupplierId });
            
        modelBuilder.Entity<ProductSupplier>()
            .HasOne(ps => ps.Product)
            .WithMany(p => p.ProductSuppliers)
            .HasForeignKey(ps => ps.ProductId);
            
        modelBuilder.Entity<ProductSupplier>()
            .HasOne(ps => ps.Supplier)
            .WithMany(s => s.ProductSuppliers)
            .HasForeignKey(ps => ps.SupplierId);
            
        // 4. One-to-One Relationships
        modelBuilder.Entity<Customer>()
            .HasOne(c => c.ShoppingCart)
            .WithOne(s => s.Customer)
            .HasForeignKey<ShoppingCart>(s => s.CustomerId);
            
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Payment)
            .WithOne(p => p.Order)
            .HasForeignKey<Payment>(p => p.OrderId);
            
        // 5. Payment TPH
        modelBuilder.Entity<Payment>()
            .HasDiscriminator<string>("PaymentType")
            .HasValue<CreditCardPayment>("credit-card")
            .HasValue<PayPalPayment>("paypal");
            
        modelBuilder.Entity<Payment>().OwnsOne(p => p.Amount);
        modelBuilder.Entity<OrderItem>().OwnsOne(o => o.UnitPrice);
            
        // 6. BaseEntity metadata via shadow properties (optional, but requested in data-model.md)
        // Actually BaseEntity has them as concrete properties. I'll just use them.
    }
}
