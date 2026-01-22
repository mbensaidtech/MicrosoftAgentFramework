namespace AIAgentsBackend.Controllers.Models;

/// <summary>
/// Request model for the Message Formulator Agent.
/// </summary>
public class MessageFormulatorRequest
{
    /// <summary>
    /// The customer's message to the AI assistant.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The AI assistant thread context ID (temporary, for drafting).
    /// </summary>
    public string? ContextId { get; set; }

    /// <summary>
    /// The main conversation ID (persistent, for customer-seller conversation).
    /// This ID links the AI assistant thread to the customer-seller conversation.
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// The customer's username (for display in conversation).
    /// </summary>
    public string? CustomerName { get; set; }
}

/// <summary>
/// Request to save an approved message to the customer-seller conversation.
/// </summary>
public class SaveMessageRequest
{
    /// <summary>
    /// The conversation ID.
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// The approved message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The customer's username.
    /// </summary>
    public string? CustomerName { get; set; }
}

/// <summary>
/// Response containing conversation messages.
/// </summary>
public class ConversationResponse
{
    /// <summary>
    /// The conversation ID.
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// The messages in the conversation.
    /// </summary>
    public List<ConversationMessageDto> Messages { get; set; } = new();
}

/// <summary>
/// A message in the customer-seller conversation.
/// </summary>
public class ConversationMessageDto
{
    /// <summary>
    /// Who sent the message: "customer" or "seller".
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// The message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the message was sent.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The customer's name (if from customer).
    /// </summary>
    public string? CustomerName { get; set; }
}
