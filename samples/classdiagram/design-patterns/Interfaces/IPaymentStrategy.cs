namespace DesignPatterns.Interfaces;

/// <summary>
/// Strategy pattern interface for payment processing
/// </summary>
public interface IPaymentStrategy
{
    Task<bool> ProcessPaymentAsync(decimal amount, string accountInfo);
    Task<bool> RefundAsync(string transactionId, decimal amount);
    bool ValidateAccount(string accountInfo);
    string GetPaymentMethodName();
}
