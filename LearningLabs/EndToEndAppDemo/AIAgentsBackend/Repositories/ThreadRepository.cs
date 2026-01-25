using AIAgentsBackend.Agents.Stores;
using AIAgentsBackend.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AIAgentsBackend.Repositories;

/// <summary>
/// Retrieves conversation threads from MongoDB.
/// </summary>
public class ThreadRepository : IThreadRepository
{
    private readonly IMongoCollection<ChatHistoryItem> collection;
    private readonly MongoDbSettings settings;

    public ThreadRepository(IMongoClient mongoClient, IOptions<MongoDbSettings> mongoSettings)
    {
        settings = mongoSettings.Value ?? throw new ArgumentNullException(nameof(mongoSettings));
        
        if (mongoClient == null)
            throw new ArgumentNullException(nameof(mongoClient));

        var database = mongoClient.GetDatabase(settings.DatabaseName);
        collection = database.GetCollection<ChatHistoryItem>(settings.ThreadMessagesCollectionName);

        CreateIndexes();
    }

    private void CreateIndexes()
    {
        try
        {
            var threadIdTimestampIndexModel = new CreateIndexModel<ChatHistoryItem>(
                Builders<ChatHistoryItem>.IndexKeys
                    .Ascending(x => x.ThreadId)
                    .Ascending(x => x.Timestamp),
                new CreateIndexOptions { Name = "idx_threadId_timestamp" });

            collection.Indexes.CreateOne(threadIdTimestampIndexModel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ThreadRepository] Warning: Could not create indexes: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all messages from a conversation thread, sorted by time.
    /// </summary>
    public async Task<IEnumerable<ChatHistoryItem>> GetThreadMessagesAsync(
        string threadId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("Thread ID cannot be null or empty", nameof(threadId));

        var filter = Builders<ChatHistoryItem>.Filter.Eq(x => x.ThreadId, threadId);
        var sort = Builders<ChatHistoryItem>.Sort.Ascending(x => x.Timestamp);

        var results = await collection
            .Find(filter)
            .Sort(sort)
            .ToListAsync(cancellationToken);

        return results;
    }

    /// <summary>
    /// Deletes all messages from threads that start with the given prefix.
    /// This is used to delete all AI assistant threads related to a conversation.
    /// </summary>
    public async Task<long> DeleteThreadsByPrefixAsync(
        string threadIdPrefix, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadIdPrefix))
            throw new ArgumentException("Thread ID prefix cannot be null or empty", nameof(threadIdPrefix));

        // Use regex to match all threadIds that start with the prefix
        var filter = Builders<ChatHistoryItem>.Filter.Regex(
            x => x.ThreadId, 
            new MongoDB.Bson.BsonRegularExpression($"^{threadIdPrefix}"));

        var result = await collection.DeleteManyAsync(filter, cancellationToken);
        return result.DeletedCount;
    }
}
