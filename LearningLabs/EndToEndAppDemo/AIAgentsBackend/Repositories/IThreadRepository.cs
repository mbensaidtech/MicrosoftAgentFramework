using AIAgentsBackend.Agents.Stores;

namespace AIAgentsBackend.Repositories;

/// <summary>
/// Accesses conversation threads stored in MongoDB.
/// </summary>
public interface IThreadRepository
{
    /// <summary>
    /// Gets all messages from a conversation thread, sorted by time.
    /// </summary>
    Task<IEnumerable<ChatHistoryItem>> GetThreadMessagesAsync(string threadId, CancellationToken cancellationToken = default);
}
