using DesignPatterns.Domain;
using DesignPatterns.Interfaces;

namespace DesignPatterns.Validators;

/// <summary>
/// Order validation rules
/// </summary>
public class OrderValidator : IValidator<Order>
{
    public bool Validate(Order entity)
    {
        return GetValidationErrors(entity).Count() == 0;
    }

    public IEnumerable<string> GetValidationErrors(Order entity)
    {
        var errors = new List<string>();

        if (entity.UserId <= 0)
        {
            errors.Add("Valid user ID is required");
        }

        if (entity.Items == null || entity.Items.Count == 0)
        {
            errors.Add("Order must contain at least one item");
        }
        else
        {
            foreach (var item in entity.Items)
            {
                if (item.Quantity <= 0)
                {
                    errors.Add($"Item quantity must be positive");
                }

                if (item.UnitPrice <= 0)
                {
                    errors.Add($"Item unit price must be positive");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(entity.ShippingAddress))
        {
            errors.Add("Shipping address is required");
        }

        if (string.IsNullOrWhiteSpace(entity.BillingAddress))
        {
            errors.Add("Billing address is required");
        }

        if (entity.TotalAmount <= 0)
        {
            errors.Add("Total amount must be positive");
        }

        return errors;
    }
}
