using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AIAgentWithAIContextProvider.Models;

/// <summary>
/// MongoDB document representing a user memory.
/// </summary>
internal class UserMemoryDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [BsonElement("memory")]
    public string Memory { get; set; } = string.Empty;
}
