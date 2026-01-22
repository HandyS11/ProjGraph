using DesignPatterns.Domain;
using DesignPatterns.Enums;

namespace DesignPatterns.Builders;

/// <summary>
/// Fluent builder for creating Order objects
/// </summary>
public class OrderBuilder
{
    private readonly Order _order = new() { OrderDate = DateTime.UtcNow, Status = OrderStatus.Pending, Items = [] };

    public OrderBuilder ForUser(int userId)
    {
        _order.UserId = userId;
        return this;
    }

    public OrderBuilder WithOrderNumber(string orderNumber)
    {
        _order.OrderNumber = orderNumber;
        return this;
    }

    public OrderBuilder WithShippingAddress(string address)
    {
        _order.ShippingAddress = address;
        return this;
    }

    public OrderBuilder WithBillingAddress(string address)
    {
        _order.BillingAddress = address;
        return this;
    }

    public OrderBuilder AddItem(Product product, int quantity, decimal? customPrice = null)
    {
        var item = new OrderItem
        {
            ProductId = product.Id,
            Product = product,
            Quantity = quantity,
            UnitPrice = customPrice ?? product.Price,
            TotalPrice = (customPrice ?? product.Price) * quantity
        };
        _order.Items.Add(item);
        return this;
    }

    public OrderBuilder WithStatus(OrderStatus status)
    {
        _order.Status = status;
        return this;
    }

    public OrderBuilder WithShippingCost(decimal cost)
    {
        _order.ShippingCost = cost;
        return this;
    }

    public OrderBuilder WithDiscount(decimal discountPercentage)
    {
        var subtotal = _order.Items.Sum(i => i.TotalPrice);
        var discount = subtotal * (discountPercentage / 100);
        _order.SubTotal = subtotal - discount;
        return this;
    }

    public OrderBuilder WithPayment(PaymentMethod method, string transactionId)
    {
        _order.PaymentInfo = new Payment
        {
            Method = method,
            TransactionId = transactionId,
            Amount = _order.TotalAmount,
            PaymentDate = DateTime.UtcNow,
            IsSuccessful = true
        };
        return this;
    }

    public OrderBuilder WithTrackingNumber(string trackingNumber)
    {
        _order.TrackingNumber = trackingNumber;
        return this;
    }

    public Order Build()
    {
        // Calculate totals if not already set
        if (_order.SubTotal == 0)
        {
            _order.SubTotal = _order.Items.Sum(i => i.TotalPrice);
        }

        if (_order.TaxAmount == 0)
        {
            _order.TaxAmount = _order.SubTotal * 0.085m; // 8.5% tax
        }

        _order.TotalAmount = _order.SubTotal + _order.TaxAmount + _order.ShippingCost;

        // Generate order number if not set
        if (string.IsNullOrEmpty(_order.OrderNumber))
        {
            _order.OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        }

        return _order;
    }

    public static OrderBuilder Create()
    {
        return new OrderBuilder();
    }
}