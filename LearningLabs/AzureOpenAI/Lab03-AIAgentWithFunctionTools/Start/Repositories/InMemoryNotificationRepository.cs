namespace AIAgentWithFunctionTools.Repositories;

public class InMemoryNotificationRepository : INotificationRepository
{
    private readonly List<Notification> _notifications = new();

    public string AddNotification(string recipient, string message)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var notification = new Notification(id, recipient, message, DateTime.Now);
        _notifications.Add(notification);
        return id;
    }

    public IEnumerable<Notification> GetNotifications(string recipient)
    {
        return _notifications
            .Where(n => n.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.CreatedAt);
    }
}

