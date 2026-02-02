namespace AIAgentsBackend.Controllers.Models;

/// <summary>
/// Response model for standard chat endpoint.
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// The agent's response message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The conversation context ID (use for follow-up messages).
    /// </summary>
    public string ContextId { get; set; } = string.Empty;

    /// <summary>
    /// The agent that handled the request.
    /// </summary>
    public string Agent { get; set; } = string.Empty;
}
