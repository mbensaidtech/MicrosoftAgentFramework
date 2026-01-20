using AIAgentsBackend.Agents.Stores;
using AIAgentsBackend.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AIAgentsBackend.Repositories;

/// <summary>
/// MongoDB implementation of the thread repository.
/// Handles data access operations for thread messages.
/// </summary>
public class ThreadRepository : IThreadRepository
{
    private readonly IMongoCollection<ChatHistoryItem> _collection;
    private readonly MongoDbSettings _settings;

    public ThreadRepository(IMongoClient mongoClient, IOptions<MongoDbSettings> mongoSettings)
    {
        _settings = mongoSettings.Value ?? throw new ArgumentNullException(nameof(mongoSettings));
        
        if (mongoClient == null)
            throw new ArgumentNullException(nameof(mongoClient));

        var database = mongoClient.GetDatabase(_settings.DatabaseName);
        _collection = database.GetCollection<ChatHistoryItem>(_settings.ThreadMessagesCollectionName);

        // Ensure indexes for better query performance
        CreateIndexes();
    }

    /// <summary>
    /// Creates indexes for optimized queries.
    /// </summary>
    private void CreateIndexes()
    {
        try
        {
            // Compound index on ThreadId and Timestamp for sorted queries
            var threadIdTimestampIndexModel = new CreateIndexModel<ChatHistoryItem>(
                Builders<ChatHistoryItem>.IndexKeys
                    .Ascending(x => x.ThreadId)
                    .Ascending(x => x.Timestamp),
                new CreateIndexOptions { Name = "idx_threadId_timestamp" });

            _collection.Indexes.CreateOne(threadIdTimestampIndexModel);
        }
        catch (Exception ex)
        {
            // Log the error but don't fail - indexes might already exist
            Console.WriteLine($"[ThreadRepository] Warning: Could not create indexes: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all messages for a specific thread, ordered by timestamp.
    /// </summary>
    public async Task<IEnumerable<ChatHistoryItem>> GetThreadMessagesAsync(
        string threadId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("Thread ID cannot be null or empty", nameof(threadId));

        var filter = Builders<ChatHistoryItem>.Filter.Eq(x => x.ThreadId, threadId);
        var sort = Builders<ChatHistoryItem>.Sort.Ascending(x => x.Timestamp);

        var results = await _collection
            .Find(filter)
            .Sort(sort)
            .ToListAsync(cancellationToken);

        Console.WriteLine($"[ThreadRepository] Retrieved {results.Count} messages for thread {threadId}");
        
        return results;
    }
}

