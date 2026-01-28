namespace DesignPatterns.Interfaces;

/// <summary>
/// Strategy pattern interface for pricing calculation
/// </summary>
public interface IPricingStrategy
{
    decimal CalculatePrice(decimal basePrice, int quantity);
    decimal ApplyDiscount(decimal price, decimal discountPercentage);
    decimal CalculateTax(decimal price, decimal taxRate);
}