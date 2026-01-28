using DesignPatterns.Interfaces;

namespace DesignPatterns.Services;

/// <summary>
/// Pricing service with configurable strategy
/// </summary>
public class PricingService
{
    private IPricingStrategy _strategy;

    public PricingService(IPricingStrategy strategy)
    {
        _strategy = strategy;
    }

    public void SetStrategy(IPricingStrategy strategy)
    {
        _strategy = strategy;
    }

    public decimal CalculateItemPrice(decimal basePrice, int quantity)
    {
        return _strategy.CalculatePrice(basePrice, quantity);
    }

    public decimal CalculateFinalPrice(decimal basePrice, int quantity, decimal discountPercentage, decimal taxRate)
    {
        var price = _strategy.CalculatePrice(basePrice, quantity);

        if (discountPercentage > 0)
        {
            price = _strategy.ApplyDiscount(price, discountPercentage);
        }

        var tax = _strategy.CalculateTax(price, taxRate);
        return price + tax;
    }
}