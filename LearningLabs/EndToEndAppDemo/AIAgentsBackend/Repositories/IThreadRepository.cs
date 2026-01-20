using AIAgentsBackend.Agents.Stores;

namespace AIAgentsBackend.Repositories;

/// <summary>
/// Repository interface for managing thread messages in MongoDB.
/// </summary>
public interface IThreadRepository
{
    /// <summary>
    /// Gets all messages for a specific thread.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of chat history items ordered by timestamp.</returns>
    Task<IEnumerable<ChatHistoryItem>> GetThreadMessagesAsync(string threadId, CancellationToken cancellationToken = default);
}

