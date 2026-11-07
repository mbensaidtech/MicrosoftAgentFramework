using Microsoft.Extensions.AI;

namespace AIAgentWithSessions.Stores;

/// <summary>
/// Where <see cref="PersistentChatHistoryProvider"/> keeps the messages of the conversations, under the key of each session.
/// The configuration (<c>Storage:Mode</c>) chooses the implementation: <see cref="MongoChatHistoryStore"/> or <see cref="JsonFileChatHistoryStore"/>.
/// </summary>
public interface IChatHistoryStore
{
    /// <summary>Where the messages are stored, for the console.</summary>
    string Description { get; }

    /// <summary>Returns the most recent messages of a session, in chronological order.</summary>
    Task<List<ChatMessage>> GetLastMessagesAsync(string sessionKey, int maxMessages, CancellationToken cancellationToken = default);

    /// <summary>Stores the new messages of a session, in the order of the list.</summary>
    Task AddMessagesAsync(string sessionKey, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
