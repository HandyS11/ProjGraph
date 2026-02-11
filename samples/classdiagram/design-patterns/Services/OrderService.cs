using DesignPatterns.Domain;
using DesignPatterns.Enums;
using DesignPatterns.Interfaces;

namespace DesignPatterns.Services;

/// <summary>
/// Order service with business logic
/// </summary>
public class OrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly INotificationService _notificationService;
    private readonly IValidator<Order> _validator;
    private readonly IPricingStrategy _pricingStrategy;

    public OrderService(
        IRepository<Order> orderRepository,
        IRepository<User> userRepository,
        IRepository<Product> productRepository,
        INotificationService notificationService,
        IValidator<Order> validator,
        IPricingStrategy pricingStrategy)
    {
        _orderRepository = orderRepository;
        _userRepository = userRepository;
        _productRepository = productRepository;
        _notificationService = notificationService;
        _validator = validator;
        _pricingStrategy = pricingStrategy;
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        if (!_validator.Validate(order))
        {
            throw new InvalidOperationException("Order validation failed");
        }

        // Calculate totals using pricing strategy
        order.SubTotal = order.Items.Sum(item =>
            _pricingStrategy.CalculatePrice(item.UnitPrice, item.Quantity));
        order.TaxAmount = _pricingStrategy.CalculateTax(order.SubTotal, 8.5m);
        order.TotalAmount = order.SubTotal + order.TaxAmount + order.ShippingCost;

        order.OrderDate = DateTime.UtcNow;
        order.OrderNumber = GenerateOrderNumber();
        order.Status = OrderStatus.Pending;

        var createdOrder = await _orderRepository.AddAsync(order);

        // Notify user
        var user = await _userRepository.GetByIdAsync(order.UserId);
        if (user != null)
        {
            await _notificationService.SendEmailAsync(
                user.Email,
                "Order Confirmation",
                $"Your order {order.OrderNumber} has been received."
            );
        }

        return createdOrder;
    }

    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        return await _orderRepository.GetByIdAsync(id);
    }

    public async Task UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            throw new InvalidOperationException("Order not found");
        }

        order.Status = newStatus;
        await _orderRepository.UpdateAsync(order);

        // Notify user of status change
        var user = await _userRepository.GetByIdAsync(order.UserId);
        if (user != null)
        {
            await _notificationService.NotifyAsync(
                user.Id.ToString(),
                $"Your order {order.OrderNumber} is now {newStatus}"
            );
        }
    }

    private string GenerateOrderNumber()
    {
        return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }
}
