using DesignPatterns.Interfaces;

namespace DesignPatterns.Services;

/// <summary>
/// Notification service implementing the Observer pattern
/// </summary>
public class NotificationService : INotificationService
{
    private readonly Dictionary<string, List<Action<object>>> _subscribers = new();

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        await Task.CompletedTask;
        Console.WriteLine($"Sending email to {to}: {subject}");
        NotifySubscribers("email.sent", new { To = to, Subject = subject });
    }

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        await Task.CompletedTask;
        Console.WriteLine($"Sending SMS to {phoneNumber}: {message}");
        NotifySubscribers("sms.sent", new { PhoneNumber = phoneNumber, Message = message });
    }

    public async Task NotifyAsync(string userId, string message)
    {
        await Task.CompletedTask;
        Console.WriteLine($"Notifying user {userId}: {message}");
        NotifySubscribers("user.notified", new { UserId = userId, Message = message });
    }

    public void Subscribe(string eventName, Action<object> handler)
    {
        if (!_subscribers.ContainsKey(eventName))
        {
            _subscribers[eventName] = new List<Action<object>>();
        }

        _subscribers[eventName].Add(handler);
    }

    public void Unsubscribe(string eventName, Action<object> handler)
    {
        if (_subscribers.ContainsKey(eventName))
        {
            _subscribers[eventName].Remove(handler);
        }
    }

    private void NotifySubscribers(string eventName, object data)
    {
        if (_subscribers.ContainsKey(eventName))
        {
            foreach (var handler in _subscribers[eventName])
            {
                handler(data);
            }
        }
    }
}
