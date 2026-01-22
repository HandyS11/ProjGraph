using DesignPatterns.Base;

namespace DesignPatterns.Domain;

/// <summary>
/// Product entity with categories and inventory
/// </summary>
public class Product : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public decimal Weight { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public List<OrderItem> OrderItems { get; set; } = new();
}