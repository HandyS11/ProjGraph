using DesignPatterns.Interfaces;

namespace DesignPatterns.Services;

/// <summary>
/// Payment processor using strategy pattern
/// </summary>
public class PaymentProcessor
{
    private readonly IPaymentStrategy _paymentStrategy;
    private readonly INotificationService _notificationService;

    public PaymentProcessor(
        IPaymentStrategy paymentStrategy,
        INotificationService notificationService)
    {
        _paymentStrategy = paymentStrategy;
        _notificationService = notificationService;
    }

    public async Task<bool> ProcessPaymentAsync(decimal amount, string accountInfo, string userEmail)
    {
        if (!_paymentStrategy.ValidateAccount(accountInfo))
        {
            await _notificationService.SendEmailAsync(
                userEmail,
                "Payment Failed",
                "Invalid payment information"
            );
            return false;
        }

        var success = await _paymentStrategy.ProcessPaymentAsync(amount, accountInfo);

        if (success)
        {
            await _notificationService.SendEmailAsync(
                userEmail,
                "Payment Successful",
                $"Your payment of ${amount} has been processed successfully using {_paymentStrategy.GetPaymentMethodName()}"
            );
        }
        else
        {
            await _notificationService.SendEmailAsync(
                userEmail,
                "Payment Failed",
                "Payment processing failed. Please try again."
            );
        }

        return success;
    }

    public async Task<bool> RefundPaymentAsync(string transactionId, decimal amount, string userEmail)
    {
        var success = await _paymentStrategy.RefundAsync(transactionId, amount);

        if (success)
        {
            await _notificationService.SendEmailAsync(
                userEmail,
                "Refund Processed",
                $"Your refund of ${amount} has been processed"
            );
        }

        return success;
    }
}
