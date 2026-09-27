using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AIAgentWithThreads.Stores;

/// <summary>
/// A <see cref="ChatHistoryProvider"/> that stores the chat history in a MongoDB collection, with the official MongoDB driver.
/// <para>
/// Same pattern as <see cref="VectorChatHistoryProvider"/>: the provider only holds the collection, and the key of the
/// conversation (<see cref="State.SessionDbKey"/>) lives in <see cref="AgentSession.StateBag"/>. The messages survive the
/// application: a session serialized today can be restored tomorrow, by another process, with a new agent.
/// </para>
/// </summary>
internal sealed class MongoChatHistoryProvider : ChatHistoryProvider
{
    private const string CollectionName = "chat_history";

    // Number of messages sent back to the model: the most recent ones. Keeps the prompt within the model limits.
    private const int MaxMessages = 10;

    private readonly ProviderSessionState<State> _sessionState;
    private readonly IMongoCollection<ChatHistoryDocument> _collection;
    private IReadOnlyList<string>? _stateKeys;

    public MongoChatHistoryProvider(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _collection = database.GetCollection<ChatHistoryDocument>(CollectionName);
        _sessionState = new ProviderSessionState<State>(
            stateInitializer: _ => new State(Guid.NewGuid().ToString("N")),
            stateKey: nameof(MongoChatHistoryProvider));
    }

    public override IReadOnlyList<string> StateKeys => _stateKeys ??= [_sessionState.StateKey];

    /// <summary>
    /// Returns the key under which the messages of the session are stored (field <c>SessionId</c> of the documents).
    /// </summary>
    public string GetSessionDbKey(AgentSession session) => _sessionState.GetOrInitializeState(session).SessionDbKey;

    /// <summary>
    /// Called before each run: loads the most recent messages of the session, in chronological order.
    /// </summary>
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        State state = _sessionState.GetOrInitializeState(context.Session);

        List<ChatHistoryDocument> documents = await _collection
            .Find(document => document.SessionId == state.SessionDbKey)
            .SortByDescending(document => document.Timestamp)
            .ThenByDescending(document => document.Order)
            .Limit(MaxMessages)
            .ToListAsync(cancellationToken);

        List<ChatMessage> messages = documents.ConvertAll(document => JsonSerializer.Deserialize<ChatMessage>(document.SerializedMessage)!);
        messages.Reverse();
        return messages;
    }

    /// <summary>
    /// Called after each successful run: stores the new messages (the request and the response of the run).
    /// </summary>
    protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        State state = _sessionState.GetOrInitializeState(context.Session);
        DateTime now = DateTime.UtcNow;

        List<ChatHistoryDocument> documents = context.RequestMessages
            .Concat(context.ResponseMessages ?? [])
            .Select((message, index) => new ChatHistoryDocument
            {
                SessionId = state.SessionDbKey,
                Timestamp = now,
                // Order of the message inside the run: MongoDB dates are precise to the millisecond only.
                Order = index,
                Role = message.Role.Value,
                MessageText = message.Text,
                SerializedMessage = JsonSerializer.Serialize(message)
            })
            .ToList();

        if (documents.Count > 0)
        {
            await _collection.InsertManyAsync(documents, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// The per-session state stored in <see cref="AgentSession.StateBag"/>.
    /// </summary>
    public sealed class State(string sessionDbKey)
    {
        public string SessionDbKey { get; } = sessionDbKey ?? throw new ArgumentNullException(nameof(sessionDbKey));
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
