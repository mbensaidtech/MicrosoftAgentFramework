using System.Text.Json;
using CommonUtilities;
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

    /// <summary>
    /// Gets or sets the thread/context identifier used to store and retrieve conversation messages from MongoDB.
    /// This is typically set from HttpContext.Items during construction or generated automatically when saving messages.
    /// </summary>
    public string? ThreadDbKey { get; internal set; }

    /// <summary>
    /// Initializes a new instance of the MongoVectorChatMessageStore.
    /// </summary>
    /// <param name="mongoVectorStore">The MongoDB vector store to use.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor to access the HTTP context.</param>
    /// <param name="collectionName">The name of the collection to use for storing chat messages.</param>
    /// <param name="jsonSerializerOptions">The JSON serializer options to use for serializing chat messages.</param>
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
        }
    }

    /// <summary>
    /// Gets previous messages from the conversation to include in context.
    /// </summary>
    /// <param name="context">The invoking context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The previous messages from the conversation.</returns>
    public override async ValueTask<IEnumerable<ChatMessage>> InvokingAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ThreadDbKey))
        {
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
        
        return messages;
    }

    /// <summary>
    /// Saves the messages from this conversation turn.
    /// </summary>
    /// <param name="context">The invoked context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task result.</returns>
    public override async ValueTask InvokedAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        ThreadDbKey ??= Guid.NewGuid().ToString("N");

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
    }

    /// <summary>
    /// Serializes the chat message store state to a JSON element.
    /// </summary>
    /// <param name="jsonSerializerOptions">The JSON serializer options to use for serializing the chat message store state.</param>
    /// <returns>The serialized chat message store state.</returns>
    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (string.IsNullOrWhiteSpace(ThreadDbKey))
        {
            ThreadDbKey = Guid.NewGuid().ToString("N");
        }
        return JsonSerializer.SerializeToElement(ThreadDbKey);
    }
}
