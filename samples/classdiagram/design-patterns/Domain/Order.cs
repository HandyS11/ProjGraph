using DesignPatterns.Base;
using DesignPatterns.Enums;

namespace DesignPatterns.Domain;

/// <summary>
/// Order entity with items and payment
/// </summary>
public class Order : AuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal TotalAmount { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public string BillingAddress { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }

    // Navigation properties
    public List<OrderItem> Items { get; set; } = [];
    public Payment? PaymentInfo { get; set; }
}