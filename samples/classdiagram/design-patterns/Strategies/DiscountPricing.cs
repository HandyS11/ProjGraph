using DesignPatterns.Interfaces;

namespace DesignPatterns.Strategies;

/// <summary>
/// Discount pricing strategy with volume discounts
/// </summary>
public class DiscountPricing : IPricingStrategy
{
    private readonly decimal _volumeDiscountThreshold;
    private readonly decimal _volumeDiscountPercentage;

    public DiscountPricing(decimal volumeDiscountThreshold = 10, decimal volumeDiscountPercentage = 10)
    {
        _volumeDiscountThreshold = volumeDiscountThreshold;
        _volumeDiscountPercentage = volumeDiscountPercentage;
    }

    public decimal CalculatePrice(decimal basePrice, int quantity)
    {
        var total = basePrice * quantity;

        // Apply volume discount if threshold is met
        if (quantity >= _volumeDiscountThreshold)
        {
            total = ApplyDiscount(total, _volumeDiscountPercentage);
        }

        return total;
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