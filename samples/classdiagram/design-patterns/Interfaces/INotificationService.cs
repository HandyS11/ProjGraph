namespace DesignPatterns.Interfaces;

/// <summary>
/// Observer pattern interface for notifications
/// </summary>
public interface INotificationService
{
    Task SendEmailAsync(string to, string subject, string body);
    Task SendSmsAsync(string phoneNumber, string message);
    Task NotifyAsync(string userId, string message);
    void Subscribe(string eventName, Action<object> handler);
    void Unsubscribe(string eventName, Action<object> handler);
}