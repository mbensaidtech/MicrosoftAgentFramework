namespace AIAgentsBackend.Services.MessageContext;

/// <summary>
/// Service for building contextualized messages for AI agents.
/// </summary>
public interface IMessageContextService
{
    /// <summary>
    /// Builds a message with full context including customer information, interaction state, and conversation history.
    /// </summary>
    /// <param name="message">The raw customer message</param>
    /// <param name="contextId">The AI thread context ID</param>
    /// <param name="conversationId">The customer-seller conversation ID (optional)</param>
    /// <param name="customerName">The customer's name (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The contextualized message ready for the AI agent</returns>
    Task<MessageContextResult> BuildContextualMessageAsync(
        string message,
        string contextId,
        string? conversationId,
        string? customerName,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of building a contextualized message.
/// </summary>
public class MessageContextResult
{
    /// <summary>
    /// The message with full context injected.
    /// </summary>
    public required string ContextualizedMessage { get; init; }

    /// <summary>
    /// Whether seller requirements tool should be disabled.
    /// </summary>
    public bool DisableSellerRequirementsTool { get; init; }
}
