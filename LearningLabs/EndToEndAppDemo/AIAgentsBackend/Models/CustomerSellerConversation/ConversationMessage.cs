using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AIAgentsBackend.Models.CustomerSellerConversation;

/// <summary>
/// Represents a message in the customer-seller conversation.
/// This is the "official" conversation after AI assistant approval.
/// </summary>
public class ConversationMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// The conversation thread ID (same as AI assistant thread ID).
    /// </summary>
    [BsonElement("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// Who sent the message: "customer" or "seller".
    /// </summary>
    [BsonElement("from")]
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// The message content.
    /// </summary>
    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the message was sent.
    /// </summary>
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The username of the customer (for display purposes).
    /// </summary>
    [BsonElement("customerName")]
    public string? CustomerName { get; set; }
}
