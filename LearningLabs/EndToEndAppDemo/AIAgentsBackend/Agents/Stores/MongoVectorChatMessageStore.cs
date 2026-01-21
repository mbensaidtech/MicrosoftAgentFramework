using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.MongoDB;

namespace AIAgentsBackend.Agents.Stores;

/// <summary>
/// Stores chat messages in MongoDB so conversations persist across restarts.
/// </summary>
public sealed class MongoVectorChatMessageStore : ChatMessageStore
{
    private readonly MongoVectorStore mongoVectorStore;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly string collectionName;

    /// <summary>
    /// Key for storing the context ID in HttpContext.Items.
    /// </summary>
    public const string ContextIdKey = "A2A_ContextId";

    public MongoVectorChatMessageStore(
        MongoVectorStore mongoVectorStore,
        IHttpContextAccessor httpContextAccessor,
        string collectionName = "chat_history",
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        this.mongoVectorStore = mongoVectorStore ?? throw new ArgumentNullException(nameof(mongoVectorStore));
        this.httpContextAccessor = httpContextAccessor;
        this.collectionName = collectionName;

        var httpContext = httpContextAccessor?.HttpContext;
        if (httpContext?.Items.TryGetValue(ContextIdKey, out var contextIdObj) == true 
            && contextIdObj is string contextId 
            && !string.IsNullOrWhiteSpace(contextId))
        {
            ThreadDbKey = contextId;
            Console.WriteLine($"[MongoStore] Using contextId from HttpContext: {ThreadDbKey}");
        }
        else
        {
            Console.WriteLine("[MongoStore] No contextId in HttpContext, will generate new one on first message");
        }
    }

    public string? ThreadDbKey { get; internal set; }

    /// <summary>
    /// Gets previous messages from the conversation to include in context.
    /// </summary>
    public override async ValueTask<IEnumerable<ChatMessage>> InvokingAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[MongoStore] InvokingAsync called with ThreadDbKey: {ThreadDbKey ?? "(null)"}");
        
        if (string.IsNullOrWhiteSpace(ThreadDbKey))
        {
            Console.WriteLine("[MongoStore] ThreadDbKey is empty, returning no messages");
            return Array.Empty<ChatMessage>();
        }

        var collection = mongoVectorStore.GetCollection<string, ChatHistoryItem>(collectionName);
        await collection.EnsureCollectionExistsAsync(cancellationToken);
        
        var records = collection.GetAsync(
            x => x.ThreadId == ThreadDbKey, 
            10,
            new() { OrderBy = x => x.Descending(y => y.Timestamp) },
            cancellationToken);

        List<ChatMessage> messages = [];
        await foreach (var record in records)
        {
            messages.Add(JsonSerializer.Deserialize<ChatMessage>(record.SerializedMessage!)!);
        }

        messages.Reverse();
        
        Console.WriteLine($"[MongoStore] Retrieved {messages.Count} messages from thread {ThreadDbKey}");
        
        return messages;
    }

    /// <summary>
    /// Saves the messages from this conversation turn.
    /// </summary>
    public override async ValueTask InvokedAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[MongoStore] InvokedAsync called with ThreadDbKey: {ThreadDbKey ?? "(null)"}");
        
        ThreadDbKey ??= Guid.NewGuid().ToString("N");
        
        Console.WriteLine($"[MongoStore] Using ThreadDbKey: {ThreadDbKey}");

        var allMessages = new List<ChatMessage>();
        
        if (context.RequestMessages != null)
        {
            allMessages.AddRange(context.RequestMessages);
        }
        
        if (context.ResponseMessages != null)
        {
            allMessages.AddRange(context.ResponseMessages);
        }

        if (allMessages.Count == 0)
        {
            Console.WriteLine("[MongoStore] No messages to store");
            return;
        }

        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var collection = mongoVectorStore.GetCollection<string, ChatHistoryItem>(collectionName);
        await collection.EnsureCollectionExistsAsync(cancellationToken);

        var chatHistoryItems = allMessages.Select((x, index) =>
        {
            var messageIdPart = !string.IsNullOrWhiteSpace(x.MessageId)
                ? x.MessageId
                : $"{baseTimestamp}_{index}";
            var key = ThreadDbKey + messageIdPart;

            return new ChatHistoryItem
            {
                Key = key,
                Timestamp = baseTimestamp + index,
                ThreadId = ThreadDbKey,
                SerializedMessage = JsonSerializer.Serialize(x),
                MessageText = x.Text
            };
        }).ToList();

        await collection.UpsertAsync(chatHistoryItems, cancellationToken);
        
        Console.WriteLine($"[MongoStore] Added {chatHistoryItems.Count} messages to thread {ThreadDbKey}");
    }

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (string.IsNullOrWhiteSpace(ThreadDbKey))
        {
            ThreadDbKey = Guid.NewGuid().ToString("N");
        }
        Console.WriteLine($"[MongoStore] Serialize() returning ThreadDbKey: {ThreadDbKey}");
        return JsonSerializer.SerializeToElement(ThreadDbKey);
    }
}
