using DesignPatterns.Interfaces;

namespace DesignPatterns.Services;

/// <summary>
/// Decorator pattern: Adds logging to an INotificationService implementation
/// </summary>
public class LoggingNotificationService(INotificationService inner) : INotificationService
{
    private readonly INotificationService _inner = inner;

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        Console.WriteLine($"[LOG] Sending email to {to}...");
        await _inner.SendEmailAsync(to, subject, body);
        Console.WriteLine($"[LOG] Email sent to {to}.");
    }

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        Console.WriteLine($"[LOG] Sending SMS to {phoneNumber}...");
        await _inner.SendSmsAsync(phoneNumber, message);
        Console.WriteLine($"[LOG] SMS sent to {phoneNumber}.");
    }

    public async Task NotifyAsync(string userId, string message)
    {
        Console.WriteLine($"[LOG] Notifying user {userId}...");
        await _inner.NotifyAsync(userId, message);
        Console.WriteLine($"[LOG] Notification sent to {userId}.");
    }

    public void Subscribe(string eventName, Action<object> handler) => _inner.Subscribe(eventName, handler);
    public void Unsubscribe(string eventName, Action<object> handler) => _inner.Unsubscribe(eventName, handler);
}
