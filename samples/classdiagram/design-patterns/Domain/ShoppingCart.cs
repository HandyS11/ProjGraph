using DesignPatterns.Base;

namespace DesignPatterns.Domain;

/// <summary>
/// Shopping cart aggregate root
/// </summary>
public class ShoppingCart : Entity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }

    // Aggregated products
    public List<ShoppingCartItem> Items { get; set; } = new();

    public decimal GetTotalAmount()
    {
        return Items.Sum(item => item.Quantity * item.UnitPrice);
    }

    public void AddItem(Product product, int quantity)
    {
        var existingItem = Items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            Items.Add(new ShoppingCartItem
            {
                ProductId = product.Id, Product = product, Quantity = quantity, UnitPrice = product.Price
            });
        }

        LastModifiedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Shopping cart item
/// </summary>
public class ShoppingCartItem : Entity
{
    public int ShoppingCartId { get; set; }
    public ShoppingCart? ShoppingCart { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}