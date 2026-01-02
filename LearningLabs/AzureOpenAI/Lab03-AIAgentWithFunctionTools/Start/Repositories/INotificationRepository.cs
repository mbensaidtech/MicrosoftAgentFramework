namespace AIAgentWithFunctionTools.Repositories;

public interface INotificationRepository
{
    /// <summary>
    /// Adds a notification to the store and returns the notification ID.
    /// </summary>
    string AddNotification(string recipient, string message);
    
    /// <summary>
    /// Gets all notifications for a recipient.
    /// </summary>
    IEnumerable<Notification> GetNotifications(string recipient);
}

public record Notification(string Id, string Recipient, string Message, DateTime CreatedAt);

