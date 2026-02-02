namespace AIAgentsBackend.Controllers.Models;

/// <summary>
/// DTO for a chat message.
/// </summary>
public class MessageDto
{
    public string? Key { get; set; }
    public long Timestamp { get; set; }
    public string? MessageText { get; set; }
    public string? SerializedMessage { get; set; }
}

