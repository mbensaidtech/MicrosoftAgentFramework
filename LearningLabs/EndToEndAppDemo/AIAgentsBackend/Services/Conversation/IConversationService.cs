using AIAgentsBackend.Models.CustomerSellerConversation;

namespace AIAgentsBackend.Services.Conversation;

/// <summary>
/// Service for managing customer-seller conversations.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Adds a customer message to the conversation.
    /// </summary>
    Task<ConversationMessage> AddCustomerMessageAsync(
        string conversationId, 
        string content, 
        string? customerName = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a seller message to the conversation.
    /// </summary>
    Task<ConversationMessage> AddSellerMessageAsync(
        string conversationId, 
        string content, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full conversation history.
    /// </summary>
    Task<IEnumerable<ConversationMessage>> GetConversationAsync(
        string conversationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the conversation history formatted as context for the AI assistant.
    /// </summary>
    Task<string> GetConversationContextAsync(
        string conversationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a conversation exists.
    /// </summary>
    Task<bool> HasExistingConversationAsync(
        string conversationId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears a conversation.
    /// </summary>
    Task ClearConversationAsync(
        string conversationId, 
        CancellationToken cancellationToken = default);
}
