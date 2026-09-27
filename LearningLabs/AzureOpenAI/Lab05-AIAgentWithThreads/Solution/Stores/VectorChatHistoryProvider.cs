using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace AIAgentWithThreads.Stores;

/// <summary>
/// A <see cref="ChatHistoryProvider"/> that stores the chat history in a vector store
/// (adapted from the official sample Agent_Step04_3rdPartyChatHistoryStorage).
/// <para>
/// The same provider instance is used by every session of the agent: it keeps no session data in its fields.
/// The only per-session value, the key under which the messages are stored (<see cref="State.SessionDbKey"/>),
/// lives in <see cref="AgentSession.StateBag"/>, so it is saved and restored with the session.
/// </para>
/// </summary>
internal sealed class VectorChatHistoryProvider : ChatHistoryProvider
{
    private const string CollectionName = "chat_history";

    // Number of messages sent back to the model: the most recent ones. Keeps the prompt within the model limits.
    private const int MaxMessages = 10;

    private readonly ProviderSessionState<State> _sessionState;
    private readonly VectorStore _vectorStore;
    private IReadOnlyList<string>? _stateKeys;

    public VectorChatHistoryProvider(VectorStore vectorStore)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _sessionState = new ProviderSessionState<State>(
            // Called the first time the provider sees a session: a new session gets a new key.
            stateInitializer: _ => new State(Guid.NewGuid().ToString("N")),
            // Name of the entry of the StateBag (and of the serialized session) that holds the state.
            stateKey: nameof(VectorChatHistoryProvider));
    }

    public override IReadOnlyList<string> StateKeys => _stateKeys ??= [_sessionState.StateKey];

    /// <summary>
    /// Returns the key under which the messages of the session are stored.
    /// </summary>
    public string GetSessionDbKey(AgentSession session) => _sessionState.GetOrInitializeState(session).SessionDbKey;

    /// <summary>
    /// Called before each run: loads the history of the session. The agent sends it to the model before the new messages.
    /// </summary>
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        State state = _sessionState.GetOrInitializeState(context.Session);
        VectorStoreCollection<string, ChatHistoryItem> collection = await GetCollectionAsync(cancellationToken);

        List<ChatHistoryItem> records = await collection
            .GetAsync(
                item => item.SessionId == state.SessionDbKey,
                MaxMessages,
                new() { OrderBy = order => order.Descending(item => item.Timestamp) },
                cancellationToken)
            .ToListAsync(cancellationToken);

        List<ChatMessage> messages = records.ConvertAll(item => JsonSerializer.Deserialize<ChatMessage>(item.SerializedMessage!)!);
        messages.Reverse();
        return messages;
    }

    /// <summary>
    /// Called after each successful run: stores the new messages (the request and the response of the run).
    /// </summary>
    protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        State state = _sessionState.GetOrInitializeState(context.Session);
        VectorStoreCollection<string, ChatHistoryItem> collection = await GetCollectionAsync(cancellationToken);

        IEnumerable<ChatMessage> newMessages = context.RequestMessages.Concat(context.ResponseMessages ?? []);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await collection.UpsertAsync(newMessages.Select((message, index) => new ChatHistoryItem
        {
            Key = $"{state.SessionDbKey}-{message.MessageId ?? Guid.NewGuid().ToString("N")}",
            // One tick per message: keeps the order of the messages stored by the same run.
            Timestamp = now.AddTicks(index),
            SessionId = state.SessionDbKey,
            SerializedMessage = JsonSerializer.Serialize(message),
            MessageText = message.Text
        }), cancellationToken);
    }

    private async Task<VectorStoreCollection<string, ChatHistoryItem>> GetCollectionAsync(CancellationToken cancellationToken)
    {
        VectorStoreCollection<string, ChatHistoryItem> collection = _vectorStore.GetCollection<string, ChatHistoryItem>(CollectionName);
        await collection.EnsureCollectionExistsAsync(cancellationToken);
        return collection;
    }

    /// <summary>
    /// The per-session state stored in <see cref="AgentSession.StateBag"/>.
    /// </summary>
    public sealed class State(string sessionDbKey)
    {
        public string SessionDbKey { get; } = sessionDbKey ?? throw new ArgumentNullException(nameof(sessionDbKey));
    }

    /// <summary>
    /// A message of the chat history, as stored in the vector store.
    /// </summary>
    private sealed class ChatHistoryItem
    {
        [VectorStoreKey]
        public string? Key { get; set; }

        [VectorStoreData]
        public string? SessionId { get; set; }

        [VectorStoreData]
        public DateTimeOffset? Timestamp { get; set; }

        [VectorStoreData]
        public string? SerializedMessage { get; set; }

        [VectorStoreData]
        public string? MessageText { get; set; }
    }
}
