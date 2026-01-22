using AIAgentsBackend.Models.CustomerSellerConversation;

namespace AIAgentsBackend.Repositories;

/// <summary>
/// Repository for customer-seller conversation messages.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Adds a new message to the conversation.
    /// </summary>
    Task<ConversationMessage> AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all messages for a conversation, ordered by timestamp.
    /// </summary>
    Task<IEnumerable<ConversationMessage>> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last N messages for a conversation.
    /// </summary>
    Task<IEnumerable<ConversationMessage>> GetLastMessagesAsync(string conversationId, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a conversation exists.
    /// </summary>
    Task<bool> ConversationExistsAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all messages in a conversation.
    /// </summary>
    Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default);
}
