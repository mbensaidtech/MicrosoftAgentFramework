using System.Text.Json;
using Microsoft.Extensions.AI;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AIAgentWithThreads.Stores;

/// <summary>
/// Stores the chat history in the <c>chat_history</c> collection of a MongoDB database, with the official MongoDB driver:
/// one document per message.
/// </summary>
public sealed class MongoChatHistoryStore : IChatHistoryStore
{
    private const string CollectionName = "chat_history";

    private readonly IMongoCollection<ChatHistoryDocument> _collection;

    public MongoChatHistoryStore(IMongoDatabase database, string description)
    {
        ArgumentNullException.ThrowIfNull(database);
        _collection = database.GetCollection<ChatHistoryDocument>(CollectionName);
        Description = description;
    }

    public string Description { get; }

    public async Task<List<ChatMessage>> GetLastMessagesAsync(string sessionKey, int maxMessages, CancellationToken cancellationToken = default)
    {
        List<ChatHistoryDocument> documents = await _collection
            .Find(document => document.SessionId == sessionKey)
            .SortByDescending(document => document.Timestamp)
            .ThenByDescending(document => document.Order)
            .Limit(maxMessages)
            .ToListAsync(cancellationToken);

        List<ChatMessage> messages = documents.ConvertAll(document => JsonSerializer.Deserialize<ChatMessage>(document.SerializedMessage)!);
        messages.Reverse();
        return messages;
    }

    public async Task AddMessagesAsync(string sessionKey, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        List<ChatHistoryDocument> documents = messages
            .Select((message, index) => new ChatHistoryDocument
            {
                SessionId = sessionKey,
                Timestamp = now,
                // Order of the message inside the run: MongoDB dates are precise to the millisecond only.
                Order = index,
                Role = message.Role.Value,
                MessageText = message.Text,
                SerializedMessage = JsonSerializer.Serialize(message)
            })
            .ToList();

        await _collection.InsertManyAsync(documents, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// A message of the chat history, as stored in MongoDB.
    /// <see cref="Role"/> and <see cref="MessageText"/> are only there to make the collection readable.
    /// </summary>
    private sealed class ChatHistoryDocument
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public string SessionId { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }

        public int Order { get; set; }

        public string Role { get; set; } = string.Empty;

        public string MessageText { get; set; } = string.Empty;

        public string SerializedMessage { get; set; } = string.Empty;
    }
}
