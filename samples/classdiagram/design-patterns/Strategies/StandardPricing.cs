using DesignPatterns.Interfaces;

namespace DesignPatterns.Strategies;

/// <summary>
/// Standard pricing strategy without discounts
/// </summary>
public class StandardPricing : IPricingStrategy
{
    public decimal CalculatePrice(decimal basePrice, int quantity)
    {
        return basePrice * quantity;
    }

    public decimal ApplyDiscount(decimal price, decimal discountPercentage)
    {
        return price * (1 - (discountPercentage / 100));
    }

    public decimal CalculateTax(decimal price, decimal taxRate)
    {
        return price * (taxRate / 100);
    }
}