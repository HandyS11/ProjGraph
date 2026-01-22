using DesignPatterns.Base;

namespace DesignPatterns.Domain;

/// <summary>
/// Product category entity
/// </summary>
public class Category : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }

    // Navigation properties
    public List<Category> SubCategories { get; set; } = [];
    public List<Product> Products { get; set; } = [];
}