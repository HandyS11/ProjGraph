using DesignPatterns.Builders;
using DesignPatterns.Domain;
using DesignPatterns.Enums;
using DesignPatterns.Interfaces;
using DesignPatterns.Repositories;
using DesignPatterns.Services;
using DesignPatterns.Strategies;
using DesignPatterns.Validators;

namespace DesignPatterns.Examples;

/// <summary>
/// Example usage demonstrating all design patterns
/// </summary>
public static class UsageExample
{
    public static async Task RunExampleAsync()
    {
        // Setup dependencies
        var unitOfWork = new UnitOfWork();
        var notificationService = new NotificationService();

        // Create repositories
        var userRepository = unitOfWork.GetRepository<User>();
        var orderRepository = unitOfWork.GetRepository<Order>();
        var productRepository = unitOfWork.GetRepository<Product>();

        // Create validators
        var userValidator = new UserValidator();
        var orderValidator = new OrderValidator();

        // Create pricing strategy (can be swapped at runtime)
        IPricingStrategy pricingStrategy = new DiscountPricing(5, 10);

        // Create services
        var userService = new UserService(userRepository, notificationService, userValidator);
        var orderService = new OrderService(
            orderRepository,
            userRepository,
            productRepository,
            notificationService,
            orderValidator,
            pricingStrategy
        );

        // Create payment strategy (Strategy Pattern)
        IPaymentStrategy paymentStrategy = new CreditCardPayment();
        var paymentProcessor = new PaymentProcessor(paymentStrategy, notificationService);

        // 1. Create a user
        var user = new User
        {
            Id = 1,
            Username = "johndoe",
            Email = "john@example.com",
            FirstName = "John",
            LastName = "Doe",
            PasswordHash = "hashed_password",
            Role = UserRole.Customer,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };

        await userService.CreateUserAsync(user);

        // 2. Create products
        var product1 = new Product
        {
            Id = 1,
            Name = "Laptop",
            Description = "High-performance laptop",
            Sku = "LAP-001",
            Price = 999.99m,
            StockQuantity = 50,
            Weight = 2.5m,
            CategoryId = 1
        };

        var product2 = new Product
        {
            Id = 2,
            Name = "Mouse",
            Description = "Wireless mouse",
            Sku = "MOU-001",
            Price = 29.99m,
            StockQuantity = 200,
            Weight = 0.1m,
            CategoryId = 1
        };

        await productRepository.AddAsync(product1);
        await productRepository.AddAsync(product2);

        // 3. Build an order using Builder Pattern
        var order = OrderBuilder.Create()
            .ForUser(user.Id)
            .WithShippingAddress("123 Main St, City, State 12345")
            .WithBillingAddress("123 Main St, City, State 12345")
            .AddItem(product1, 1)
            .AddItem(product2, 6) // Volume discount will apply
            .WithShippingCost(15.00m)
            .WithPayment(PaymentMethod.CreditCard, "TXN-12345")
            .Build();

        // 4. Create the order
        await orderService.CreateOrderAsync(order);

        // 5. Process payment
        await paymentProcessor.ProcessPaymentAsync(
            order.TotalAmount,
            "4111111111111111", // Test credit card
            user.Email
        );

        // 6. Update order status
        await orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Processing);

        // 7. Switch pricing strategy at runtime (Strategy Pattern)
        var standardPricing = new StandardPricing();
        var pricingService = new PricingService(standardPricing);
        var price = pricingService.CalculateFinalPrice(100m, 5, 10m, 8.5m);

        // 8. Switch payment strategy
        var paypalProcessor = new PaymentProcessor(
            new PayPalPayment(),
            notificationService
        );

        Console.WriteLine($"Order created: {order.OrderNumber}");
        Console.WriteLine($"Total amount: ${order.TotalAmount:F2}");
        Console.WriteLine("Design patterns demonstrated:");
        Console.WriteLine("  ✓ Repository Pattern");
        Console.WriteLine("  ✓ Unit of Work Pattern");
        Console.WriteLine("  ✓ Strategy Pattern (Payment & Pricing)");
        Console.WriteLine("  ✓ Builder Pattern (Order)");
        Console.WriteLine("  ✓ Observer Pattern (Notifications)");
        Console.WriteLine("  ✓ Validator Pattern");
        Console.WriteLine("  ✓ Service Layer Pattern");
    }
}
