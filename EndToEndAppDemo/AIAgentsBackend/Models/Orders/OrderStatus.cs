using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AIAgentsBackend.Models.Orders;

/// <summary>
/// Represents an order status.
/// </summary>
public class OrderStatus
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Unique status identifier (e.g., STATUS-CONFIRMED).
    /// </summary>
    [BsonElement("statusId")]
    public string StatusId { get; set; } = string.Empty;

    /// <summary>
    /// Status code (e.g., CONFIRMED, IN_PREPARATION, SHIPPING, DELIVERED, CANCELED).
    /// </summary>
    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the status.
    /// </summary>
    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this status means.
    /// </summary>
    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Order of the status in the workflow (1 = first, higher = later).
    /// </summary>
    [BsonElement("sortOrder")]
    public int SortOrder { get; set; }

    /// <summary>
    /// Color code for UI display (e.g., #28a745 for green).
    /// </summary>
    [BsonElement("colorCode")]
    public string ColorCode { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a terminal status (order complete/cancelled).
    /// </summary>
    [BsonElement("isFinal")]
    public bool IsFinal { get; set; }
}

