using Microsoft.Extensions.VectorData;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AIAgentsBackend.Agents.Stores;

/// <summary>
/// Represents a chat history item for the vector store.
/// </summary>
public sealed class ChatHistoryItem
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfDefault]
    public string? Id { get; set; }

    [VectorStoreKey]
    public string? Key { get; set; }

    [VectorStoreData]
    public string? ThreadId { get; set; }

    [VectorStoreData]
    public long Timestamp { get; set; }

    [VectorStoreData]
    public string? SerializedMessage { get; set; }

    [VectorStoreData]
    public string? MessageText { get; set; }
}
