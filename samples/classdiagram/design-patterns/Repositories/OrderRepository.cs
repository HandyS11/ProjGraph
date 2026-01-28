using DesignPatterns.Domain;
using DesignPatterns.Enums;

namespace DesignPatterns.Repositories;

/// <summary>
/// Order-specific repository with custom queries
/// </summary>
public class OrderRepository : Repository<Order>
{
    public async Task<IEnumerable<Order>> GetOrdersByUserAsync(int userId)
    {
        return await FindAsync(o => o.UserId == userId);
    }

    public async Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status)
    {
        return await FindAsync(o => o.Status == status);
    }

    public async Task<Order?> GetOrderByNumberAsync(string orderNumber)
    {
        var orders = await FindAsync(o => o.OrderNumber == orderNumber);
        return orders.FirstOrDefault();
    }

    public async Task<IEnumerable<Order>> GetRecentOrdersAsync(int days)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        return await FindAsync(o => o.OrderDate >= cutoffDate);
    }

    public async Task<decimal> GetTotalRevenueAsync()
    {
        var orders = await GetAllAsync();
        return orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount);
    }
}