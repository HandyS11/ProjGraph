using DesignPatterns.Interfaces;

namespace DesignPatterns.Strategies;

/// <summary>
/// Credit card payment strategy implementation
/// </summary>
public class CreditCardPayment : IPaymentStrategy
{
    public async Task<bool> ProcessPaymentAsync(decimal amount, string accountInfo)
    {
        await Task.CompletedTask;
        // Simulate credit card processing
        if (ValidateAccount(accountInfo))
        {
            Console.WriteLine($"Processing credit card payment of ${amount}");
            return true;
        }

        return false;
    }

    public async Task<bool> RefundAsync(string transactionId, decimal amount)
    {
        await Task.CompletedTask;
        Console.WriteLine($"Refunding ${amount} to credit card, transaction: {transactionId}");
        return true;
    }

    public bool ValidateAccount(string accountInfo)
    {
        // Basic credit card validation (Luhn algorithm would be used in real app)
        return !string.IsNullOrEmpty(accountInfo) && accountInfo.Length >= 13;
    }

    public string GetPaymentMethodName()
    {
        return "Credit Card";
    }
}
