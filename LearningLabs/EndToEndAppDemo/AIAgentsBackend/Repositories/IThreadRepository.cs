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

    /// <summary>
    /// Deletes all messages from threads that start with the given prefix.
    /// </summary>
    Task<long> DeleteThreadsByPrefixAsync(string threadIdPrefix, CancellationToken cancellationToken = default);
}
