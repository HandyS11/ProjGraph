using DesignPatterns.Interfaces;

namespace DesignPatterns.Strategies;

/// <summary>
/// PayPal payment strategy implementation
/// </summary>
public class PayPalPayment : IPaymentStrategy
{
    public async Task<bool> ProcessPaymentAsync(decimal amount, string accountInfo)
    {
        await Task.CompletedTask;
        // Simulate PayPal API call
        if (ValidateAccount(accountInfo))
        {
            Console.WriteLine($"Processing PayPal payment of ${amount}");
            return true;
        }

        return false;
    }

    public async Task<bool> RefundAsync(string transactionId, decimal amount)
    {
        await Task.CompletedTask;
        Console.WriteLine($"Refunding ${amount} via PayPal, transaction: {transactionId}");
        return true;
    }

    public bool ValidateAccount(string accountInfo)
    {
        // Validate PayPal email
        return !string.IsNullOrEmpty(accountInfo) && accountInfo.Contains('@');
    }

    public string GetPaymentMethodName()
    {
        return "PayPal";
    }
}
