using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AIAgentWithSessions.Stores;

/// <summary>
/// A <see cref="ChatHistoryProvider"/> that keeps the chat history outside of the application, in an <see cref="IChatHistoryStore"/>:
/// MongoDB (<see cref="MongoChatHistoryStore"/>) or a JSON file (<see cref="JsonFileChatHistoryStore"/>).
/// <para>
/// Same pattern as the official sample Agent_Step04_3rdPartyChatHistoryStorage: the provider only holds the store, and the key of the
/// conversation (<see cref="State.SessionDbKey"/>) lives in <see cref="AgentSession.StateBag"/>. The messages survive the
/// application: a session serialized today can be restored tomorrow, by another process, with a new agent.
/// </para>
/// </summary>
internal sealed class PersistentChatHistoryProvider : ChatHistoryProvider
{
    // Number of messages sent back to the model: the most recent ones. Keeps the prompt within the model limits.
    private const int MaxMessages = 10;

    private readonly ProviderSessionState<State> _sessionState;
    private readonly IChatHistoryStore _store;
    private IReadOnlyList<string>? _stateKeys;

    public PersistentChatHistoryProvider(IChatHistoryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sessionState = new ProviderSessionState<State>(
            stateInitializer: _ => new State(Guid.NewGuid().ToString("N")),
            stateKey: nameof(PersistentChatHistoryProvider));
    }

    public override IReadOnlyList<string> StateKeys => _stateKeys ??= [_sessionState.StateKey];

    /// <summary>
    /// Returns the key under which the messages of the session are stored (field <c>SessionId</c> of the stored messages).
    /// </summary>
    public string GetSessionDbKey(AgentSession session) => _sessionState.GetOrInitializeState(session).SessionDbKey;

    /// <summary>
    /// Called before each run: loads the most recent messages of the session, in chronological order.
    /// </summary>
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        State state = _sessionState.GetOrInitializeState(context.Session);
        return await _store.GetLastMessagesAsync(state.SessionDbKey, MaxMessages, cancellationToken);
    }

    /// <summary>
    /// Called after each successful run: stores the new messages (the request and the response of the run).
    /// </summary>
    protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        State state = _sessionState.GetOrInitializeState(context.Session);
        List<ChatMessage> messages = context.RequestMessages.Concat(context.ResponseMessages ?? []).ToList();

        if (messages.Count > 0)
        {
            await _store.AddMessagesAsync(state.SessionDbKey, messages, cancellationToken);
        }
    }

    /// <summary>
    /// The per-session state stored in <see cref="AgentSession.StateBag"/>.
    /// </summary>
    public sealed class State(string sessionDbKey)
    {
        public string SessionDbKey { get; } = sessionDbKey ?? throw new ArgumentNullException(nameof(sessionDbKey));
    }
}
