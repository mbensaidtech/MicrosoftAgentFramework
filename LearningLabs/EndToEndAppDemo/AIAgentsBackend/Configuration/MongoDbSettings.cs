namespace AIAgentsBackend.Configuration;

/// <summary>
/// Configuration settings for MongoDB.
/// </summary>
public class MongoDbSettings
{
    /// <summary>
    /// Gets or sets the MongoDB connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database name.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection name for chat message store.
    /// </summary>
    public string ChatMessageStoreCollectionName { get; set; } = "chat_history";
}
