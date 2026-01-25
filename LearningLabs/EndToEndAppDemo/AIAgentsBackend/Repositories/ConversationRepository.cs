using AIAgentsBackend.Configuration;
using AIAgentsBackend.Models.CustomerSellerConversation;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AIAgentsBackend.Repositories;

/// <summary>
/// MongoDB repository for customer-seller conversation messages.
/// </summary>
public class ConversationRepository : IConversationRepository
{
    private readonly IMongoCollection<ConversationMessage> collection;

    public ConversationRepository(IMongoClient mongoClient, IOptions<MongoDbSettings> mongoSettings)
    {
        var settings = mongoSettings.Value ?? throw new ArgumentNullException(nameof(mongoSettings));
        
        if (mongoClient == null)
            throw new ArgumentNullException(nameof(mongoClient));

        var database = mongoClient.GetDatabase(settings.DatabaseName);
        collection = database.GetCollection<ConversationMessage>(settings.CustomerSellerConversationCollectionName);

        CreateIndexes();
    }

    private void CreateIndexes()
    {
        try
        {
            // Index for querying by conversationId and ordering by timestamp
            var conversationIdTimestampIndex = new CreateIndexModel<ConversationMessage>(
                Builders<ConversationMessage>.IndexKeys
                    .Ascending(x => x.ConversationId)
                    .Ascending(x => x.Timestamp),
                new CreateIndexOptions { Name = "idx_conversationId_timestamp" });

            collection.Indexes.CreateOne(conversationIdTimestampIndex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConversationRepository] Warning: Could not create indexes: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<ConversationMessage> AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        if (string.IsNullOrWhiteSpace(message.ConversationId))
            throw new ArgumentException("ConversationId is required", nameof(message));

        message.Timestamp = DateTime.UtcNow;
        await collection.InsertOneAsync(message, cancellationToken: cancellationToken);
        
        return message;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ConversationMessage>> GetConversationAsync(
        string conversationId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("ConversationId cannot be null or empty", nameof(conversationId));

        var filter = Builders<ConversationMessage>.Filter.Eq(x => x.ConversationId, conversationId);
        var sort = Builders<ConversationMessage>.Sort.Ascending(x => x.Timestamp);

        var results = await collection
            .Find(filter)
            .Sort(sort)
            .ToListAsync(cancellationToken);
        
        return results;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ConversationMessage>> GetLastMessagesAsync(
        string conversationId, 
        int count, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("ConversationId cannot be null or empty", nameof(conversationId));

        var filter = Builders<ConversationMessage>.Filter.Eq(x => x.ConversationId, conversationId);
        var sort = Builders<ConversationMessage>.Sort.Descending(x => x.Timestamp);

        var results = await collection
            .Find(filter)
            .Sort(sort)
            .Limit(count)
            .ToListAsync(cancellationToken);

        // Reverse to get chronological order
        results.Reverse();
        
        return results;
    }

    /// <inheritdoc />
    public async Task<bool> ConversationExistsAsync(
        string conversationId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return false;

        var filter = Builders<ConversationMessage>.Filter.Eq(x => x.ConversationId, conversationId);
        var count = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        
        return count > 0;
    }

    /// <inheritdoc />
    public async Task DeleteConversationAsync(
        string conversationId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("ConversationId cannot be null or empty", nameof(conversationId));

        var filter = Builders<ConversationMessage>.Filter.Eq(x => x.ConversationId, conversationId);
        var result = await collection.DeleteManyAsync(filter, cancellationToken);
    }
}
