using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.MongoDB;

namespace AIAgentWithThreads.Stores;

/// <summary>
/// MongoDB implementation of ChatMessageStore using a vector store.
/// Messages are persisted to MongoDB and survive application restarts.
/// Suitable for production scenarios where conversation history must be preserved.
/// </summary>
internal sealed class MongoVectorChatMessageStore : ChatMessageStore
{
    private readonly MongoVectorStore _mongoVectorStore;
    private readonly ILogger<MongoVectorChatMessageStore>? _logger;

    public MongoVectorChatMessageStore(
        MongoVectorStore mongoVectorStore,
        JsonElement serializedStoreState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        ILogger<MongoVectorChatMessageStore>? logger = null)
    {
        this._mongoVectorStore = mongoVectorStore ?? throw new ArgumentNullException(nameof(mongoVectorStore));
        this._logger = logger;

        // Extract ThreadDbKey from serialized state
        if (serializedStoreState.ValueKind is JsonValueKind.String)
        {
            var threadIdString = serializedStoreState.GetString();
            if (!string.IsNullOrWhiteSpace(threadIdString))
            {
                this.ThreadDbKey = threadIdString;
            }
        }
    }

    public string? ThreadDbKey { get; internal set; }

    public override async Task AddMessagesAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        this.ThreadDbKey ??= Guid.NewGuid().ToString("N");

        // Materialize the messages to ensure all are captured
        var messagesList = messages.ToList();

        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var collection = this._mongoVectorStore.GetCollection<string, ChatHistoryItem>("chat_history");
        await collection.EnsureCollectionExistsAsync(cancellationToken);

        var chatHistoryItems = messagesList.Select((x, index) =>
        {
            // Generate unique key: if MessageId is null/empty, use timestamp + index
            var messageIdPart = !string.IsNullOrWhiteSpace(x.MessageId)
                ? x.MessageId
                : $"{baseTimestamp}_{index}";
            var key = this.ThreadDbKey + messageIdPart;

            return new ChatHistoryItem()
            {
                Key = key,
                Timestamp = baseTimestamp + index,
                ThreadId = this.ThreadDbKey,
                SerializedMessage = JsonSerializer.Serialize(x),
                MessageText = x.Text
            };
        }).ToList();

        await collection.UpsertAsync(chatHistoryItems, cancellationToken);
    }

    public override async Task<IEnumerable<ChatMessage>> GetMessagesAsync(
        CancellationToken cancellationToken)
    {
        // If ThreadDbKey is null, return empty (messages haven't been loaded yet or thread is new)
        if (string.IsNullOrWhiteSpace(this.ThreadDbKey))
        {
            return Array.Empty<ChatMessage>();
        }

        var collection = this._mongoVectorStore.GetCollection<string, ChatHistoryItem>("chat_history");
        await collection.EnsureCollectionExistsAsync(cancellationToken);
        var records = collection
            .GetAsync(
                x => x.ThreadId == this.ThreadDbKey, 10,
                new() { OrderBy = x => x.Descending(y => y.Timestamp) },
                cancellationToken);

        List<ChatMessage> messages = [];
        await foreach (var record in records)
        {
            messages.Add(JsonSerializer.Deserialize<ChatMessage>(record.SerializedMessage!)!);
        }

        messages.Reverse();
        return messages;
    }

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        // Serialize the thread id so we can retrieve messages using the same thread id later.
        if (string.IsNullOrWhiteSpace(this.ThreadDbKey))
        {
            this.ThreadDbKey = Guid.NewGuid().ToString("N");
        }
        return JsonSerializer.SerializeToElement(this.ThreadDbKey);
    }
}

