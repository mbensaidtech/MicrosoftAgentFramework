namespace AIAgentsBackend.Controllers.Models;

/// <summary>
/// Response model for thread messages.
/// </summary>
public class ThreadMessagesResponse
{
    public string ThreadId { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public List<MessageDto> Messages { get; set; } = new();
}

