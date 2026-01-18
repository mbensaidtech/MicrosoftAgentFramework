namespace AIAgentsBackend.Controllers.Models;

/// <summary>
/// Request model for chat endpoints.
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// The user's message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional conversation context ID for message history.
    /// If not provided, a new conversation is started.
    /// </summary>
    public string? ContextId { get; set; }

    /// <summary>
    /// Optional metadata (userId, etc.)
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
