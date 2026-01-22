using DesignPatterns.Base;
using DesignPatterns.Enums;

namespace DesignPatterns.Domain;

/// <summary>
/// Payment information entity
/// </summary>
public class Payment : Entity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CardLastFourDigits { get; set; }
}