using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using AIAgentWithFunctionTools.Repositories;

namespace AIAgentWithFunctionTools.Tools;

/// <summary>
/// Tools for managing notifications. Uses IServiceProvider passed as parameter to resolve dependencies.
/// </summary>
public class NotificationTools
{
    [Description("Sends a notification to a recipient. Returns the notification ID.")]
    public static string SendNotification(
        IServiceProvider serviceProvider,
        [Description("The recipient's name or ID")] string recipient,
        [Description("The notification message")] string message)
    {
        var repository = serviceProvider.GetRequiredService<INotificationRepository>();
        var notificationId = repository.AddNotification(recipient, message);
        return $"Notification sent successfully! ID: {notificationId}, Recipient: {recipient}, Message: {message}";
    }

    [Description("Gets all notifications for a specific recipient.")]
    public static string GetNotificationsForRecipient(
        IServiceProvider serviceProvider,
        [Description("The recipient's name or ID")] string recipient)
    {
        var repository = serviceProvider.GetRequiredService<INotificationRepository>();
        var notifications = repository.GetNotifications(recipient).ToList();

        if (notifications.Count == 0)
            return $"No notifications found for '{recipient}'.";

        var output = $"Notifications for {recipient}:\n";
        foreach (var notification in notifications)
        {
            output += $"- [{notification.Id}] {notification.CreatedAt:HH:mm:ss}: {notification.Message}\n";
        }
        return output;
    }
}
