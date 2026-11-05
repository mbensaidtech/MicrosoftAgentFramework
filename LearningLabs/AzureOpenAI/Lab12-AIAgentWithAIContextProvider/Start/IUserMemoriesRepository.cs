namespace AIAgentWithAIContextProvider;

/// <summary>
/// Stores the long-term memories of the users. The memories belong to a <b>user</b>, not to a conversation: every session of the
/// same user reads the same memories, and they survive the application.
/// The configuration (<c>Storage:Mode</c>) chooses the implementation: <see cref="MongoUserMemoriesRepository"/> or
/// <see cref="JsonFileUserMemoriesRepository"/>.
/// </summary>
public interface IUserMemoriesRepository
{
    /// <summary>Where the memories are stored, for the console.</summary>
    string Description { get; }

    /// <summary>Returns the memories of a user, in the order they were stored.</summary>
    Task<List<string>> GetMemoriesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Stores new memories for a user.</summary>
    Task AddMemoriesAsync(string userId, IEnumerable<string> memories, CancellationToken cancellationToken = default);

    /// <summary>Deletes all the memories of a user ("forget me").</summary>
    /// <returns>The number of deleted memories.</returns>
    Task<long> DeleteMemoriesAsync(string userId, CancellationToken cancellationToken = default);
}
