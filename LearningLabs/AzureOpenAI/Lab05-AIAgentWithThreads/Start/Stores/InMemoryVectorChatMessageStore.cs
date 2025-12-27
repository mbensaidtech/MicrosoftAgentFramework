using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

namespace AIAgentWithThreads.Stores;

/// <summary>
/// In-memory implementation of ChatMessageStore using a vector store.
/// Messages are stored in memory and lost when the application stops.
/// Suitable for development and testing scenarios.
/// </summary>
internal sealed class InMemoryVectorChatMessageStore : ChatMessageStore
{
    private readonly VectorStore _vectorStore;

    public InMemoryVectorChatMessageStore(
        VectorStore vectorStore, 
        JsonElement serializedStoreState, 
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        this._vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));

        if (serializedStoreState.ValueKind is JsonValueKind.String)
        {
            // Deserialize the thread id so we can access the same messages as before.
            this.ThreadDbKey = serializedStoreState.Deserialize<string>();
        }
    }

    public string? ThreadDbKey { get; private set; }

    public override async Task AddMessagesAsync(
        IEnumerable<ChatMessage> messages, 
        CancellationToken cancellationToken = default)
    {
        this.ThreadDbKey ??= Guid.NewGuid().ToString("N");

        var collection = this._vectorStore.GetCollection<string, ChatHistoryItem>("chat_history");
        await collection.EnsureCollectionExistsAsync(cancellationToken);

        await collection.UpsertAsync(messages.Select(x => new ChatHistoryItem()
        {
            Key = this.ThreadDbKey + x.MessageId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ThreadId = this.ThreadDbKey,
            SerializedMessage = JsonSerializer.Serialize(x),
            MessageText = x.Text
        }), cancellationToken);
    }

    public override async Task<IEnumerable<ChatMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default)
    {
        var collection = this._vectorStore.GetCollection<string, ChatHistoryItem>("chat_history");
        await collection.EnsureCollectionExistsAsync(cancellationToken);

        var records = await collection
            .GetAsync(
                x => x.ThreadId == this.ThreadDbKey, 10,
                new() { OrderBy = x => x.Descending(y => y.Timestamp) },
                cancellationToken)
            .ToListAsync(cancellationToken);

        var messages = records.ConvertAll(x => JsonSerializer.Deserialize<ChatMessage>(x.SerializedMessage!)!);
        messages.Reverse();
        return messages;
    }

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null) =>
        // Serialize the thread id so we can retrieve messages using the same thread id later.
        JsonSerializer.SerializeToElement(this.ThreadDbKey);
}

